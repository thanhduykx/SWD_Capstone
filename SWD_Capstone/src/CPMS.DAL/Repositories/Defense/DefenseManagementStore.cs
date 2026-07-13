using CPMS.BLL.Defense.Management;
using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using CPMS.BLL.Models;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories.Defense;

public sealed class DefenseManagementStore(CpmsDbContext dbContext) : IDefenseManagementStore
{
    public async Task<PagedResult<DefenseRoundResult>> GetRoundsAsync(
        PagedRequest request, CancellationToken cancellationToken)
    {
        var query = dbContext.DefenseRounds.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Code.ToLower().Contains(search) || x.Name.ToLower().Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(request.FilterBy) && !string.IsNullOrWhiteSpace(request.FilterValue))
        {
            var value = request.FilterValue.Trim();
            query = request.FilterBy.Trim().ToLowerInvariant() switch
            {
                "semesterid" when int.TryParse(value, out var semesterId) => query.Where(x => x.SemesterId == semesterId),
                "type" when Enum.TryParse<CouncilType>(value, true, out var type) => query.Where(x => x.Type == type),
                "status" when Enum.TryParse<DefenseRoundStatus>(value, true, out var status) => query.Where(x => x.Status == status),
                _ => query
            };
        }
        query = request.SortBy?.ToLowerInvariant() switch
        {
            "code" => request.Desc ? query.OrderByDescending(x => x.Code) : query.OrderBy(x => x.Code),
            "name" => request.Desc ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
            "semesterid" => request.Desc ? query.OrderByDescending(x => x.SemesterId) : query.OrderBy(x => x.SemesterId),
            "type" => request.Desc ? query.OrderByDescending(x => x.Type) : query.OrderBy(x => x.Type),
            "status" => request.Desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            "enddate" => request.Desc ? query.OrderByDescending(x => x.EndDate) : query.OrderBy(x => x.EndDate),
            _ => request.Desc ? query.OrderByDescending(x => x.StartDate) : query.OrderBy(x => x.StartDate)
        };
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((request.SafePage - 1) * request.SafePageSize).Take(request.SafePageSize)
            .Select(x => new DefenseRoundResult(
                x.Id, x.Code, x.Name, x.SemesterId, x.Type, x.StartDate, x.EndDate, x.Status))
            .ToListAsync(cancellationToken);
        return new PagedResult<DefenseRoundResult>(items, request.SafePage, request.SafePageSize, total);
    }

    public async Task<int?> FindModeratorIdAsync(
        int userId, bool allowFallback, CancellationToken cancellationToken)
    {
        var id = await dbContext.TrainingDepartments.Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (id is null && allowFallback)
            id = await dbContext.TrainingDepartments.OrderBy(x => x.Id)
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken);
        return id;
    }
    public Task<bool> SemesterExistsAsync(int semesterId, CancellationToken cancellationToken) =>
        dbContext.Semesters.AnyAsync(x => x.Id == semesterId, cancellationToken);
    public Task<bool> RoundCodeExistsAsync(
        int semesterId, string code, CancellationToken cancellationToken) =>
        dbContext.DefenseRounds.AnyAsync(x =>
            x.SemesterId == semesterId && x.Code.ToLower() == code.ToLower(), cancellationToken);

    public async Task<DefenseRoundResult> CreateRoundAsync(
        CreateDefenseRoundCommand input, int moderatorId, CancellationToken cancellationToken)
    {
        var entity = new DefenseRound
        {
            Code = input.Code, Name = input.Name, SemesterId = input.SemesterId, Type = input.Type,
            StartDate = input.StartDate, EndDate = input.EndDate, Status = DefenseRoundStatus.Draft,
            CreatedByTrainingDepartmentId = moderatorId
        };
        dbContext.DefenseRounds.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new DefenseRoundResult(
            entity.Id, entity.Code, entity.Name, entity.SemesterId, entity.Type,
            entity.StartDate, entity.EndDate, entity.Status);
    }

    public Task<int> CountExistingLecturersAsync(
        IReadOnlyCollection<int> lecturerIds, CancellationToken cancellationToken) =>
        dbContext.Lecturers.CountAsync(x => lecturerIds.Contains(x.Id), cancellationToken);

    public async Task<DefenseBoardResult> CreateBoardAsync(
        CreateDefenseBoardCommand input, int moderatorId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var council = new Council
        {
            Code = input.Code,
            SemesterId = input.SemesterId,
            ManagedByTrainingDepartmentId = moderatorId,
            ChairmanId = input.ChairmanId,
            SecretaryId = input.SecretaryId,
            Type = input.Type,
            Status = CouncilStatus.Pending
        };
        dbContext.Councils.Add(council);
        await dbContext.SaveChangesAsync(cancellationToken);
        var members = new List<(int LecturerId, CouncilMemberRole Role)>
        {
            (input.ChairmanId, CouncilMemberRole.Chairman),
            (input.SecretaryId, CouncilMemberRole.Secretary)
        };
        members.AddRange(input.MemberLecturerIds.Select(x => (x, CouncilMemberRole.Member)));
        foreach (var member in members.GroupBy(x => x.LecturerId).Select(x => x.First()))
            dbContext.CouncilMembers.Add(new CouncilMember
            {
                CouncilId = council.Id, LecturerId = member.LecturerId, Role = member.Role
            });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await MapBoardAsync(council.Id, cancellationToken);
    }

    public Task<bool> CouncilExistsAsync(int councilId, CancellationToken cancellationToken) =>
        dbContext.Councils.AnyAsync(x => x.Id == councilId, cancellationToken);
    public async Task AddCouncilMemberIfMissingAsync(
        int councilId, int lecturerId, CouncilMemberRole role, CancellationToken cancellationToken)
    {
        if (!await dbContext.CouncilMembers.AnyAsync(x =>
                x.CouncilId == councilId && x.LecturerId == lecturerId, cancellationToken))
        {
            dbContext.CouncilMembers.Add(new CouncilMember
            {
                CouncilId = councilId, LecturerId = lecturerId, Role = role
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<DefenseRoundState?> FindRoundAsync(int roundId, CancellationToken cancellationToken) =>
        dbContext.DefenseRounds.Where(x => x.Id == roundId)
            .Select(x => new DefenseRoundState(x.Id, x.SemesterId, x.Type, x.StartDate, x.EndDate))
            .SingleOrDefaultAsync(cancellationToken);
    public Task<DefenseCouncilState?> FindCouncilAsync(int councilId, CancellationToken cancellationToken) =>
        dbContext.Councils.Where(x => x.Id == councilId)
            .Select(x => new DefenseCouncilState(x.Id, x.Code, x.SemesterId, x.Type))
            .SingleOrDefaultAsync(cancellationToken);
    public Task<DefenseGroupState?> FindGroupAsync(int groupId, CancellationToken cancellationToken) =>
        dbContext.CapstoneGroups.Where(x => x.Id == groupId)
            .Select(x => new DefenseGroupState(x.Id, x.Code, x.SemesterId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<DefenseSessionAssignmentResult> CreateSessionAsync(
        AssignDefenseSessionCommand input,
        int moderatorId,
        DefenseCouncilState council,
        DefenseGroupState group,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var session = new DefenseSession
        {
            Code = input.Code, DefenseRoundId = input.DefenseRoundId, CouncilId = input.CouncilId,
            GroupId = input.GroupId, SessionDate = input.SessionDate, Slot = input.Slot,
            Room = input.Room, AssignedByTrainingDepartmentId = moderatorId
        };
        dbContext.DefenseSessions.Add(session);
        if (!await dbContext.CouncilGroups.AnyAsync(x =>
                x.CouncilId == council.Id && x.GroupId == group.Id, cancellationToken))
            dbContext.CouncilGroups.Add(new CouncilGroup
            {
                CouncilId = council.Id, GroupId = group.Id, Position = input.Slot
            });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MapSession(session, council.Code, group.Code);
    }

    public Task<int?> FindLecturerIdAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Lecturers.Where(x => x.UserId == userId).Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
    public async Task<IReadOnlyList<DefenseSessionAssignmentResult>> GetLecturerSessionsAsync(
        int lecturerId, CancellationToken cancellationToken) =>
        await dbContext.DefenseSessions.Where(session => dbContext.CouncilMembers.Any(member =>
                member.CouncilId == session.CouncilId && member.LecturerId == lecturerId))
            .OrderBy(x => x.SessionDate).ThenBy(x => x.Slot)
            .Select(x => new DefenseSessionAssignmentResult(
                x.Id, x.Code, x.DefenseRoundId, x.CouncilId,
                dbContext.Councils.Where(c => c.Id == x.CouncilId).Select(c => c.Code).Single(),
                x.GroupId,
                dbContext.CapstoneGroups.Where(g => g.Id == x.GroupId).Select(g => g.Code).Single(),
                x.SessionDate, x.Slot, x.Room, x.StartedAt, x.EndedAt, x.IsLocked))
            .ToListAsync(cancellationToken);

    private async Task<DefenseBoardResult> MapBoardAsync(int id, CancellationToken cancellationToken)
    {
        var council = await dbContext.Councils.SingleAsync(x => x.Id == id, cancellationToken);
        var memberIds = await dbContext.CouncilMembers.Where(x => x.CouncilId == id)
            .OrderBy(x => x.Role).ThenBy(x => x.LecturerId).Select(x => x.LecturerId)
            .ToArrayAsync(cancellationToken);
        return new DefenseBoardResult(
            council.Id, council.Code, council.SemesterId, council.Type, council.Status,
            council.ChairmanId, council.SecretaryId, memberIds);
    }
    private static DefenseSessionAssignmentResult MapSession(
        DefenseSession x, string councilCode, string groupCode) =>
        new(x.Id, x.Code, x.DefenseRoundId, x.CouncilId, councilCode, x.GroupId, groupCode,
            x.SessionDate, x.Slot, x.Room, x.StartedAt, x.EndedAt, x.IsLocked);
}

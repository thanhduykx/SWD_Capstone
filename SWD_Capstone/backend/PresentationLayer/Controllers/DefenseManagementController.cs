using System.Globalization;
using System.Security.Claims;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Core.Exceptions;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/defense-management")]
public sealed class DefenseManagementController(CpmsDbContext dbContext) : ControllerBase
{
    [HttpGet("rounds")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<IReadOnlyList<DefenseRoundResponse>> GetRounds(CancellationToken cancellationToken) =>
        await dbContext.DefenseRounds
            .OrderByDescending(x => x.StartDate)
            .Select(x => new DefenseRoundResponse(
                x.Id,
                x.Code,
                x.Name,
                x.SemesterId,
                x.Type,
                x.StartDate,
                x.EndDate,
                x.Status))
            .ToListAsync(cancellationToken);

    [HttpPost("rounds")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<DefenseRoundResponse>> CreateRound(
        CreateDefenseRoundRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureDateRange(request.StartDate, request.EndDate);

        var trainingDepartmentId = await CurrentTrainingDepartmentIdAsync(cancellationToken);
        var semesterExists = await dbContext.Semesters.AnyAsync(x => x.Id == request.SemesterId, cancellationToken);
        if (!semesterExists)
        {
            return BadRequest(new { error = "Semester does not exist." });
        }

        var round = new DefenseRound
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            SemesterId = request.SemesterId,
            Type = request.Type,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = DefenseRoundStatus.Draft,
            CreatedByTrainingDepartmentId = trainingDepartmentId
        };

        dbContext.DefenseRounds.Add(round);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetRounds), new { round.Id }, MapRound(round));
    }

    [HttpPost("boards")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<DefenseBoardResponse>> CreateBoard(
        CreateDefenseBoardRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trainingDepartmentId = await CurrentTrainingDepartmentIdAsync(cancellationToken);
        await EnsureLecturerExistsAsync(request.ChairmanId, cancellationToken);
        await EnsureLecturerExistsAsync(request.SecretaryId, cancellationToken);

        var council = new Council
        {
            Code = request.Code.Trim(),
            SemesterId = request.SemesterId,
            ManagedByTrainingDepartmentId = trainingDepartmentId,
            ChairmanId = request.ChairmanId,
            SecretaryId = request.SecretaryId,
            Type = request.Type,
            Status = CouncilStatus.Pending
        };

        dbContext.Councils.Add(council);
        await dbContext.SaveChangesAsync(cancellationToken);

        await AddCouncilMemberIfMissingAsync(council.Id, request.ChairmanId, CouncilMemberRole.Chairman, cancellationToken);
        await AddCouncilMemberIfMissingAsync(council.Id, request.SecretaryId, CouncilMemberRole.Secretary, cancellationToken);
        foreach (var lecturerId in request.MemberLecturerIds.Distinct())
        {
            await AddCouncilMemberIfMissingAsync(council.Id, lecturerId, CouncilMemberRole.Member, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetBoardSessionsForCurrentLecturer), new { council.Id }, await MapBoardAsync(council.Id, cancellationToken));
    }

    [HttpPost("boards/{councilId:int}/members")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult> AddBoardMember(
        int councilId,
        AddDefenseBoardMemberRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var councilExists = await dbContext.Councils.AnyAsync(x => x.Id == councilId, cancellationToken);
        if (!councilExists)
        {
            return NotFound();
        }

        await AddCouncilMemberIfMissingAsync(councilId, request.LecturerId, request.Role, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("sessions")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<DefenseSessionAssignmentResponse>> AssignProjectToBoard(
        AssignDefenseSessionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trainingDepartmentId = await CurrentTrainingDepartmentIdAsync(cancellationToken);
        var round = await dbContext.DefenseRounds.SingleOrDefaultAsync(x => x.Id == request.DefenseRoundId, cancellationToken)
            ?? throw new KeyNotFoundException("Defense round does not exist.");
        var council = await dbContext.Councils.SingleOrDefaultAsync(x => x.Id == request.CouncilId, cancellationToken)
            ?? throw new KeyNotFoundException("Defense board does not exist.");
        var group = await dbContext.CapstoneGroups.SingleOrDefaultAsync(x => x.Id == request.GroupId, cancellationToken)
            ?? throw new KeyNotFoundException("Project/group does not exist.");

        if (council.SemesterId != round.SemesterId || group.SemesterId != round.SemesterId)
        {
            throw new BusinessRuleException("Defense round, board and project must belong to the same semester.");
        }

        if (council.Type != round.Type)
        {
            throw new BusinessRuleException("Defense board type must match defense round type.");
        }

        var sessionDate = DateOnly.FromDateTime(request.SessionDate);
        if (sessionDate < round.StartDate || sessionDate > round.EndDate)
        {
            throw new BusinessRuleException("Defense session date must be inside the defense round date range.");
        }

        var session = new DefenseSession
        {
            Code = request.Code.Trim(),
            DefenseRoundId = round.Id,
            CouncilId = council.Id,
            GroupId = group.Id,
            SessionDate = request.SessionDate,
            Slot = request.Slot,
            Room = request.Room.Trim(),
            AssignedByTrainingDepartmentId = trainingDepartmentId
        };

        dbContext.DefenseSessions.Add(session);
        if (!await dbContext.CouncilGroups.AnyAsync(x => x.CouncilId == council.Id && x.GroupId == group.Id, cancellationToken))
        {
            dbContext.CouncilGroups.Add(new CouncilGroup
            {
                CouncilId = council.Id,
                GroupId = group.Id,
                Position = request.Slot
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetBoardSessionsForCurrentLecturer), new { session.Id }, MapSession(session, council.Code, group.Code));
    }

    [HttpGet("my-board-sessions")]
    [Authorize(Roles = "Lecturer,EvaluationPanel")]
    public async Task<IReadOnlyList<DefenseSessionAssignmentResponse>> GetBoardSessionsForCurrentLecturer(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var lecturerId = await dbContext.Lecturers
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (lecturerId is null)
        {
            return [];
        }

        return await dbContext.DefenseSessions
            .Where(session => dbContext.CouncilMembers
                .Any(member => member.CouncilId == session.CouncilId && member.LecturerId == lecturerId.Value))
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.Slot)
            .Select(x => new DefenseSessionAssignmentResponse(
                x.Id,
                x.Code,
                x.DefenseRoundId,
                x.CouncilId,
                dbContext.Councils.Where(c => c.Id == x.CouncilId).Select(c => c.Code).Single(),
                x.GroupId,
                dbContext.CapstoneGroups.Where(g => g.Id == x.GroupId).Select(g => g.Code).Single(),
                x.SessionDate,
                x.Slot,
                x.Room,
                x.StartedAt,
                x.EndedAt,
                x.IsLocked))
            .ToListAsync(cancellationToken);
    }

    private async Task<int> CurrentTrainingDepartmentIdAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var trainingDepartmentId = await dbContext.TrainingDepartments
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (trainingDepartmentId is null && User.IsInRole(nameof(UserRole.SystemAdministrator)))
        {
            trainingDepartmentId = await dbContext.TrainingDepartments
                .OrderBy(x => x.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return trainingDepartmentId
            ?? throw new UnauthorizedAccessException("A Training Department moderator profile is required for defense assignment.");
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);

    private async Task EnsureLecturerExistsAsync(int lecturerId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Lecturers.AnyAsync(x => x.Id == lecturerId, cancellationToken))
        {
            throw new KeyNotFoundException($"Lecturer {lecturerId} does not exist.");
        }
    }

    private async Task AddCouncilMemberIfMissingAsync(
        int councilId,
        int lecturerId,
        CouncilMemberRole role,
        CancellationToken cancellationToken)
    {
        await EnsureLecturerExistsAsync(lecturerId, cancellationToken);
        var exists = await dbContext.CouncilMembers
            .AnyAsync(x => x.CouncilId == councilId && x.LecturerId == lecturerId, cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.CouncilMembers.Add(new CouncilMember
        {
            CouncilId = councilId,
            LecturerId = lecturerId,
            Role = role
        });
    }

    private async Task<DefenseBoardResponse> MapBoardAsync(int councilId, CancellationToken cancellationToken)
    {
        var council = await dbContext.Councils.SingleAsync(x => x.Id == councilId, cancellationToken);
        var memberIds = await dbContext.CouncilMembers
            .Where(x => x.CouncilId == councilId)
            .OrderBy(x => x.Role)
            .ThenBy(x => x.LecturerId)
            .Select(x => x.LecturerId)
            .ToArrayAsync(cancellationToken);

        return new DefenseBoardResponse(
            council.Id,
            council.Code,
            council.SemesterId,
            council.Type,
            council.Status,
            council.ChairmanId,
            council.SecretaryId,
            memberIds);
    }

    private static DefenseRoundResponse MapRound(DefenseRound round) =>
        new(round.Id, round.Code, round.Name, round.SemesterId, round.Type, round.StartDate, round.EndDate, round.Status);

    private static DefenseSessionAssignmentResponse MapSession(DefenseSession session, string councilCode, string groupCode) =>
        new(
            session.Id,
            session.Code,
            session.DefenseRoundId,
            session.CouncilId,
            councilCode,
            session.GroupId,
            groupCode,
            session.SessionDate,
            session.Slot,
            session.Room,
            session.StartedAt,
            session.EndedAt,
            session.IsLocked);

    private static void EnsureDateRange(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new BusinessRuleException("End date must not be before start date.");
        }
    }
}

public sealed record CreateDefenseRoundRequest(
    string Code,
    string Name,
    int SemesterId,
    CouncilType Type,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record DefenseRoundResponse(
    int Id,
    string Code,
    string Name,
    int SemesterId,
    CouncilType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    DefenseRoundStatus Status);

public sealed record CreateDefenseBoardRequest(
    string Code,
    int SemesterId,
    CouncilType Type,
    int ChairmanId,
    int SecretaryId,
    IReadOnlyCollection<int> MemberLecturerIds);

public sealed record AddDefenseBoardMemberRequest(int LecturerId, CouncilMemberRole Role);

public sealed record DefenseBoardResponse(
    int Id,
    string Code,
    int SemesterId,
    CouncilType Type,
    CouncilStatus Status,
    int ChairmanId,
    int SecretaryId,
    IReadOnlyCollection<int> MemberLecturerIds);

public sealed record AssignDefenseSessionRequest(
    string Code,
    int DefenseRoundId,
    int CouncilId,
    int GroupId,
    DateTime SessionDate,
    int Slot,
    string Room);

public sealed record DefenseSessionAssignmentResponse(
    int Id,
    string Code,
    int DefenseRoundId,
    int CouncilId,
    string CouncilCode,
    int GroupId,
    string GroupCode,
    DateTime SessionDate,
    int Slot,
    string Room,
    DateTime? StartedAt,
    DateTime? EndedAt,
    bool IsLocked);

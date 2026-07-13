using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Models;

namespace CPMS.BLL.Defense.Management;

public sealed class DefenseManagementService(IDefenseManagementStore store) : IDefenseManagementService
{
    public Task<PagedResult<DefenseRoundResult>> GetRoundsAsync(
        PagedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.GetRoundsAsync(request, cancellationToken);
    }

    public async Task<DefenseRoundResult> CreateRoundAsync(
        CreateDefenseRoundCommand command, DefenseActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureDateRange(command.StartDate, command.EndDate);
        var code = Required(command.Code, "Defense round code is required.");
        var name = Required(command.Name, "Defense round name is required.");
        var moderatorId = await RequireModeratorAsync(actor, cancellationToken);
        if (!await store.SemesterExistsAsync(command.SemesterId, cancellationToken))
            throw new BusinessRuleException("Semester does not exist.");
        if (await store.RoundCodeExistsAsync(command.SemesterId, code, cancellationToken))
            throw new BusinessRuleException("A defense round with this code already exists in the semester.");
        return await store.CreateRoundAsync(command with { Code = code, Name = name }, moderatorId, cancellationToken);
    }

    public async Task<DefenseBoardResult> CreateBoardAsync(
        CreateDefenseBoardCommand command, DefenseActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = Required(command.Code, "Defense board code is required.");
        if (!await store.SemesterExistsAsync(command.SemesterId, cancellationToken))
            throw new BusinessRuleException("Semester does not exist.");
        var lecturerIds = command.MemberLecturerIds.Append(command.ChairmanId).Append(command.SecretaryId)
            .Distinct().ToArray();
        if (await store.CountExistingLecturersAsync(lecturerIds, cancellationToken) != lecturerIds.Length)
            throw new KeyNotFoundException("One or more defense board lecturers do not exist.");
        var moderatorId = await RequireModeratorAsync(actor, cancellationToken);
        return await store.CreateBoardAsync(
            command with { Code = code, MemberLecturerIds = command.MemberLecturerIds.Distinct().ToArray() },
            moderatorId, cancellationToken);
    }

    public async Task AddBoardMemberAsync(
        int councilId, int lecturerId, CouncilMemberRole role, CancellationToken cancellationToken)
    {
        if (!await store.CouncilExistsAsync(councilId, cancellationToken))
            throw new KeyNotFoundException("Defense board does not exist.");
        if (await store.CountExistingLecturersAsync([lecturerId], cancellationToken) != 1)
            throw new KeyNotFoundException($"Lecturer {lecturerId} does not exist.");
        await store.AddCouncilMemberIfMissingAsync(councilId, lecturerId, role, cancellationToken);
    }

    public async Task<DefenseSessionAssignmentResult> AssignSessionAsync(
        AssignDefenseSessionCommand command, DefenseActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = Required(command.Code, "Defense session code is required.");
        var room = Required(command.Room, "Defense room is required.");
        var moderatorId = await RequireModeratorAsync(actor, cancellationToken);
        var round = await store.FindRoundAsync(command.DefenseRoundId, cancellationToken)
            ?? throw new KeyNotFoundException("Defense round does not exist.");
        var council = await store.FindCouncilAsync(command.CouncilId, cancellationToken)
            ?? throw new KeyNotFoundException("Defense board does not exist.");
        var group = await store.FindGroupAsync(command.GroupId, cancellationToken)
            ?? throw new KeyNotFoundException("Project/group does not exist.");
        if (council.SemesterId != round.SemesterId || group.SemesterId != round.SemesterId)
            throw new BusinessRuleException("Defense round, board and project must belong to the same semester.");
        if (council.Type != round.Type)
            throw new BusinessRuleException("Defense board type must match defense round type.");
        var date = DateOnly.FromDateTime(command.SessionDate);
        if (date < round.StartDate || date > round.EndDate)
            throw new BusinessRuleException("Defense session date must be inside the defense round date range.");
        return await store.CreateSessionAsync(
            command with { Code = code, Room = room }, moderatorId, council, group, cancellationToken);
    }

    public async Task<IReadOnlyList<DefenseSessionAssignmentResult>> GetLecturerSessionsAsync(
        int userId, CancellationToken cancellationToken)
    {
        var lecturerId = await store.FindLecturerIdAsync(userId, cancellationToken);
        return lecturerId.HasValue
            ? await store.GetLecturerSessionsAsync(lecturerId.Value, cancellationToken) : [];
    }

    private async Task<int> RequireModeratorAsync(DefenseActor actor, CancellationToken cancellationToken) =>
        await store.FindModeratorIdAsync(actor.UserId, actor.AllowModeratorFallback, cancellationToken)
        ?? throw new UnauthorizedAccessException(
            "A Training Department moderator profile is required for defense assignment.");

    private static void EnsureDateRange(DateOnly start, DateOnly end)
    {
        if (end < start) throw new BusinessRuleException("End date must not be before start date.");
        if (end.DayNumber - start.DayNumber != 6)
            throw new BusinessRuleException("A defense round must have a duration of exactly six days.");
    }
    private static string Required(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new BusinessRuleException(message) : value.Trim();
}

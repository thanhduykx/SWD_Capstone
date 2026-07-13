using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Services;

namespace CPMS.BLL.Reviews.Sessions;

public sealed class ReviewSessionService(IReviewSessionStore store, AssignmentRules rules) : IReviewSessionService
{
    public Task<ReviewSessionResult> CreateAsync(
        AssignReviewSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return store.ExecuteInTransactionAsync(
            token => CreateWithinTransactionAsync(command, token), cancellationToken);
    }

    public Task<IReadOnlyList<ReviewSessionResult>> BulkAssignAsync(
        IReadOnlyCollection<AssignReviewSessionCommand> commands, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (commands.Count == 0)
        {
            throw new BusinessRuleException("At least one review session assignment is required.");
        }
        return store.ExecuteInTransactionAsync<IReadOnlyList<ReviewSessionResult>>(
            async token =>
            {
                var results = new List<ReviewSessionResult>(commands.Count);
                foreach (var command in commands)
                {
                    results.Add(await CreateWithinTransactionAsync(command, token));
                }
                return results;
            },
            cancellationToken);
    }

    public Task<ReviewSessionResult> UpdateAsync(
        int sessionId, UpdateReviewSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return store.ExecuteInTransactionAsync(
            token => UpdateWithinTransactionAsync(sessionId, command, token), cancellationToken);
    }

    public async Task<IReadOnlyList<MyReviewSessionResult>> GetLecturerSessionsAsync(
        int userId, CancellationToken cancellationToken)
    {
        var lecturerId = await store.FindLecturerIdByUserIdAsync(userId, cancellationToken);
        return lecturerId.HasValue
            ? await store.GetLecturerSessionsAsync(lecturerId.Value, cancellationToken)
            : [];
    }

    private async Task<ReviewSessionResult> CreateWithinTransactionAsync(
        AssignReviewSessionCommand command, CancellationToken cancellationToken)
    {
        ValidateText(command.Code, "Session code");
        ValidateText(command.Room, "Room");
        rules.ValidateReviewSlot(command.Slot);
        var sessionDate = NormalizeSessionDate(command.SessionDate);
        var dayOfWeek = IsoDayOfWeek(sessionDate);
        var reviewerIds = NormalizeReviewers(command.ReviewerIds);
        await EnsureLecturersExistAsync(reviewerIds, cancellationToken);
        var group = await store.FindGroupAsync(command.GroupId, cancellationToken)
            ?? throw new KeyNotFoundException("Capstone group does not exist.");
        rules.ValidateReviewAssignment(
            group.SupervisorId,
            reviewerIds,
            command.Type == ReviewType.Review2 ? command.PreviousReviewerIds : null);

        var weekStart = ReviewWeekStart(sessionDate);
        var round = await store.AcquireRoundAsync(
            group.SemesterId, command.Type, DateOnly.FromDateTime(weekStart), cancellationToken);
        if (round?.Status != ReviewRoundStatus.Closed)
        {
            throw new BusinessRuleException("The review round must be Closed before creating assignments.");
        }
        var selectedGroups = await store.CountGroupsWithSelectedAvailabilityAsync(
            round.Id, [group.Id], dayOfWeek, command.Slot,
            AssignmentRules.RequiredStudentAvailabilityCount, cancellationToken);
        if (selectedGroups != 1)
        {
            throw new BusinessRuleException(
                "The group must submit exactly five slots including the selected assignment slot.");
        }
        await EnsureReviewersAvailableAsync(round.Id, dayOfWeek, command.Slot, reviewerIds, cancellationToken);
        var alreadyScheduled = await store.IsGroupScheduledAsync(
            group.Id, command.Type, weekStart, weekStart.AddDays(7), cancellationToken);
        rules.EnsureGroupCanBeScheduledForReviewType(alreadyScheduled);
        rules.EnsureLecturerAvailableForReviewSlot(await store.HasReviewerConflictAsync(
            null, sessionDate, command.Slot, reviewerIds, cancellationToken));

        return await store.CreateSessionAsync(
            new PersistReviewSession(
                command.Code.Trim(), group.SemesterId, group.Id, command.Type, reviewerIds,
                dayOfWeek, command.Slot, command.Room.Trim(), sessionDate, DateTime.UtcNow),
            cancellationToken);
    }

    private async Task<ReviewSessionResult> UpdateWithinTransactionAsync(
        int sessionId, UpdateReviewSessionCommand command, CancellationToken cancellationToken)
    {
        ValidateText(command.Code, "Session code");
        ValidateText(command.Room, "Room");
        var session = await store.FindSessionForEditAsync(sessionId, cancellationToken)
            ?? throw new KeyNotFoundException("Review session not found.");
        if (session.Status != ReviewSessionStatus.Draft || command.Status == ReviewSessionStatus.Published)
        {
            throw new ConflictException(
                "Only Draft sessions can be edited; publishing must use the schedule publish endpoint.");
        }

        rules.ValidateReviewSlot(command.Slot);
        var sessionDate = NormalizeSessionDate(command.SessionDate);
        var dayOfWeek = IsoDayOfWeek(sessionDate);
        var reviewerIds = NormalizeReviewers(command.ReviewerIds);
        foreach (var supervisorId in session.SupervisorIds)
        {
            rules.ValidateReviewAssignment(supervisorId, reviewerIds, command.PreviousReviewerIds);
        }
        await EnsureLecturersExistAsync(reviewerIds, cancellationToken);

        var round = await store.AcquireRoundAsync(
            session.SemesterId, session.Type, DateOnly.FromDateTime(ReviewWeekStart(sessionDate)), cancellationToken);
        if (round?.Status != ReviewRoundStatus.Closed)
        {
            throw new ConflictException(
                "The review round must remain Closed while Draft assignments are edited.");
        }
        var availableGroups = await store.CountGroupsWithSelectedAvailabilityAsync(
            round.Id, session.GroupIds, dayOfWeek, command.Slot,
            AssignmentRules.RequiredStudentAvailabilityCount, cancellationToken);
        if (availableGroups != session.GroupIds.Count)
        {
            throw new ConflictException(
                "Every assigned group must include the selected date and slot in its five submitted slots.");
        }
        await EnsureReviewersAvailableAsync(round.Id, dayOfWeek, command.Slot, reviewerIds, cancellationToken);
        rules.EnsureLecturerAvailableForReviewSlot(await store.HasReviewerConflictAsync(
            sessionId, sessionDate, command.Slot, reviewerIds, cancellationToken));

        return await store.UpdateSessionAsync(
            sessionId,
            new PersistReviewSessionUpdate(
                command.Code.Trim(), reviewerIds, dayOfWeek, command.Slot, command.Room.Trim(),
                sessionDate, command.Status, DateTime.UtcNow),
            cancellationToken);
    }

    private async Task EnsureLecturersExistAsync(
        IReadOnlyCollection<int> reviewerIds, CancellationToken cancellationToken)
    {
        if (reviewerIds.Count == 0)
        {
            throw new BusinessRuleException("At least one reviewer is required.");
        }
        if (await store.CountExistingLecturersAsync(reviewerIds, cancellationToken) != reviewerIds.Count)
        {
            throw new KeyNotFoundException("One or more reviewers do not exist.");
        }
    }

    private async Task EnsureReviewersAvailableAsync(
        int roundId,
        int dayOfWeek,
        int slot,
        IReadOnlyCollection<int> reviewerIds,
        CancellationToken cancellationToken)
    {
        if (await store.CountAvailableReviewersAsync(
                roundId, dayOfWeek, slot, reviewerIds, cancellationToken) != reviewerIds.Count)
        {
            throw new BusinessRuleException(
                "All reviewers must submit availability for the selected date and slot before scheduling.");
        }
    }

    private static int[] NormalizeReviewers(IReadOnlyCollection<int>? reviewerIds) =>
        reviewerIds?.Distinct().ToArray() ?? [];
    private static DateTime NormalizeSessionDate(DateTime date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
    private static int IsoDayOfWeek(DateTime date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
    private static DateTime ReviewWeekStart(DateTime date) => date.Date.AddDays(1 - IsoDayOfWeek(date));

    private static void ValidateText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException($"{fieldName} is required.");
        }
    }
}

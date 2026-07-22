using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Services;

namespace CPMS.BLL.Reviews.Scheduling;

public sealed class ReviewRoundService(IReviewRoundStore store, AssignmentRules rules) : IReviewRoundService
{
    public async Task<IReadOnlyList<ReviewRoundManagementResult>> GetRoundsAsync(
        int semesterId, CancellationToken cancellationToken) =>
        (await store.GetRoundsAsync(semesterId, cancellationToken)).Select(Map).ToArray();

    public async Task<ReviewRoundManagementResult> CreateRoundAsync(
        int userId,
        bool allowModeratorFallback,
        int semesterId,
        ReviewType reviewType,
        DateOnly? registrationEndDate,
        DateOnly? legacyWeekStartDate,
        CancellationToken cancellationToken)
    {
        if (!await store.SemesterExistsAsync(semesterId, cancellationToken))
        {
            throw new BusinessRuleException("Semester does not exist.");
        }

        var moderatorId = await store.FindModeratorIdAsync(userId, allowModeratorFallback, cancellationToken)
            ?? throw new UnauthorizedAccessException("A Training Department moderator profile is required.");
        DateOnly normalizedStart;
        if (registrationEndDate.HasValue)
        {
            normalizedStart = registrationEndDate.Value.AddDays(5);
            if (normalizedStart.DayOfWeek != DayOfWeek.Monday)
            {
                throw new BusinessRuleException(
                    "Registration must end on Wednesday so the Monday-Saturday review schedule starts exactly five days later.");
            }
            if (legacyWeekStartDate.HasValue && legacyWeekStartDate.Value != normalizedStart)
            {
                throw new BusinessRuleException("Review week must start exactly five days after registration ends.");
            }
        }
        else if (legacyWeekStartDate.HasValue)
        {
            normalizedStart = ReviewWeek.NormalizeStart(legacyWeekStartDate.Value);
        }
        else
        {
            throw new BusinessRuleException("Registration end date is required.");
        }
        if (await store.RoundExistsAsync(semesterId, reviewType, cancellationToken))
        {
            throw new BusinessRuleException("This semester already has a review round of the selected type.");
        }

        ReviewRoundState state = await store.CreateRoundAsync(
            semesterId, reviewType, normalizedStart, normalizedStart.AddDays(5), moderatorId, cancellationToken);
        return Map(state);
    }

    public async Task<ReviewRoundManagementResult> UpdateStatusAsync(
        int roundId, ReviewRoundStatus nextStatus, CancellationToken cancellationToken)
    {
        ReviewRoundState round = await store.FindRoundAsync(roundId, cancellationToken)
            ?? throw new KeyNotFoundException("Review round not found.");
        try
        {
            rules.EnsureReviewRoundTransition(round.Status, nextStatus);
        }
        catch (BusinessRuleException exception)
        {
            throw new ConflictException(exception.Message);
        }

        DateTime? openedAt = round.OpenedAt;
        DateTime? closedAt = round.ClosedAt;
        if (nextStatus == ReviewRoundStatus.Open && openedAt is null)
        {
            openedAt = DateTime.UtcNow;
        }
        if (nextStatus == ReviewRoundStatus.Closed && closedAt is null)
        {
            (int ActiveGroups, int CompletedRegistrations) completion = await store.GetRegistrationCompletionAsync(
                round.Id, round.SemesterId, AssignmentRules.RequiredStudentAvailabilityCount, cancellationToken);
            if (completion.ActiveGroups == 0 || completion.CompletedRegistrations != completion.ActiveGroups)
            {
                throw new ReviewRoundClosureException(
                    "Every active capstone group must submit exactly five slots before registration closes.",
                    completion.ActiveGroups,
                    completion.CompletedRegistrations);
            }
            closedAt = DateTime.UtcNow;
        }

        return Map(await store.UpdateRoundStatusAsync(roundId, nextStatus, openedAt, closedAt, cancellationToken));
    }

    private static ReviewRoundManagementResult Map(ReviewRoundState state) =>
        new(state.Id, state.SemesterId, state.Type, state.Status, state.WeekStartDate.AddDays(-5), state.WeekStartDate, state.WeekEndDate,
            state.CreatedAt, state.OpenedAt, state.ClosedAt, state.RegisteredGroupCount);
}

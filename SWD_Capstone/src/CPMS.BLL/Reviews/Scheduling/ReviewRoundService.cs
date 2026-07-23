using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Services;

namespace CPMS.BLL.Reviews.Scheduling;

public sealed class ReviewRoundService(IReviewRoundStore store) : IReviewRoundService
{
    private const int RegistrationWindowDays = 7;
    private const int DaysBetweenRegistrationAndReview = 5;
    private const int MinimumDaysBetweenReviewWeeks = 28;

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
        SemesterReviewPeriod semester = await store.FindSemesterPeriodAsync(semesterId, cancellationToken)
            ?? throw new BusinessRuleException("Semester does not exist.");

        var moderatorId = await store.FindModeratorIdAsync(userId, allowModeratorFallback, cancellationToken)
            ?? throw new UnauthorizedAccessException("A Training Department moderator profile is required.");
        DateOnly normalizedStart = ResolveWeekStart(registrationEndDate, legacyWeekStartDate);

        DateOnly reviewEnd = normalizedStart.AddDays(5);
        EnsureWithinSemester(semester, normalizedStart, reviewEnd);

        if (await store.RoundExistsAsync(semesterId, reviewType, cancellationToken))
        {
            throw new BusinessRuleException("This semester already has a review round of the selected type.");
        }

        IReadOnlyList<ReviewRoundState> existingRounds =
            await store.GetRoundsAsync(semesterId, cancellationToken);
        EnsureSpacingAndOrder(existingRounds, reviewType, normalizedStart);

        ReviewRoundState state = await store.CreateRoundAsync(
            semesterId, reviewType, normalizedStart, reviewEnd, moderatorId, cancellationToken);
        return Map(state);
    }

    private static DateOnly ResolveWeekStart(
        DateOnly? registrationEndDate,
        DateOnly? legacyWeekStartDate)
    {
        if (!registrationEndDate.HasValue)
        {
            if (legacyWeekStartDate.HasValue)
            {
                return ReviewWeek.NormalizeStart(legacyWeekStartDate.Value);
            }
            throw new BusinessRuleException("Registration end date is required.");
        }

        DateOnly normalizedStart = registrationEndDate.Value.AddDays(DaysBetweenRegistrationAndReview);
        if (normalizedStart.DayOfWeek != DayOfWeek.Monday)
        {
            throw new BusinessRuleException(
                "Registration must end on Wednesday so the Monday-Saturday review schedule starts exactly five days later.");
        }
        if (legacyWeekStartDate.HasValue && legacyWeekStartDate.Value != normalizedStart)
        {
            throw new BusinessRuleException("Review week must start exactly five days after registration ends.");
        }
        return normalizedStart;
    }

    private static void EnsureWithinSemester(
        SemesterReviewPeriod semester,
        DateOnly reviewStart,
        DateOnly reviewEnd)
    {
        DateOnly registrationStart = reviewStart.AddDays(
            -(DaysBetweenRegistrationAndReview + RegistrationWindowDays));
        if (registrationStart < semester.StartDate || reviewEnd > semester.EndDate)
        {
            throw new BusinessRuleException(
                "The registration window and Monday-Saturday review week must be fully within the semester.");
        }
    }

    private static void EnsureSpacingAndOrder(
        IReadOnlyList<ReviewRoundState> existingRounds,
        ReviewType reviewType,
        DateOnly weekStart)
    {
        ReviewRoundState? conflictingRound = existingRounds.FirstOrDefault(existing =>
            Math.Abs(existing.WeekStartDate.DayNumber - weekStart.DayNumber)
                < MinimumDaysBetweenReviewWeeks);
        if (conflictingRound is not null)
        {
            throw new BusinessRuleException(
                $"Review weeks must be at least four weeks apart. " +
                $"{conflictingRound.Type} starts on {conflictingRound.WeekStartDate:yyyy-MM-dd}.");
        }

        int requestedOrder = (int)reviewType;
        bool violatesReviewOrder = existingRounds.Any(existing =>
            ((int)existing.Type < requestedOrder && existing.WeekStartDate >= weekStart)
            || ((int)existing.Type > requestedOrder && existing.WeekStartDate <= weekStart));
        if (violatesReviewOrder)
        {
            throw new BusinessRuleException(
                "Review 1, Review 2 and Review 3 must be scheduled in chronological order.");
        }
    }

    public async Task<ReviewRoundManagementResult> UpdateStatusAsync(
        int roundId, ReviewRoundStatus nextStatus, CancellationToken cancellationToken)
    {
        ReviewRoundState round = await store.FindRoundAsync(roundId, cancellationToken)
            ?? throw new KeyNotFoundException("Review round not found.");
        try
        {
            AssignmentRules.EnsureReviewRoundTransition(round.Status, nextStatus);
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

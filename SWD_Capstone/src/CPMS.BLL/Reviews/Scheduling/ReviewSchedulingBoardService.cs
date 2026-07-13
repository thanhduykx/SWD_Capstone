using CPMS.BLL.Exceptions;
using CPMS.BLL.Enums;

namespace CPMS.BLL.Reviews.Scheduling;

public sealed class ReviewSchedulingBoardService(IReviewSchedulingBoardStore store) : IReviewSchedulingBoardService
{
    public async Task<ReviewSchedulingBoardResult> GetBoardAsync(
        int semesterId,
        ReviewType reviewType,
        DateOnly weekStart,
        CancellationToken cancellationToken)
    {
        if (!await store.SemesterExistsAsync(semesterId, cancellationToken))
        {
            throw new BusinessRuleException("Semester does not exist.");
        }

        var normalizedWeekStart = ReviewWeek.NormalizeStart(weekStart);
        var roundId = await store.FindRoundIdAsync(
            semesterId, reviewType, normalizedWeekStart, cancellationToken)
            ?? throw new KeyNotFoundException("Review round not found.");
        return await store.LoadBoardAsync(
            semesterId, reviewType, normalizedWeekStart, roundId, cancellationToken);
    }

    public Task<IReadOnlyList<StudentSlotRegistrationResult>> GetStudentRegistrationsAsync(
        int roundId, CancellationToken cancellationToken) =>
        store.GetStudentRegistrationsAsync(roundId, cancellationToken);
}

public static class ReviewWeek
{
    public static DateOnly NormalizeStart(DateOnly date)
    {
        var day = (int)date.DayOfWeek;
        return date.AddDays(-(day == 0 ? 6 : day - 1));
    }
}

using CPMS.Core.Enums;

namespace CPMS.BLL.Reviews.Scheduling;

public interface IReviewSchedulingBoardStore
{
    Task<bool> SemesterExistsAsync(int semesterId, CancellationToken cancellationToken);
    Task<int?> FindRoundIdAsync(
        int semesterId, ReviewType reviewType, DateOnly weekStart, CancellationToken cancellationToken);
    Task<ReviewSchedulingBoardResult> LoadBoardAsync(
        int semesterId,
        ReviewType reviewType,
        DateOnly weekStart,
        int roundId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<StudentSlotRegistrationResult>> GetStudentRegistrationsAsync(
        int roundId, CancellationToken cancellationToken);
}

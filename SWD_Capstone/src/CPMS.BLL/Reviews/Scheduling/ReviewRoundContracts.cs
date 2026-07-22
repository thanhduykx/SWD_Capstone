using CPMS.BLL.Enums;

namespace CPMS.BLL.Reviews.Scheduling;

public sealed record ReviewRoundManagementResult(
    int Id,
    int SemesterId,
    ReviewType Type,
    ReviewRoundStatus Status,
    DateOnly RegistrationEndDate,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    DateTime CreatedAt,
    DateTime? OpenedAt,
    DateTime? ClosedAt,
    int RegisteredGroupCount);

public interface IReviewRoundService
{
    Task<IReadOnlyList<ReviewRoundManagementResult>> GetRoundsAsync(int semesterId, CancellationToken cancellationToken);
    Task<ReviewRoundManagementResult> CreateRoundAsync(int userId, bool allowModeratorFallback, int semesterId, ReviewType reviewType, DateOnly? registrationEndDate, DateOnly? legacyWeekStartDate, CancellationToken cancellationToken);
    Task<ReviewRoundManagementResult> UpdateStatusAsync(int roundId, ReviewRoundStatus nextStatus, CancellationToken cancellationToken);
}

public sealed class ReviewRoundClosureException(
    string message,
    int activeGroupCount,
    int completedRegistrationCount) : Exception(message)
{
    public int ActiveGroupCount { get; } = activeGroupCount;
    public int CompletedRegistrationCount { get; } = completedRegistrationCount;
}

using CPMS.BLL.Enums;

namespace CPMS.BLL.Reviews.Scheduling;

public sealed record ReviewRoundState(
    int Id,
    int SemesterId,
    ReviewType Type,
    ReviewRoundStatus Status,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    DateTime CreatedAt,
    DateTime? OpenedAt,
    DateTime? ClosedAt,
    int RegisteredGroupCount);

public interface IReviewRoundStore
{
    Task<IReadOnlyList<ReviewRoundState>> GetRoundsAsync(int semesterId, CancellationToken cancellationToken);
    Task<bool> SemesterExistsAsync(int semesterId, CancellationToken cancellationToken);
    Task<int?> FindModeratorIdAsync(int userId, bool allowFallback, CancellationToken cancellationToken);
    Task<bool> RoundExistsAsync(int semesterId, ReviewType reviewType, CancellationToken cancellationToken);
    Task<ReviewRoundState> CreateRoundAsync(int semesterId, ReviewType reviewType, DateOnly weekStartDate, DateOnly weekEndDate, int moderatorId, CancellationToken cancellationToken);
    Task<ReviewRoundState?> FindRoundAsync(int roundId, CancellationToken cancellationToken);
    Task<(int ActiveGroups, int CompletedRegistrations)> GetRegistrationCompletionAsync(int roundId, int semesterId, int requiredSlotCount, CancellationToken cancellationToken);
    Task<ReviewRoundState> UpdateRoundStatusAsync(int roundId, ReviewRoundStatus status, DateTime? openedAt, DateTime? closedAt, CancellationToken cancellationToken);
}

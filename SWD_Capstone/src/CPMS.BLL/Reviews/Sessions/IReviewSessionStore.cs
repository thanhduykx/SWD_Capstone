using CPMS.Core.Enums;

namespace CPMS.BLL.Reviews.Sessions;

public sealed record ReviewGroupState(int Id, int SemesterId, int SupervisorId);
public sealed record ReviewRoundEditState(int Id, ReviewRoundStatus Status);
public sealed record ReviewSessionEditState(
    int Id,
    int SemesterId,
    ReviewType Type,
    ReviewSessionStatus Status,
    IReadOnlyList<int> GroupIds,
    IReadOnlyList<int> SupervisorIds);
public sealed record PersistReviewSession(
    string Code,
    int SemesterId,
    int GroupId,
    ReviewType Type,
    IReadOnlyList<int> ReviewerIds,
    int DayOfWeek,
    int Slot,
    string Room,
    DateTime SessionDate,
    DateTime ChangedAt);
public sealed record PersistReviewSessionUpdate(
    string Code,
    IReadOnlyList<int> ReviewerIds,
    int DayOfWeek,
    int Slot,
    string Room,
    DateTime SessionDate,
    ReviewSessionStatus Status,
    DateTime ChangedAt);

public interface IReviewSessionStore
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken);
    Task<ReviewGroupState?> FindGroupAsync(int groupId, CancellationToken cancellationToken);
    Task<ReviewSessionEditState?> FindSessionForEditAsync(int sessionId, CancellationToken cancellationToken);
    Task<ReviewRoundEditState?> AcquireRoundAsync(
        int semesterId, ReviewType reviewType, DateOnly weekStart, CancellationToken cancellationToken);
    Task<int> CountExistingLecturersAsync(
        IReadOnlyCollection<int> reviewerIds, CancellationToken cancellationToken);
    Task<int> CountAvailableReviewersAsync(
        int roundId,
        int dayOfWeek,
        int slot,
        IReadOnlyCollection<int> reviewerIds,
        CancellationToken cancellationToken);
    Task<int> CountGroupsWithSelectedAvailabilityAsync(
        int roundId,
        IReadOnlyCollection<int> groupIds,
        int dayOfWeek,
        int slot,
        int requiredSlotCount,
        CancellationToken cancellationToken);
    Task<bool> IsGroupScheduledAsync(
        int groupId,
        ReviewType reviewType,
        DateTime weekStart,
        DateTime weekEnd,
        CancellationToken cancellationToken);
    Task<bool> HasReviewerConflictAsync(
        int? excludedSessionId,
        DateTime sessionDate,
        int slot,
        IReadOnlyCollection<int> reviewerIds,
        CancellationToken cancellationToken);
    Task<ReviewSessionResult> CreateSessionAsync(
        PersistReviewSession session, CancellationToken cancellationToken);
    Task<ReviewSessionResult> UpdateSessionAsync(
        int sessionId, PersistReviewSessionUpdate update, CancellationToken cancellationToken);
    Task<int?> FindLecturerIdByUserIdAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MyReviewSessionResult>> GetLecturerSessionsAsync(
        int lecturerId, CancellationToken cancellationToken);
}

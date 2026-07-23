using CPMS.BLL.Enums;

namespace CPMS.BLL.Reviews.Sessions;

public sealed record AssignReviewSessionCommand(
    string Code,
    int GroupId,
    ReviewType Type,
    IReadOnlyCollection<int> ReviewerIds,
    IReadOnlyCollection<int>? PreviousReviewerIds,
    int Slot,
    string Room,
    DateTime SessionDate);

public sealed record UpdateReviewSessionCommand(
    string Code,
    IReadOnlyCollection<int> ReviewerIds,
    IReadOnlyCollection<int>? PreviousReviewerIds,
    int Slot,
    string Room,
    DateTime SessionDate,
    ReviewSessionStatus Status);

public sealed record ReviewSessionResult(
    int Id,
    string Code,
    int SemesterId,
    IReadOnlyList<ReviewSessionGroupResult> Groups,
    ReviewType Type,
    ReviewSessionStatus Status,
    IReadOnlyList<int> ReviewerIds,
    DateTime SessionDate,
    int Slot,
    string Room);
public sealed record ReviewSessionGroupResult(
    int GroupId, string GroupCode, int GroupPosition, ReviewGroupStatus Status, DateTime? CompletedAt);
public sealed record MyReviewSessionResult(
    int SessionId,
    int SubmissionId,
    string Code,
    ReviewType Type,
    ReviewSessionStatus SessionStatus,
    int GroupId,
    string GroupCode,
    string? TopicName,
    DateTime SessionDate,
    int Slot,
    string Room,
    int ReviewerCount,
    ReviewSubmissionStatus SubmissionStatus,
    DateTime LastSavedAt,
    bool HasAccessCode,
    bool IsAccessVerified);

public interface IReviewSessionService
{
    Task<ReviewSessionResult> CreateAsync(AssignReviewSessionCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewSessionResult>> BulkAssignAsync(
        IReadOnlyCollection<AssignReviewSessionCommand> commands, CancellationToken cancellationToken);
    Task<ReviewSessionResult> UpdateAsync(
        int sessionId, UpdateReviewSessionCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<MyReviewSessionResult>> GetLecturerSessionsAsync(
        int userId, CancellationToken cancellationToken);
}

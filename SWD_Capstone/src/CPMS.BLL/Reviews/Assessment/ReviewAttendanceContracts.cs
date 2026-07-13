using CPMS.BLL.Enums;

namespace CPMS.BLL.Reviews.Assessment;

public sealed record ReviewActor(int UserId, UserRole Role);
public sealed record AttendanceEntry(int StudentId, bool IsPresent, string? Note);
public sealed record AttendanceStudent(int StudentId, string StudentCode, string FullName, bool? IsPresent, string? Note);
public sealed record ReviewAttendanceResult(
    int SessionId,
    string SessionCode,
    int GroupId,
    string GroupCode,
    DateTime SessionDate,
    int Slot,
    string Room,
    IReadOnlyList<AttendanceStudent> Students);
public sealed record ReviewCommentResult(
    int Id, int AuthorUserId, string AuthorName, string Content, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record CompleteReviewResult(
    int SessionId, int GroupId, ReviewGroupStatus Status, DateTime? CompletedAt);

public interface IReviewAttendanceService
{
    Task<ReviewAttendanceResult> GetAttendanceAsync(
        int sessionId, int? groupId, ReviewActor actor, CancellationToken cancellationToken);
    Task<ReviewAttendanceResult> SubmitAttendanceAsync(
        int sessionId,
        int groupId,
        IReadOnlyCollection<AttendanceEntry> entries,
        ReviewActor actor,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewCommentResult>> GetCommentsAsync(
        int sessionId, int? groupId, ReviewActor actor, CancellationToken cancellationToken);
    Task<ReviewCommentResult> AddCommentAsync(
        int sessionId, int groupId, string? content, ReviewActor actor, CancellationToken cancellationToken);
    Task<CompleteReviewResult> CompleteAsync(
        int sessionId, int groupId, ReviewActor actor, CancellationToken cancellationToken);
}

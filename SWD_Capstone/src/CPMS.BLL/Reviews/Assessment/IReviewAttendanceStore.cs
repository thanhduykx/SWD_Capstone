using CPMS.BLL.Enums;

namespace CPMS.BLL.Reviews.Assessment;

public sealed record ReviewAttendanceContext(
    int SessionId,
    string SessionCode,
    ReviewSessionStatus SessionStatus,
    IReadOnlyList<int> ReviewerIds,
    DateTime SessionDate,
    int Slot,
    string Room,
    int GroupId,
    string GroupCode,
    ReviewGroupStatus GroupStatus,
    DateTime? CompletedAt,
    int? LecturerId,
    int? StudentGroupId);
public sealed record ReviewAttendanceContextLookup(bool SessionExists, ReviewAttendanceContext? Context);

public interface IReviewAttendanceStore
{
    Task<ReviewAttendanceContextLookup> LoadContextAsync(
        int sessionId, int? groupId, int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AttendanceStudent>> GetStudentsAsync(
        int sessionId, int groupId, CancellationToken cancellationToken);
    Task<IReadOnlySet<int>> GetStudentIdsAsync(int groupId, CancellationToken cancellationToken);
    Task SaveAttendanceAsync(
        int sessionId,
        int lecturerId,
        IReadOnlyCollection<AttendanceEntry> entries,
        DateTime markedAt,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewCommentResult>> GetCommentsAsync(
        int sessionId, int groupId, CancellationToken cancellationToken);
    Task<ReviewCommentResult> AddCommentAsync(
        int sessionId,
        int groupId,
        int authorUserId,
        string content,
        CancellationToken cancellationToken);
    Task<(int StudentCount, int AttendanceCount, int SubmittedCount, int ConfirmationCount)> GetCompletionCountsAsync(
        int sessionId, int groupId, CancellationToken cancellationToken);
    Task<CompleteReviewResult> CompleteAsync(
        int sessionId, int groupId, int lecturerId, DateTime completedAt, CancellationToken cancellationToken);
}

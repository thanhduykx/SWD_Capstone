namespace CPMS.BLL.Reviews.Access;

public sealed record GeneratedReviewSessionAccessCode(
    int SessionId,
    int GroupId,
    string AccessCode,
    int CodeVersion,
    DateTime GeneratedAt);

public sealed record VerifiedReviewSessionAccess(
    int SessionId,
    int GroupId,
    bool IsVerified,
    DateTime VerifiedAt);

public interface IReviewSessionAccessService
{
    Task<GeneratedReviewSessionAccessCode> GenerateAsync(
        int sessionId,
        int groupId,
        int generatedByUserId,
        CancellationToken cancellationToken);

    Task<VerifiedReviewSessionAccess> VerifyAsync(
        int sessionId,
        int groupId,
        int lecturerUserId,
        string accessCode,
        CancellationToken cancellationToken);

    Task EnsureLecturerAccessAsync(
        int sessionId,
        int groupId,
        int lecturerUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tương thích FE/API cũ: tạo cùng một mã cho mọi nhóm trong session.
    /// </summary>
    Task<GeneratedReviewSessionAccessCode> GenerateForSessionAsync(
        int sessionId,
        int generatedByUserId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tương thích FE/API cũ: verify theo session — resolve nhóm GV được phân công.
    /// </summary>
    Task<VerifiedReviewSessionAccess> VerifyForSessionAsync(
        int sessionId,
        int lecturerUserId,
        string accessCode,
        CancellationToken cancellationToken);
}

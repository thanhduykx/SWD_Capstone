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
}

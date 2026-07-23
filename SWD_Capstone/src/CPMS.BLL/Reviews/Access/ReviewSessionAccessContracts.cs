namespace CPMS.BLL.Reviews.Access;

public sealed record GeneratedReviewSessionAccessCode(
    int SessionId,
    string AccessCode,
    int CodeVersion,
    DateTime GeneratedAt);

public sealed record VerifiedReviewSessionAccess(
    int SessionId,
    bool IsVerified,
    DateTime VerifiedAt);

public interface IReviewSessionAccessService
{
    Task<GeneratedReviewSessionAccessCode> GenerateAsync(
        int sessionId,
        int generatedByUserId,
        CancellationToken cancellationToken);

    Task<VerifiedReviewSessionAccess> VerifyAsync(
        int sessionId,
        int lecturerUserId,
        string accessCode,
        CancellationToken cancellationToken);

    Task EnsureLecturerAccessAsync(
        int sessionId,
        int lecturerUserId,
        CancellationToken cancellationToken);
}

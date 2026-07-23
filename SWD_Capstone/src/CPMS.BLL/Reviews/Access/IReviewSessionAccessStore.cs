namespace CPMS.BLL.Reviews.Access;

public sealed record ReviewSessionAccessState(
    int SessionId,
    string? AccessCodeHash,
    int CodeVersion);

public interface IReviewSessionAccessStore
{
    Task<ReviewSessionAccessState?> FindSessionAsync(
        int sessionId,
        CancellationToken cancellationToken);

    Task<int?> FindAssignedLecturerAsync(
        int sessionId,
        int lecturerUserId,
        CancellationToken cancellationToken);

    Task SaveAccessCodeAsync(
        int sessionId,
        string accessCodeHash,
        int codeVersion,
        int generatedByUserId,
        DateTime generatedAt,
        CancellationToken cancellationToken);

    Task SaveGrantAsync(
        int sessionId,
        int lecturerId,
        int codeVersion,
        DateTime verifiedAt,
        CancellationToken cancellationToken);

    Task<bool> HasCurrentGrantAsync(
        int sessionId,
        int lecturerId,
        int codeVersion,
        CancellationToken cancellationToken);
}

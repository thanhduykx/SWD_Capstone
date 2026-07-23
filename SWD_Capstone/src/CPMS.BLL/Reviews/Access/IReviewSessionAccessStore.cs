namespace CPMS.BLL.Reviews.Access;

public sealed record ReviewSessionAccessState(
    int SessionId,
    int GroupId,
    string? AccessCodeHash,
    int CodeVersion);

public interface IReviewSessionAccessStore
{
    Task<ReviewSessionAccessState?> FindGroupAsync(
        int sessionId,
        int groupId,
        CancellationToken cancellationToken);

    Task<int?> FindAssignedLecturerAsync(
        int sessionId,
        int groupId,
        int lecturerUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> FindGroupIdsInSessionAsync(
        int sessionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<int>> FindAssignedGroupIdsAsync(
        int sessionId,
        int lecturerUserId,
        CancellationToken cancellationToken);

    Task SaveAccessCodeAsync(
        int sessionId,
        int groupId,
        string accessCodeHash,
        int codeVersion,
        int generatedByUserId,
        DateTime generatedAt,
        CancellationToken cancellationToken);

    Task SaveGrantAsync(
        int sessionId,
        int groupId,
        int lecturerId,
        int codeVersion,
        DateTime verifiedAt,
        CancellationToken cancellationToken);

    Task<bool> HasCurrentGrantAsync(
        int sessionId,
        int groupId,
        int lecturerId,
        int codeVersion,
        CancellationToken cancellationToken);
}

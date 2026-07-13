using CPMS.Core.Enums;

namespace CPMS.BLL.Reviews.Publication;

public sealed record ReviewPublicationScope(int SemesterId, ReviewType ReviewType, DateOnly WeekStart);
public sealed record PublicationSession(
    int Id, string Code, ReviewType Type, DateTime SessionDate, int Slot, string Room, IReadOnlyList<int> ReviewerIds);
public sealed record PublicationRecipient(
    int UserId,
    string Email,
    string DisplayName,
    string? GroupCode,
    string? ProjectName,
    IReadOnlyList<int> SessionIds);
public sealed record ReviewPublicationSnapshot(
    int? RoundId,
    ReviewRoundStatus? RoundStatus,
    bool AlreadyPublished,
    IReadOnlyList<PublicationSession> Sessions,
    int ActiveGroupCount,
    int FullyRegisteredGroupCount,
    int ScheduledGroupCount,
    IReadOnlyList<PublicationRecipient> ReviewerRecipients,
    IReadOnlyList<PublicationRecipient> GroupLeaderRecipients);
public sealed record PublicationEmail(int RecipientUserId, string RecipientEmail, string Subject, string Body);
public sealed record PersistReviewPublication(
    int RoundId,
    int ModeratorId,
    string Subject,
    string Body,
    IReadOnlyList<int> SessionIds,
    IReadOnlyList<PublicationEmail> Emails,
    int UserId,
    string? IpAddress,
    string? UserAgent,
    DateTime PublishedAt);

public interface IReviewPublicationStore
{
    Task<int?> FindModeratorIdAsync(int userId, bool allowFallback, CancellationToken cancellationToken);
    Task<TResult> ExecuteLockedAsync<TResult>(
        ReviewPublicationScope scope,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
    Task<ReviewPublicationSnapshot> LoadSnapshotAsync(
        ReviewPublicationScope scope, int requiredSlotCount, CancellationToken cancellationToken);
    Task<int> PersistAsync(
        ReviewPublicationScope scope,
        PersistReviewPublication publication,
        CancellationToken cancellationToken);
    Task<(int Sent, int Failed)> DispatchEmailsAsync(
        int publicationId, CancellationToken cancellationToken);
}

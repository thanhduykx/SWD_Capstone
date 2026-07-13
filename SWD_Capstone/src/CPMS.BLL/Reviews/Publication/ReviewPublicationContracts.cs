using CPMS.Core.Enums;

namespace CPMS.BLL.Reviews.Publication;

public sealed record PublishReviewScheduleCommand(
    int UserId,
    bool AllowModeratorFallback,
    int SemesterId,
    ReviewType ReviewType,
    DateOnly WeekStart,
    string? Subject,
    string? Message,
    string? IpAddress,
    string? UserAgent);
public sealed record ReviewSchedulePublicationResult(
    int PublicationId, int PublishedSessionCount, int SentEmailCount, int FailedEmailCount);
public sealed class IncompleteReviewScheduleException(
    string message,
    int activeGroups,
    int fullyRegisteredGroups,
    int scheduledGroups) : Exception(message)
{
    public int ActiveGroups { get; } = activeGroups;
    public int FullyRegisteredGroups { get; } = fullyRegisteredGroups;
    public int ScheduledGroups { get; } = scheduledGroups;
}
public interface IReviewPublicationService
{
    Task<ReviewSchedulePublicationResult> PublishAsync(
        PublishReviewScheduleCommand command, CancellationToken cancellationToken);
}

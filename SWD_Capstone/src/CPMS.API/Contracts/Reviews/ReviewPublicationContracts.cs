using CPMS.Core.Enums;

namespace CPMS.API.Contracts.Reviews;

public sealed record PublishReviewScheduleRequest(
    int SemesterId, ReviewType ReviewType, DateOnly WeekStart, string? Subject, string? Message);
public sealed record ReviewSchedulePublishResponse(
    int PublicationId, int PublishedSessionCount, int SentEmailCount, int FailedEmailCount);

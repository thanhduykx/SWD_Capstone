using CPMS.BLL.Enums;

namespace CPMS.API.Contracts.Reviews;

public sealed record PublishReviewScheduleRequest(
    [property: JsonRequired] int SemesterId,
    [property: JsonRequired] ReviewType ReviewType,
    [property: JsonRequired] DateOnly WeekStart,
    string? Subject,
    string? Message);
public sealed record ReviewSchedulePublishResponse(
    int PublicationId, int PublishedSessionCount, int SentEmailCount, int FailedEmailCount);

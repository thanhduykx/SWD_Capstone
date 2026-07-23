using CPMS.BLL.Enums;

namespace CPMS.API.Contracts.Reviews;

public sealed record CreateReviewSessionRequest(
    string Code,
    [property: JsonRequired] int GroupId,
    [property: JsonRequired] int GroupPosition,
    [property: JsonRequired] ReviewType Type,
    [property: JsonRequired] IReadOnlyCollection<int> ReviewerIds,
    int[] PreviousReviewerIds,
    [property: JsonRequired] int Slot,
    string Room,
    [property: JsonRequired] DateTime SessionDate);
public sealed record BulkAssignReviewSessionsRequest(IReadOnlyCollection<BulkAssignReviewSessionRequest> Sessions);
public sealed record BulkAssignReviewSessionsResponse(
    int SentEmailCount, int FailedEmailCount, IReadOnlyCollection<ReviewSessionResponse> Sessions);
public sealed record BulkAssignReviewSessionRequest(
    string Code,
    [property: JsonRequired] int GroupId,
    [property: JsonRequired] int GroupPosition,
    [property: JsonRequired] ReviewType Type,
    IReadOnlyCollection<int> ReviewerIds,
    IReadOnlyCollection<int> PreviousReviewerIds,
    [property: JsonRequired] int Slot,
    string Room,
    [property: JsonRequired] DateTime SessionDate);
public sealed record UpdateReviewSessionRequest(
    string Code,
    IReadOnlyCollection<int> ReviewerIds,
    IReadOnlyCollection<int> PreviousReviewerIds,
    [property: JsonRequired] int Slot,
    string Room,
    [property: JsonRequired] DateTime SessionDate,
    [property: JsonRequired] ReviewSessionStatus Status);
public sealed record ReviewSessionResponse(
    int Id,
    string Code,
    int SemesterId,
    IReadOnlyCollection<ReviewSessionGroupResponse> Groups,
    ReviewType Type,
    ReviewSessionStatus Status,
    IReadOnlyCollection<int> ReviewerIds,
    DateTime SessionDate,
    int Slot,
    string Room);
public sealed record ReviewSessionGroupResponse(
    int GroupId, string GroupCode, int GroupPosition, ReviewGroupStatus Status, DateTime? CompletedAt);
public sealed record MyReviewSessionResponse(
    int SessionId,
    int SubmissionId,
    string Code,
    ReviewType Type,
    ReviewSessionStatus SessionStatus,
    int GroupId,
    string GroupCode,
    string? TopicName,
    DateTime SessionDate,
    int Slot,
    string Room,
    int ReviewerCount,
    ReviewSubmissionStatus SubmissionStatus,
    DateTime LastSavedAt,
    bool HasAccessCode,
    bool IsAccessVerified);

public sealed record VerifyReviewSessionAccessCodeRequest(string AccessCode);
public sealed record GeneratedReviewSessionAccessCodeResponse(
    int SessionId,
    int GroupId,
    string AccessCode,
    int CodeVersion,
    DateTime GeneratedAt);
public sealed record VerifiedReviewSessionAccessResponse(
    int SessionId,
    int GroupId,
    bool IsVerified,
    DateTime VerifiedAt);

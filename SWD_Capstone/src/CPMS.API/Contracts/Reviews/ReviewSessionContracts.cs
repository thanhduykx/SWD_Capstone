using CPMS.BLL.Enums;

namespace CPMS.API.Contracts.Reviews;

public sealed record CreateReviewSessionRequest(
    string Code,
    int GroupId,
    int GroupPosition,
    ReviewType Type,
    int Reviewer1Id,
    int Reviewer2Id,
    int[] PreviousReviewerIds,
    int Slot,
    string Room,
    DateTime SessionDate);
public sealed record BulkAssignReviewSessionsRequest(IReadOnlyCollection<BulkAssignReviewSessionRequest> Sessions);
public sealed record BulkAssignReviewSessionsResponse(
    int SentEmailCount, int FailedEmailCount, IReadOnlyCollection<ReviewSessionResponse> Sessions);
public sealed record BulkAssignReviewSessionRequest(
    string Code,
    int GroupId,
    int GroupPosition,
    ReviewType Type,
    IReadOnlyCollection<int> ReviewerIds,
    IReadOnlyCollection<int> PreviousReviewerIds,
    int Slot,
    string Room,
    DateTime SessionDate);
public sealed record UpdateReviewSessionRequest(
    string Code,
    IReadOnlyCollection<int> ReviewerIds,
    IReadOnlyCollection<int> PreviousReviewerIds,
    int Slot,
    string Room,
    DateTime SessionDate,
    ReviewSessionStatus Status);
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
    ReviewSubmissionStatus SubmissionStatus,
    DateTime LastSavedAt);

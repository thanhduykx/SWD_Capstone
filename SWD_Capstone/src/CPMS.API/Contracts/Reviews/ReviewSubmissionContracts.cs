using CPMS.BLL.Enums;

namespace CPMS.API.Contracts.Reviews;

public sealed record SaveReviewSubmissionDraftRequest(
    string? WorkProductVersion,
    string? WorkProductSize,
    decimal? EffortHours,
    string? ReviewerComment,
    IReadOnlyCollection<string>? ReviewerComments,
    string? Suggestion,
    string? ResultText,
    IReadOnlyCollection<SaveReviewSubmissionItemRequest> Items);
public sealed record SaveReviewSubmissionItemRequest(
    string ItemKey, ReviewChecklistAnswer? Answer, string? Comment);
public sealed record ReviewSubmissionResponse(
    int Id,
    int SessionId,
    int GroupId,
    string GroupCode,
    string ProjectName,
    ReviewType Type,
    ReviewSubmissionStatus Status,
    ReviewSessionStatus SessionStatus,
    DateTime SessionDate,
    int Slot,
    string Room,
    int ReviewerId,
    string ReviewerCode,
    string ReviewerName,
    string? WorkProductVersion,
    string? WorkProductSize,
    decimal? EffortHours,
    string? ReviewerComment,
    IReadOnlyCollection<string> ReviewerComments,
    string? Suggestion,
    string? ResultText,
    DateTime LastSavedAt,
    DateTime? SubmittedAt,
    IReadOnlyCollection<ReviewSubmissionItemResponse> Items);
public sealed record ReviewSubmissionItemResponse(
    string ItemKey,
    string Label,
    string? Description,
    string? Priority,
    bool IsSection,
    string? CriteriaCode,
    ReviewChecklistAnswer? Answer,
    string? Comment);

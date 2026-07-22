using CPMS.BLL.Enums;

namespace CPMS.BLL.Reviews.Assessment;

public sealed record SaveReviewDraft(
    string? WorkProductVersion,
    string? WorkProductSize,
    decimal? EffortHours,
    string? ReviewerComment,
    IReadOnlyCollection<string>? ReviewerComments,
    string? Suggestion,
    string? ResultText,
    IReadOnlyCollection<SaveReviewItem> Items);
public sealed record SaveReviewItem(string ItemKey, ReviewChecklistAnswer? Answer, string? Comment);
public sealed record ReviewSubmissionItemResult(
    string ItemKey,
    string Label,
    string? Description,
    string? Priority,
    bool IsSection,
    string? CriteriaCode,
    ReviewChecklistAnswer? Answer,
    string? Comment);
public sealed record ReviewSubmissionResult(
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
    IReadOnlyList<string> ReviewerComments,
    string? Suggestion,
    string? ResultText,
    DateTime LastSavedAt,
    DateTime? SubmittedAt,
    IReadOnlyList<ReviewSubmissionItemResult> Items);
public sealed record ReviewSubmissionSummary(
    int Id,
    int SessionId,
    int GroupId,
    string ReviewType,
    string Status,
    string ReviewerName,
    string Notes,
    DateTime SubmittedAt);
public sealed record ScheduledReviewExport(int SessionId, int GroupId);

public interface IReviewSubmissionService
{
    Task<IReadOnlyList<ReviewSubmissionSummary>> GetMySubmissionsAsync(
        ReviewActor actor, CancellationToken cancellationToken);
    Task<ReviewSubmissionResult> GetAsync(
        int submissionId, ReviewActor actor, CancellationToken cancellationToken);
    Task<ReviewSubmissionResult> SaveDraftAsync(
        int submissionId, SaveReviewDraft draft, ReviewActor actor, CancellationToken cancellationToken);
    Task<ReviewSubmissionResult> SubmitAsync(
        int submissionId, ReviewActor actor, CancellationToken cancellationToken);
    Task<ReviewChecklistExportData> GetExportDataAsync(
        int submissionId, ReviewActor actor, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewChecklistExportData>> GetBulkExportDataAsync(
        int semesterId, ReviewType reviewType, CancellationToken cancellationToken);
}

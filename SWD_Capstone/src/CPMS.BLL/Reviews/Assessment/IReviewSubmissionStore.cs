using CPMS.Core.Enums;

namespace CPMS.BLL.Reviews.Assessment;

public sealed record ReviewSubmissionState(
    int Id,
    int SessionId,
    int GroupId,
    int ReviewerId,
    ReviewType Type,
    ReviewSubmissionStatus Status,
    ReviewSessionStatus SessionStatus,
    bool GroupCompleted,
    string? ReviewerComment);
public sealed record ReviewSubmissionData(
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
    string? Suggestion,
    string? ResultText,
    DateTime LastSavedAt,
    DateTime? SubmittedAt,
    IReadOnlyDictionary<string, (ReviewChecklistAnswer? Answer, string? Comment)> ItemResponses);
public sealed record PersistReviewDraft(
    string? WorkProductVersion,
    string? WorkProductSize,
    decimal? EffortHours,
    string? ReviewerComment,
    string? Suggestion,
    string? ResultText,
    IReadOnlyCollection<SaveReviewItem> Items,
    DateTime ChangedAt);

public interface IReviewSubmissionStore
{
    Task<int?> FindLecturerIdAsync(int userId, CancellationToken cancellationToken);
    Task<int?> FindStudentGroupIdAsync(int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewSubmissionSummary>> GetLecturerSummariesAsync(
        int lecturerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReviewSubmissionSummary>> GetStudentSummariesAsync(
        int groupId, CancellationToken cancellationToken);
    Task<ReviewSubmissionState?> FindStateAsync(int submissionId, CancellationToken cancellationToken);
    Task<ReviewSubmissionData> GetDataAsync(int submissionId, CancellationToken cancellationToken);
    Task SaveDraftAsync(int submissionId, PersistReviewDraft draft, CancellationToken cancellationToken);
    Task<int> CountAnsweredItemsAsync(
        int submissionId, IReadOnlyCollection<string> requiredKeys, CancellationToken cancellationToken);
    Task SubmitAsync(int submissionId, DateTime submittedAt, CancellationToken cancellationToken);
    Task<ReviewChecklistExportData> GetExportDataAsync(
        int sessionId, int groupId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScheduledReviewExport>> GetScheduledExportsAsync(
        int semesterId, ReviewType reviewType, CancellationToken cancellationToken);
}

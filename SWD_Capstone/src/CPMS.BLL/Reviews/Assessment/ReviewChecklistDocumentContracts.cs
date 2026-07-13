using CPMS.Core.Enums;

namespace CPMS.BLL.Reviews.Assessment;

public interface IReviewChecklistTemplateProvider
{
    IReadOnlyList<ReviewChecklistTemplateItem> GetItems(ReviewType type);
}
public sealed record ReviewChecklistTemplateItem(
    string ItemKey,
    string Label,
    string? Description,
    string? Priority,
    int RowNumber,
    bool IsSection,
    string? CriteriaCode);
public sealed record ReviewChecklistExportData(
    int SessionId,
    ReviewType Type,
    string GroupCode,
    string ProjectName,
    DateTime ReviewDate,
    string? WorkProductVersion,
    string? WorkProductSize,
    IReadOnlyList<ReviewChecklistExportReviewer> Reviewers);
public sealed record ReviewChecklistExportReviewer(
    int ReviewerId,
    string ReviewerCode,
    string ReviewerName,
    decimal? EffortHours,
    string? ReviewerComment,
    string? Suggestion,
    IReadOnlyList<ReviewChecklistExportItem> Items);
public sealed record ReviewChecklistExportItem(
    string ItemKey, ReviewChecklistAnswer? Answer, string? Comment);

using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Reviews.Access;

namespace CPMS.BLL.Reviews.Assessment;

public sealed class ReviewSubmissionService(
    IReviewSubmissionStore store,
    IReviewChecklistTemplateProvider templateProvider,
    IReviewSessionAccessService sessionAccessService) : IReviewSubmissionService
{
    public async Task<IReadOnlyList<ReviewSubmissionSummary>> GetMySubmissionsAsync(
        ReviewActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Role == UserRole.Lecturer)
        {
            var lecturerId = await store.FindLecturerIdAsync(actor.UserId, cancellationToken);
            return lecturerId.HasValue
                ? await store.GetLecturerSummariesAsync(lecturerId.Value, cancellationToken) : [];
        }
        if (actor.Role == UserRole.Student)
        {
            var groupId = await store.FindStudentGroupIdAsync(actor.UserId, cancellationToken);
            return groupId.HasValue
                ? await store.GetStudentSummariesAsync(groupId.Value, cancellationToken) : [];
        }
        return [];
    }

    public async Task<ReviewSubmissionResult> GetAsync(
        int submissionId, ReviewActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ReviewSubmissionState state = await RequireStateAsync(submissionId, cancellationToken);
        await EnsureCanViewAsync(state, actor, cancellationToken);
        return Map(await store.GetDataAsync(submissionId, cancellationToken));
    }

    public async Task<IReadOnlyList<PreviousReviewFeedback>> GetPreviousHistoryAsync(
        int submissionId, ReviewActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ReviewSubmissionState state = await RequireStateAsync(submissionId, cancellationToken);
        await EnsureCanViewAsync(state, actor, cancellationToken);
        return await store.GetPreviousHistoryAsync(
            state.GroupId, state.Type, cancellationToken);
    }

    public async Task<ReviewSubmissionResult> SaveDraftAsync(
        int submissionId, SaveReviewDraft draft, ReviewActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(draft.Items);
        ArgumentNullException.ThrowIfNull(actor);
        ReviewSubmissionState state = await RequireStateAsync(submissionId, cancellationToken);
        await EnsureCanEditAsync(state, actor, cancellationToken);
        if (state.GroupCompleted)
            return Map(await store.GetDataAsync(submissionId, cancellationToken));
        if (state.SessionStatus != ReviewSessionStatus.Published)
            throw new ConflictException("Review feedback can only be edited after the schedule is published.");
        var allowedKeys = templateProvider.GetItems(state.Type).Where(x => !x.IsSection)
            .Select(x => x.ItemKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        SaveReviewItem? invalidItem = draft.Items.FirstOrDefault(item => !allowedKeys.Contains(item.ItemKey));
        if (invalidItem is not null)
        {
            throw new BusinessRuleException(
                $"Checklist item '{invalidItem.ItemKey}' does not belong to this review template.");
        }
        string[] reviewerComments = NormalizeComments(draft.ReviewerComments, draft.ReviewerComment);
        if (reviewerComments.Length > 50)
            throw new BusinessRuleException("A review submission can contain at most 50 comments.");
        if (reviewerComments.Any(comment => comment.Length > 4000))
            throw new BusinessRuleException("Each reviewer comment must not exceed 4000 characters.");
        await store.SaveDraftAsync(
            submissionId,
            new PersistReviewDraft(
                EmptyToNull(draft.WorkProductVersion), EmptyToNull(draft.WorkProductSize), draft.EffortHours,
                reviewerComments.FirstOrDefault(), reviewerComments, EmptyToNull(draft.Suggestion), EmptyToNull(draft.ResultText),
                draft.Items.Select(x => x with { Comment = EmptyToNull(x.Comment) }).ToArray(), DateTime.UtcNow),
            cancellationToken);
        return Map(await store.GetDataAsync(submissionId, cancellationToken));
    }

    public async Task<ReviewSubmissionResult> SubmitAsync(
        int submissionId, ReviewActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ReviewSubmissionState state = await RequireStateAsync(submissionId, cancellationToken);
        await EnsureCanEditAsync(state, actor, cancellationToken);
        if (state.GroupCompleted || state.Status == ReviewSubmissionStatus.Submitted)
            return Map(await store.GetDataAsync(submissionId, cancellationToken));
        if (state.SessionStatus != ReviewSessionStatus.Published)
            throw new ConflictException("Review feedback can only be submitted for a published session.");
        if (string.IsNullOrWhiteSpace(state.ReviewerComment))
            throw new ConflictException("Reviewer comment is required before completing the review.");
        await store.SubmitAsync(submissionId, DateTime.UtcNow, cancellationToken);
        return Map(await store.GetDataAsync(submissionId, cancellationToken));
    }

    public async Task<ReviewChecklistExportData> GetExportDataAsync(
        int submissionId, ReviewActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ReviewSubmissionState state = await RequireStateAsync(submissionId, cancellationToken);
        await EnsureCanViewAsync(state, actor, cancellationToken);
        return await store.GetExportDataAsync(state.SessionId, state.GroupId, cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewChecklistExportData>> GetBulkExportDataAsync(
        int semesterId, ReviewType reviewType, CancellationToken cancellationToken)
    {
        IReadOnlyList<ScheduledReviewExport> scheduled = await store.GetScheduledExportsAsync(semesterId, reviewType, cancellationToken);
        if (scheduled.Count == 0) throw new KeyNotFoundException("No review sessions were found for export.");
        var results = new List<ReviewChecklistExportData>(scheduled.Count);
        foreach (ScheduledReviewExport item in scheduled)
            results.Add(await store.GetExportDataAsync(item.SessionId, item.GroupId, cancellationToken));
        return results;
    }

    private async Task<ReviewSubmissionState> RequireStateAsync(int id, CancellationToken cancellationToken) =>
        await store.FindStateAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Review submission not found.");

    private async Task EnsureCanViewAsync(
        ReviewSubmissionState state, ReviewActor actor, CancellationToken cancellationToken)
    {
        if (actor.Role is UserRole.SystemAdministrator or UserRole.TrainingDepartment) return;
        if (actor.Role == UserRole.Lecturer &&
            await store.FindLecturerIdAsync(actor.UserId, cancellationToken) == state.ReviewerId)
        {
            await sessionAccessService.EnsureLecturerAccessAsync(
                state.SessionId,
                state.GroupId,
                actor.UserId,
                cancellationToken);
            return;
        }
        if (actor.Role == UserRole.Student && state.Status != ReviewSubmissionStatus.Draft &&
            await store.FindStudentGroupIdAsync(actor.UserId, cancellationToken) == state.GroupId) return;
        throw new UnauthorizedAccessException("You are not allowed to view this review submission.");
    }

    private async Task EnsureCanEditAsync(
        ReviewSubmissionState state, ReviewActor actor, CancellationToken cancellationToken)
    {
        if (actor.Role != UserRole.Lecturer ||
            await store.FindLecturerIdAsync(actor.UserId, cancellationToken) != state.ReviewerId)
            throw new UnauthorizedAccessException("Only the assigned reviewer can edit this review submission.");
        await sessionAccessService.EnsureLecturerAccessAsync(
            state.SessionId,
            state.GroupId,
            actor.UserId,
            cancellationToken);
    }

    private ReviewSubmissionResult Map(ReviewSubmissionData data)
    {
        ReviewSubmissionItemResult[] items = templateProvider.GetItems(data.Type).Select(item =>
        {
            data.ItemResponses.TryGetValue(item.ItemKey, out (ReviewChecklistAnswer? Answer, string? Comment) response);
            return new ReviewSubmissionItemResult(
                item.ItemKey, item.Label, item.Description, item.Priority, item.IsSection,
                item.CriteriaCode, response.Answer, response.Comment);
        }).ToArray();
        return new ReviewSubmissionResult(
            data.Id, data.SessionId, data.GroupId, data.GroupCode, data.ProjectName, data.Type,
            data.Status, data.SessionStatus, data.SessionDate, data.Slot, data.Room, data.ReviewerId,
            data.ReviewerCode, data.ReviewerName, data.WorkProductVersion, data.WorkProductSize,
            data.EffortHours, data.ReviewerComment, data.ReviewerComments, data.Suggestion, data.ResultText,
            data.LastSavedAt, data.SubmittedAt, items);
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[] NormalizeComments(
        IReadOnlyCollection<string>? comments,
        string? legacyComment)
    {
        IReadOnlyCollection<string> values = comments is { Count: > 0 }
            ? comments
            : [legacyComment ?? string.Empty];
        return values.Select(EmptyToNull).Where(x => x is not null).Select(x => x!)
            .Distinct(StringComparer.Ordinal).ToArray();
    }
}

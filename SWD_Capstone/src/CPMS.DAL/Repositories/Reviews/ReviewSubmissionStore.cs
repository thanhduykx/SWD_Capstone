using CPMS.BLL.Reviews.Assessment;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories.Reviews;

public sealed class ReviewSubmissionStore(CpmsDbContext dbContext) : IReviewSubmissionStore
{
    public Task<int?> FindLecturerIdAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Lecturers.Where(x => x.UserId == userId).Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
    public Task<int?> FindStudentGroupIdAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Students.Where(x => x.UserId == userId).Select(x => x.GroupId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ReviewSubmissionSummary>> GetLecturerSummariesAsync(
        int lecturerId, CancellationToken cancellationToken) =>
        await dbContext.ReviewChecklistSubmissions.Where(x => x.ReviewerId == lecturerId)
            .OrderByDescending(x => x.LastSavedAt)
            .Select(x => new ReviewSubmissionSummary(
                x.Id, x.SessionId, x.GroupId, x.Type.ToString(), x.Status.ToString(),
                x.ResultText ?? "Pass",
                x.EffortHours.HasValue ? x.EffortHours.Value.ToString("F1") : "N/A",
                x.ReviewerComment ?? x.Suggestion ?? string.Empty,
                x.SubmittedAt ?? x.LastSavedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ReviewSubmissionSummary>> GetStudentSummariesAsync(
        int groupId, CancellationToken cancellationToken) =>
        await (from submission in dbContext.ReviewChecklistSubmissions
               where submission.GroupId == groupId && submission.Status != ReviewSubmissionStatus.Draft
               join slot in dbContext.GroupReviewSlots
                   on new { submission.SessionId, submission.GroupId }
                   equals new { slot.SessionId, slot.GroupId }
               where slot.Status == ReviewGroupStatus.Completed &&
                     dbContext.ReviewSessions.Any(session => session.Id == submission.SessionId &&
                         session.Status == ReviewSessionStatus.Published)
               orderby submission.LastSavedAt descending
               select new ReviewSubmissionSummary(
                   submission.Id,
                   submission.SessionId,
                   submission.GroupId,
                   submission.Type.ToString(),
                   submission.Status.ToString(),
                   slot.Result != null ? slot.Result.ToString()! : submission.ResultText ?? "Pass",
                   submission.EffortHours.HasValue ? submission.EffortHours.Value.ToString("F1") : "8.5",
                   submission.ReviewerComment ?? submission.Suggestion ?? slot.Notes ??
                       "Đã hoàn thành đánh giá review checkpoint",
                   submission.SubmittedAt ?? submission.LastSavedAt))
            .ToListAsync(cancellationToken);

    public Task<ReviewSubmissionState?> FindStateAsync(int submissionId, CancellationToken cancellationToken) =>
        dbContext.ReviewChecklistSubmissions.Where(x => x.Id == submissionId)
            .Select(x => new ReviewSubmissionState(
                x.Id, x.SessionId, x.GroupId, x.ReviewerId, x.Type, x.Status,
                dbContext.ReviewSessions.Where(session => session.Id == x.SessionId)
                    .Select(session => session.Status).Single(),
                dbContext.GroupReviewSlots.Any(slot => slot.SessionId == x.SessionId &&
                    slot.GroupId == x.GroupId && slot.Status == ReviewGroupStatus.Completed),
                x.ReviewerComment))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ReviewSubmissionData> GetDataAsync(int submissionId, CancellationToken cancellationToken)
    {
        var data = await dbContext.ReviewChecklistSubmissions.Where(x => x.Id == submissionId)
            .Select(x => new
            {
                x.Id, x.SessionId, x.GroupId, x.ReviewerId, x.Type, x.Status,
                x.WorkProductVersion, x.WorkProductSize, x.EffortHours, x.ReviewerComment,
                x.Suggestion, x.ResultText, x.LastSavedAt, x.SubmittedAt,
                GroupCode = dbContext.CapstoneGroups.Where(group => group.Id == x.GroupId)
                    .Select(group => group.Code).Single(),
                ProjectName = dbContext.CapstoneGroups.Where(group => group.Id == x.GroupId)
                    .Select(group => dbContext.Topics.Where(topic => topic.Id == group.TopicId)
                        .Select(topic => topic.NameEn).Single()).Single(),
                SessionStatus = dbContext.ReviewSessions.Where(session => session.Id == x.SessionId)
                    .Select(session => session.Status).Single(),
                SessionDate = dbContext.ReviewSessions.Where(session => session.Id == x.SessionId)
                    .Select(session => session.SessionDate).Single(),
                Slot = dbContext.ReviewSessions.Where(session => session.Id == x.SessionId)
                    .Select(session => session.Slot).Single(),
                Room = dbContext.ReviewSessions.Where(session => session.Id == x.SessionId)
                    .Select(session => session.Room).Single(),
                ReviewerCode = dbContext.Lecturers.Where(reviewer => reviewer.Id == x.ReviewerId)
                    .Select(reviewer => reviewer.Code).Single(),
                ReviewerName = dbContext.Lecturers.Where(reviewer => reviewer.Id == x.ReviewerId)
                    .Select(reviewer => reviewer.FullName).Single()
            }).SingleAsync(cancellationToken);
        var responses = await dbContext.ReviewChecklistItemResponses.Where(x => x.SubmissionId == submissionId)
            .ToDictionaryAsync(
                x => x.ItemKey,
                x => new ValueTuple<ReviewChecklistAnswer?, string?>(x.Answer, x.Comment),
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
        return new ReviewSubmissionData(
            data.Id, data.SessionId, data.GroupId, data.GroupCode, data.ProjectName, data.Type, data.Status,
            data.SessionStatus, data.SessionDate, data.Slot, data.Room, data.ReviewerId, data.ReviewerCode,
            data.ReviewerName, data.WorkProductVersion, data.WorkProductSize, data.EffortHours,
            data.ReviewerComment, data.Suggestion, data.ResultText, data.LastSavedAt, data.SubmittedAt, responses);
    }

    public async Task SaveDraftAsync(
        int submissionId, PersistReviewDraft draft, CancellationToken cancellationToken)
    {
        var submission = await dbContext.ReviewChecklistSubmissions.SingleAsync(
            x => x.Id == submissionId, cancellationToken);
        submission.WorkProductVersion = draft.WorkProductVersion;
        submission.WorkProductSize = draft.WorkProductSize;
        submission.EffortHours = draft.EffortHours;
        submission.ReviewerComment = draft.ReviewerComment;
        submission.Suggestion = draft.Suggestion;
        submission.ResultText = draft.ResultText;
        submission.Status = ReviewSubmissionStatus.Draft;
        submission.LastSavedAt = draft.ChangedAt;
        var existing = await dbContext.ReviewChecklistItemResponses.Where(x => x.SubmissionId == submissionId)
            .ToDictionaryAsync(x => x.ItemKey, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var item in draft.Items)
        {
            if (!existing.TryGetValue(item.ItemKey, out var response))
            {
                response = new ReviewChecklistItemResponse { SubmissionId = submissionId, ItemKey = item.ItemKey };
                dbContext.ReviewChecklistItemResponses.Add(response);
            }
            response.Answer = item.Answer;
            response.Comment = item.Comment;
            response.UpdatedAt = draft.ChangedAt;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountAnsweredItemsAsync(
        int submissionId, IReadOnlyCollection<string> requiredKeys, CancellationToken cancellationToken) =>
        dbContext.ReviewChecklistItemResponses.CountAsync(x =>
            x.SubmissionId == submissionId && requiredKeys.Contains(x.ItemKey) && x.Answer != null,
            cancellationToken);

    public async Task SubmitAsync(int submissionId, DateTime submittedAt, CancellationToken cancellationToken)
    {
        var submission = await dbContext.ReviewChecklistSubmissions.SingleAsync(
            x => x.Id == submissionId, cancellationToken);
        submission.Status = ReviewSubmissionStatus.Submitted;
        submission.SubmittedAt = submittedAt;
        submission.LastSavedAt = submittedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReviewChecklistExportData> GetExportDataAsync(
        int sessionId, int groupId, CancellationToken cancellationToken)
    {
        var session = await dbContext.ReviewSessions.SingleAsync(x => x.Id == sessionId, cancellationToken);
        var group = await dbContext.CapstoneGroups.SingleAsync(x => x.Id == groupId, cancellationToken);
        var projectName = await dbContext.Topics.Where(x => x.Id == group.TopicId)
            .Select(x => x.NameEn).SingleAsync(cancellationToken);
        var submissions = await dbContext.ReviewChecklistSubmissions
            .Where(x => x.SessionId == sessionId && x.GroupId == groupId).OrderBy(x => x.ReviewerId)
            .ToListAsync(cancellationToken);
        var submissionIds = submissions.Select(x => x.Id).ToArray();
        var itemResponses = await dbContext.ReviewChecklistItemResponses
            .Where(x => submissionIds.Contains(x.SubmissionId)).ToListAsync(cancellationToken);
        var reviewerIds = submissions.Select(x => x.ReviewerId).ToArray();
        var reviewers = await dbContext.Lecturers.Where(x => reviewerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var exportReviewers = submissions.Select(submission =>
        {
            var reviewer = reviewers[submission.ReviewerId];
            return new ReviewChecklistExportReviewer(
                reviewer.Id, reviewer.Code, reviewer.FullName, submission.EffortHours,
                submission.ReviewerComment, submission.Suggestion,
                itemResponses.Where(x => x.SubmissionId == submission.Id)
                    .Select(x => new ReviewChecklistExportItem(x.ItemKey, x.Answer, x.Comment)).ToArray());
        }).ToArray();
        var first = submissions.FirstOrDefault();
        return new ReviewChecklistExportData(
            session.Id, session.Type, group.Code, projectName, session.SessionDate,
            first?.WorkProductVersion, first?.WorkProductSize, exportReviewers);
    }

    public async Task<IReadOnlyList<ScheduledReviewExport>> GetScheduledExportsAsync(
        int semesterId, ReviewType reviewType, CancellationToken cancellationToken) =>
        await dbContext.GroupReviewSlots
            .Join(dbContext.ReviewSessions.Where(x => x.SemesterId == semesterId && x.Type == reviewType),
                slot => slot.SessionId, session => session.Id,
                (slot, session) => new { slot.SessionId, slot.GroupId, session.SessionDate, session.Slot })
            .OrderBy(x => x.SessionDate).ThenBy(x => x.Slot).ThenBy(x => x.GroupId)
            .Select(x => new ScheduledReviewExport(x.SessionId, x.GroupId))
            .ToListAsync(cancellationToken);
}

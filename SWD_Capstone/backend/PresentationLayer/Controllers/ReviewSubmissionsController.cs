using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using CPMS.Api.Services;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Core.Exceptions;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/review-submissions")]
[Authorize]
public sealed class ReviewSubmissionsController(
    CpmsDbContext dbContext,
    ReviewChecklistTemplateService templateService) : ControllerBase
{
    [HttpGet("{submissionId:int}")]
    public async Task<ActionResult<ReviewSubmissionResponse>> Get(
        int submissionId,
        CancellationToken cancellationToken)
    {
        var submission = await dbContext.ReviewChecklistSubmissions.SingleOrDefaultAsync(x => x.Id == submissionId, cancellationToken);
        if (submission is null)
        {
            return NotFound();
        }

        await EnsureCanViewAsync(submission, cancellationToken);
        return Ok(await MapSubmissionAsync(submission.Id, cancellationToken));
    }

    [HttpPut("{submissionId:int}/draft")]
    [Authorize(Roles = "Lecturer,EvaluationPanel")]
    public async Task<ActionResult<ReviewSubmissionResponse>> SaveDraft(
        int submissionId,
        SaveReviewSubmissionDraftRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var submission = await dbContext.ReviewChecklistSubmissions.SingleOrDefaultAsync(x => x.Id == submissionId, cancellationToken);
        if (submission is null)
        {
            return NotFound();
        }

        await EnsureCanEditAsync(submission, cancellationToken);
        submission.WorkProductVersion = EmptyToNull(request.WorkProductVersion);
        submission.WorkProductSize = EmptyToNull(request.WorkProductSize);
        submission.EffortHours = request.EffortHours;
        submission.ReviewerComment = EmptyToNull(request.ReviewerComment);
        submission.Suggestion = EmptyToNull(request.Suggestion);
        submission.ResultText = EmptyToNull(request.ResultText);
        submission.Status = ReviewSubmissionStatus.Draft;
        submission.LastSavedAt = DateTime.UtcNow;

        await UpsertItemResponsesAsync(submission.Id, request.Items, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await MapSubmissionAsync(submission.Id, cancellationToken));
    }

    [HttpPost("{submissionId:int}/submit")]
    [Authorize(Roles = "Lecturer,EvaluationPanel")]
    public async Task<ActionResult<ReviewSubmissionResponse>> Submit(
        int submissionId,
        CancellationToken cancellationToken)
    {
        var submission = await dbContext.ReviewChecklistSubmissions.SingleOrDefaultAsync(x => x.Id == submissionId, cancellationToken);
        if (submission is null)
        {
            return NotFound();
        }

        await EnsureCanEditAsync(submission, cancellationToken);
        submission.Status = ReviewSubmissionStatus.Submitted;
        submission.SubmittedAt = DateTime.UtcNow;
        submission.LastSavedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await MapSubmissionAsync(submission.Id, cancellationToken));
    }

    [HttpGet("{submissionId:int}/export.xlsx")]
    public async Task<ActionResult> Export(
        int submissionId,
        CancellationToken cancellationToken)
    {
        var submission = await dbContext.ReviewChecklistSubmissions.SingleOrDefaultAsync(x => x.Id == submissionId, cancellationToken);
        if (submission is null)
        {
            return NotFound();
        }

        await EnsureCanViewAsync(submission, cancellationToken);
        var exportData = await BuildExportDataAsync(submission.SessionId, cancellationToken);
        var bytes = templateService.ExportWorkbook(exportData);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            SafeFileName($"{exportData.GroupCode}_{exportData.Type}_{exportData.SessionId}.xlsx"));
    }

    [HttpGet("export.zip")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult> ExportZip(
        int semesterId,
        ReviewType reviewType,
        CancellationToken cancellationToken)
    {
        var sessionIds = await dbContext.ReviewSessions
            .Where(x => x.SemesterId == semesterId && x.Type == reviewType)
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.Slot)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (sessionIds.Count == 0)
        {
            return NotFound(new { error = "No review sessions were found for export." });
        }

        using var archiveStream = new MemoryStream();
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var sessionId in sessionIds)
            {
                var exportData = await BuildExportDataAsync(sessionId, cancellationToken);
                var entry = archive.CreateEntry(SafeFileName($"{exportData.GroupCode}_{exportData.Type}_{exportData.SessionId}.xlsx"));
                await using var entryStream = entry.Open();
                var bytes = templateService.ExportWorkbook(exportData);
                await entryStream.WriteAsync(bytes, cancellationToken);
            }
        }

        return File(archiveStream.ToArray(), "application/zip", SafeFileName($"review_{reviewType}_{semesterId}.zip"));
    }

    private async Task UpsertItemResponsesAsync(
        int submissionId,
        IReadOnlyCollection<SaveReviewSubmissionItemRequest> items,
        CancellationToken cancellationToken)
    {
        var allowedKeys = templateService.GetItems(
                await dbContext.ReviewChecklistSubmissions
                    .Where(x => x.Id == submissionId)
                    .Select(x => x.Type)
                    .SingleAsync(cancellationToken))
            .Where(x => !x.IsSection)
            .Select(x => x.ItemKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existing = await dbContext.ReviewChecklistItemResponses
            .Where(x => x.SubmissionId == submissionId)
            .ToDictionaryAsync(x => x.ItemKey, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var item in items)
        {
            if (!allowedKeys.Contains(item.ItemKey))
            {
                throw new BusinessRuleException($"Checklist item '{item.ItemKey}' does not belong to this review template.");
            }

            if (!existing.TryGetValue(item.ItemKey, out var response))
            {
                response = new ReviewChecklistItemResponse
                {
                    SubmissionId = submissionId,
                    ItemKey = item.ItemKey
                };
                dbContext.ReviewChecklistItemResponses.Add(response);
            }

            response.Answer = item.Answer;
            response.Comment = EmptyToNull(item.Comment);
            response.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<ReviewSubmissionResponse> MapSubmissionAsync(int submissionId, CancellationToken cancellationToken)
    {
        var submission = await dbContext.ReviewChecklistSubmissions.SingleAsync(x => x.Id == submissionId, cancellationToken);
        var session = await dbContext.ReviewSessions.SingleAsync(x => x.Id == submission.SessionId, cancellationToken);
        var group = await dbContext.CapstoneGroups.SingleAsync(x => x.Id == submission.GroupId, cancellationToken);
        var projectName = await dbContext.Topics
            .Where(x => x.Id == group.TopicId)
            .Select(x => x.NameEn)
            .SingleAsync(cancellationToken);
        var reviewer = await dbContext.Lecturers.SingleAsync(x => x.Id == submission.ReviewerId, cancellationToken);
        var responses = await dbContext.ReviewChecklistItemResponses
            .Where(x => x.SubmissionId == submission.Id)
            .ToDictionaryAsync(x => x.ItemKey, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var items = templateService.GetItems(submission.Type)
            .Select(item =>
            {
                responses.TryGetValue(item.ItemKey, out var response);
                return new ReviewSubmissionItemResponse(
                    item.ItemKey,
                    item.Label,
                    item.Description,
                    item.Priority,
                    item.IsSection,
                    item.CriteriaCode,
                    response?.Answer,
                    response?.Comment);
            })
            .ToArray();

        return new ReviewSubmissionResponse(
            submission.Id,
            submission.SessionId,
            submission.GroupId,
            group.Code,
            projectName,
            submission.Type,
            submission.Status,
            session.Status,
            session.SessionDate,
            session.Slot,
            session.Room,
            submission.ReviewerId,
            reviewer.Code,
            reviewer.FullName,
            submission.WorkProductVersion,
            submission.WorkProductSize,
            submission.EffortHours,
            submission.ReviewerComment,
            submission.Suggestion,
            submission.ResultText,
            submission.LastSavedAt,
            submission.SubmittedAt,
            items);
    }

    private async Task<ReviewChecklistExportData> BuildExportDataAsync(int sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.ReviewSessions.SingleAsync(x => x.Id == sessionId, cancellationToken);
        var slot = await dbContext.GroupReviewSlots.SingleAsync(x => x.SessionId == sessionId, cancellationToken);
        var group = await dbContext.CapstoneGroups.SingleAsync(x => x.Id == slot.GroupId, cancellationToken);
        var projectName = await dbContext.Topics
            .Where(x => x.Id == group.TopicId)
            .Select(x => x.NameEn)
            .SingleAsync(cancellationToken);
        var submissions = await dbContext.ReviewChecklistSubmissions
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.ReviewerId)
            .ToListAsync(cancellationToken);
        var submissionIds = submissions.Select(x => x.Id).ToArray();
        var itemResponses = await dbContext.ReviewChecklistItemResponses
            .Where(x => submissionIds.Contains(x.SubmissionId))
            .ToListAsync(cancellationToken);

        var reviewers = new List<ReviewChecklistExportReviewer>();
        foreach (var submission in submissions)
        {
            var reviewer = await dbContext.Lecturers.SingleAsync(x => x.Id == submission.ReviewerId, cancellationToken);
            reviewers.Add(new ReviewChecklistExportReviewer(
                reviewer.Id,
                reviewer.Code,
                reviewer.FullName,
                submission.EffortHours,
                submission.ReviewerComment,
                submission.Suggestion,
                itemResponses
                    .Where(x => x.SubmissionId == submission.Id)
                    .Select(x => new ReviewChecklistExportItem(x.ItemKey, x.Answer, x.Comment))
                    .ToArray()));
        }

        var firstSubmission = submissions.FirstOrDefault();
        return new ReviewChecklistExportData(
            session.Id,
            session.Type,
            group.Code,
            projectName,
            session.SessionDate,
            firstSubmission?.WorkProductVersion,
            firstSubmission?.WorkProductSize,
            reviewers);
    }

    private async Task EnsureCanViewAsync(ReviewChecklistSubmission submission, CancellationToken cancellationToken)
    {
        if (User.IsInRole(nameof(UserRole.SystemAdministrator)) || User.IsInRole(nameof(UserRole.TrainingDepartment)))
        {
            return;
        }

        if (User.IsInRole(nameof(UserRole.Lecturer)) || User.IsInRole(nameof(UserRole.EvaluationPanel)))
        {
            var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
            if (lecturerId == submission.ReviewerId)
            {
                return;
            }
        }

        if (User.IsInRole(nameof(UserRole.Student)))
        {
            var userId = CurrentUserId();
            var studentGroupId = await dbContext.Students
                .Where(x => x.UserId == userId)
                .Select(x => x.GroupId)
                .SingleOrDefaultAsync(cancellationToken);
            var sessionStatus = await dbContext.ReviewSessions
                .Where(x => x.Id == submission.SessionId)
                .Select(x => x.Status)
                .SingleAsync(cancellationToken);
            if (studentGroupId == submission.GroupId && sessionStatus == ReviewSessionStatus.Published)
            {
                return;
            }
        }

        throw new UnauthorizedAccessException("You are not allowed to view this review submission.");
    }

    private async Task EnsureCanEditAsync(ReviewChecklistSubmission submission, CancellationToken cancellationToken)
    {
        var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
        if (lecturerId != submission.ReviewerId)
        {
            throw new UnauthorizedAccessException("Only the assigned reviewer can edit this review submission.");
        }
    }

    private async Task<int?> CurrentLecturerIdAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        return await dbContext.Lecturers
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SafeFileName(string fileName)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName;
    }
}

public sealed record SaveReviewSubmissionDraftRequest(
    string? WorkProductVersion,
    string? WorkProductSize,
    decimal? EffortHours,
    string? ReviewerComment,
    string? Suggestion,
    string? ResultText,
    IReadOnlyCollection<SaveReviewSubmissionItemRequest> Items);

public sealed record SaveReviewSubmissionItemRequest(
    string ItemKey,
    ReviewChecklistAnswer? Answer,
    string? Comment);

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

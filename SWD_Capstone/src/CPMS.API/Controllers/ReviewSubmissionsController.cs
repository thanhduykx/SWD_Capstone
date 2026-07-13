using System.Globalization;
using System.IO.Compression;
using System.Security.Claims;
using CPMS.API.Contracts.Reviews;
using CPMS.API.Services;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Reviews.Assessment;
using CPMS.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/review-submissions")]
[Authorize]
public sealed class ReviewSubmissionsController(
    IReviewSubmissionService submissionService,
    ReviewChecklistTemplateService templateService) : ControllerBase
{
    [HttpGet("my")]
    public async Task<ActionResult> GetMySubmissions(CancellationToken cancellationToken) =>
        Ok(await submissionService.GetMySubmissionsAsync(CurrentActor(), cancellationToken));

    [HttpGet("{submissionId:int}")]
    public async Task<ActionResult<ReviewSubmissionResponse>> Get(
        int submissionId, CancellationToken cancellationToken)
    {
        try { return Ok(Map(await submissionService.GetAsync(submissionId, CurrentActor(), cancellationToken))); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{submissionId:int}/draft")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewSubmissionResponse>> SaveDraft(
        int submissionId, SaveReviewSubmissionDraftRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var draft = new SaveReviewDraft(
                request.WorkProductVersion, request.WorkProductSize, request.EffortHours,
                request.ReviewerComment, request.Suggestion, request.ResultText,
                request.Items.Select(x => new SaveReviewItem(x.ItemKey, x.Answer, x.Comment)).ToArray());
            return Ok(Map(await submissionService.SaveDraftAsync(
                submissionId, draft, CurrentActor(), cancellationToken)));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ConflictException exception) { return Conflict(new { error = exception.Message }); }
    }

    [HttpPost("{submissionId:int}/submit")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewSubmissionResponse>> Submit(
        int submissionId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(Map(await submissionService.SubmitAsync(
                submissionId, CurrentActor(), cancellationToken)));
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ConflictException exception) { return Conflict(new { error = exception.Message }); }
    }

    [HttpGet("{submissionId:int}/export.xlsx")]
    public async Task<ActionResult> Export(int submissionId, CancellationToken cancellationToken)
    {
        try
        {
            var data = await submissionService.GetExportDataAsync(
                submissionId, CurrentActor(), cancellationToken);
            return File(templateService.ExportWorkbook(data),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                SafeFileName($"{data.GroupCode}_{data.Type}_{data.SessionId}.xlsx"));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("export.zip")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult> ExportZip(
        int semesterId, ReviewType reviewType, CancellationToken cancellationToken)
    {
        try
        {
            var exports = await submissionService.GetBulkExportDataAsync(
                semesterId, reviewType, cancellationToken);
            using var archiveStream = new MemoryStream();
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var data in exports)
                {
                    var entry = archive.CreateEntry(SafeFileName(
                        $"{data.GroupCode}_{data.Type}_{data.SessionId}.xlsx"));
                    await using var entryStream = entry.Open();
                    await entryStream.WriteAsync(templateService.ExportWorkbook(data), cancellationToken);
                }
            }
            return File(archiveStream.ToArray(), "application/zip",
                SafeFileName($"review_{reviewType}_{semesterId}.zip"));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
    }

    private ReviewActor CurrentActor() => new(CurrentUserId(), CurrentRole());
    private UserRole CurrentRole() => Enum.GetValues<UserRole>().FirstOrDefault(role => User.IsInRole(role.ToString()));
    private int CurrentUserId() => int.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Missing user identifier."), CultureInfo.InvariantCulture);

    private static ReviewSubmissionResponse Map(ReviewSubmissionResult result) =>
        new(result.Id, result.SessionId, result.GroupId, result.GroupCode, result.ProjectName,
            result.Type, result.Status, result.SessionStatus, result.SessionDate, result.Slot, result.Room,
            result.ReviewerId, result.ReviewerCode, result.ReviewerName, result.WorkProductVersion,
            result.WorkProductSize, result.EffortHours, result.ReviewerComment, result.Suggestion,
            result.ResultText, result.LastSavedAt, result.SubmittedAt,
            result.Items.Select(x => new ReviewSubmissionItemResponse(
                x.ItemKey, x.Label, x.Description, x.Priority, x.IsSection,
                x.CriteriaCode, x.Answer, x.Comment)).ToArray());

    private static string SafeFileName(string fileName)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(invalidChar, '_');
        return fileName;
    }
}

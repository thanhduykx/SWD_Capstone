using System.Globalization;
using System.Security.Claims;
using CPMS.API.Contracts.Reviews;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Reviews.Assessment;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.API.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/review-attendance")]
[Authorize]
public sealed class ReviewAttendanceController(
    IReviewAttendanceService attendanceService,
    IHubContext<ReviewProgressHub> progressHub) : ControllerBase
{
    [HttpGet("{sessionId:int}")]
    public async Task<ActionResult<ReviewAttendanceListResponse>> GetAttendance(
        int sessionId, [FromQuery] int? groupId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(Map(await attendanceService.GetAttendanceAsync(
                sessionId, groupId, CurrentActor(), cancellationToken)));
        }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (BusinessRuleException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpPost("{sessionId:int}")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewAttendanceListResponse>> SubmitAttendance(
        int sessionId, SubmitAttendanceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            AttendanceEntry[] entries = request.Entries.Select(x => new AttendanceEntry(x.StudentId, x.IsPresent, x.Note)).ToArray();
            return Ok(Map(await attendanceService.SubmitAttendanceAsync(
                sessionId, request.GroupId, entries, CurrentActor(), cancellationToken)));
        }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (BusinessRuleException exception) { return BadRequest(new { error = exception.Message }); }
        catch (ConflictException exception) { return Conflict(new { error = exception.Message }); }
    }

    [HttpGet("{sessionId:int}/comments")]
    public async Task<ActionResult<IReadOnlyList<ReviewCommentResponse>>> GetComments(
        int sessionId, [FromQuery] int? groupId, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<ReviewCommentResult> results = await attendanceService.GetCommentsAsync(
                sessionId, groupId, CurrentActor(), cancellationToken);
            return Ok(results.Select(MapComment).ToArray());
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException) { return Forbid(); }
    }

    [HttpPost("{sessionId:int}/comments")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewCommentResponse>> AddComment(
        int sessionId, AddCommentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ReviewCommentResponse response = MapComment(await attendanceService.AddCommentAsync(
                sessionId, request.GroupId, request.Content, CurrentActor(), cancellationToken));
            await progressHub.Clients
                .Group(ReviewProgressHub.GroupName(sessionId, request.GroupId))
                .SendAsync(ReviewProgressHub.CommentAdded, response, cancellationToken);
            return Ok(response);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (BusinessRuleException exception) { return BadRequest(new { error = exception.Message }); }
        catch (ConflictException exception) { return Conflict(new { error = exception.Message }); }
    }

    [HttpPost("{sessionId:int}/groups/{groupId:int}/complete")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult> CompleteReview(
        int sessionId, int groupId, CancellationToken cancellationToken)
    {
        try
        {
            CompleteReviewResult result = await attendanceService.CompleteAsync(
                sessionId, groupId, CurrentActor(), cancellationToken);
            return Ok(new { result.SessionId, result.GroupId, status = result.Status, result.CompletedAt });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ConflictException exception) { return Conflict(new { error = exception.Message }); }
    }

    private ReviewActor CurrentActor() => new(CurrentUserId(), CurrentRole());
    private UserRole CurrentRole() => Enum.GetValues<UserRole>().FirstOrDefault(role => User.IsInRole(role.ToString()));
    private int CurrentUserId() => int.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Missing user identifier."),
        CultureInfo.InvariantCulture);

    private static ReviewAttendanceListResponse Map(ReviewAttendanceResult result) =>
        new(result.SessionId, result.SessionCode, result.GroupId, result.GroupCode,
            result.GroupStatus.ToString(), result.SessionDate, result.Slot, result.Room,
            result.Students.Select(x => new AttendanceStudentResponse(
                x.StudentId, x.StudentCode, x.FullName, x.IsPresent, x.Note, x.StudentConfirmedAt)).ToArray());
    private static ReviewCommentResponse MapComment(ReviewCommentResult result) =>
        new(result.Id, result.AuthorUserId, result.AuthorName, result.Content, result.CreatedAt, result.UpdatedAt);
}

using System.Globalization;
using System.Security.Claims;
using CPMS.API.Contracts.Reviews;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Reviews.Sessions;
using CPMS.BLL.Reviews.Access;
using CPMS.BLL.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/review-sessions")]
[Authorize]
public sealed class ReviewSessionsController(
    IReviewSessionService sessionService,
    IReviewSessionAccessService accessService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment,Moderator")]
    public async Task<ActionResult<ReviewSessionResponse>> Create(
        CreateReviewSessionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ReviewSessionResult result = await sessionService.CreateAsync(
            new AssignReviewSessionCommand(
                request.Code, request.GroupId, request.Type, request.ReviewerIds,
                request.PreviousReviewerIds, request.Slot, request.Room, request.SessionDate),
            cancellationToken);
        ReviewSessionResponse response = Map(result);
        return CreatedAtAction(nameof(Create), new { response.Id }, response);
    }

    [HttpPost("bulk-assign")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment,Moderator")]
    public async Task<ActionResult<BulkAssignReviewSessionsResponse>> BulkAssign(
        BulkAssignReviewSessionsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Sessions is null)
        {
            return BadRequest(new { error = "Sessions is required." });
        }
        AssignReviewSessionCommand[] commands = request.Sessions.Select(x => new AssignReviewSessionCommand(
            x.Code, x.GroupId, x.Type, x.ReviewerIds, x.PreviousReviewerIds,
            x.Slot, x.Room, x.SessionDate)).ToArray();
        IReadOnlyList<ReviewSessionResult> results = await sessionService.BulkAssignAsync(commands, cancellationToken);
        return Ok(new BulkAssignReviewSessionsResponse(0, 0, results.Select(Map).ToArray()));
    }

    [HttpPatch("{sessionId:int}")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment,Moderator")]
    public async Task<ActionResult<ReviewSessionResponse>> Update(
        int sessionId,
        UpdateReviewSessionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ReviewSessionResult result = await sessionService.UpdateAsync(
                sessionId,
                new UpdateReviewSessionCommand(
                    request.Code, request.ReviewerIds, request.PreviousReviewerIds,
                    request.Slot, request.Room, request.SessionDate, request.Status),
                cancellationToken);
            return Ok(Map(result));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ConflictException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpGet("my")]
    [Authorize]
    public async Task<IReadOnlyList<MyReviewSessionResponse>> GetMySessions(CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Lecturer")) throw new UnauthorizedAccessException("Lecturer role required.");
        IReadOnlyList<MyReviewSessionResult> results = await sessionService.GetLecturerSessionsAsync(CurrentUserId(), cancellationToken);
        return results.Select(x => new MyReviewSessionResponse(
            x.SessionId, x.SubmissionId, x.Code, x.Type, x.SessionStatus, x.GroupId, x.GroupCode,
            x.TopicName, x.SessionDate, x.Slot, x.Room, x.ReviewerCount, x.SubmissionStatus, x.LastSavedAt,
            x.HasAccessCode, x.IsAccessVerified)).ToArray();
    }

    [HttpPost("{sessionId:int}/groups/{groupId:int}/access-code")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<GeneratedReviewSessionAccessCodeResponse>> GenerateAccessCode(
        int sessionId,
        int groupId,
        CancellationToken cancellationToken)
    {
        try
        {
            GeneratedReviewSessionAccessCode result = await accessService.GenerateAsync(
                sessionId,
                groupId,
                CurrentUserId(),
                cancellationToken);
            return Ok(new GeneratedReviewSessionAccessCodeResponse(
                result.SessionId,
                result.GroupId,
                result.AccessCode,
                result.CodeVersion,
                result.GeneratedAt));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
    }

    [HttpPost("{sessionId:int}/groups/{groupId:int}/access-code/verify")]
    [Authorize(Roles = "Lecturer")]
    [EnableRateLimiting("review-access-code")]
    public async Task<ActionResult<VerifiedReviewSessionAccessResponse>> VerifyAccessCode(
        int sessionId,
        int groupId,
        VerifyReviewSessionAccessCodeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            VerifiedReviewSessionAccess result = await accessService.VerifyAsync(
                sessionId,
                groupId,
                CurrentUserId(),
                request.AccessCode,
                cancellationToken);
            return Ok(new VerifiedReviewSessionAccessResponse(
                result.SessionId,
                result.GroupId,
                result.IsVerified,
                result.VerifiedAt));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = exception.Message });
        }
    }

    private int CurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(idStr)) throw new UnauthorizedAccessException("Missing user identifier.");
        return int.Parse(idStr, CultureInfo.InvariantCulture);
    }

    private static ReviewSessionResponse Map(ReviewSessionResult result) =>
        new(
            result.Id,
            result.Code,
            result.SemesterId,
            result.Groups.Select(x => new ReviewSessionGroupResponse(
                x.GroupId, x.GroupCode, x.GroupPosition, x.Status, x.CompletedAt)).ToArray(),
            result.Type,
            result.Status,
            result.ReviewerIds,
            result.SessionDate,
            result.Slot,
            result.Room);
}

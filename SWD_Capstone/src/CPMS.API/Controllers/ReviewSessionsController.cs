using System.Globalization;
using System.Security.Claims;
using CPMS.API.Contracts.Reviews;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Reviews.Sessions;
using CPMS.BLL.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/review-sessions")]
[Authorize]
public sealed class ReviewSessionsController(IReviewSessionService sessionService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<ReviewSessionResponse>> Create(
        CreateReviewSessionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ReviewSessionResult result = await sessionService.CreateAsync(
            ToCommand(
                request.Code,
                request.GroupId,
                request.Type,
                [request.Reviewer1Id, request.Reviewer2Id],
                request.PreviousReviewerIds,
                request.Slot,
                request.Room,
                request.SessionDate),
            cancellationToken);
        ReviewSessionResponse response = Map(result);
        return CreatedAtAction(nameof(Create), new { response.Id }, response);
    }

    [HttpPost("bulk-assign")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<BulkAssignReviewSessionsResponse>> BulkAssign(
        BulkAssignReviewSessionsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Sessions is null)
        {
            return BadRequest(new { error = "Sessions is required." });
        }
        AssignReviewSessionCommand[] commands = request.Sessions.Select(x => ToCommand(
            x.Code, x.GroupId, x.Type, x.ReviewerIds, x.PreviousReviewerIds,
            x.Slot, x.Room, x.SessionDate)).ToArray();
        IReadOnlyList<ReviewSessionResult> results = await sessionService.BulkAssignAsync(commands, cancellationToken);
        return Ok(new BulkAssignReviewSessionsResponse(0, 0, results.Select(Map).ToArray()));
    }

    [HttpPatch("{sessionId:int}")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
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
    [Authorize(Roles = "Lecturer")]
    public async Task<IReadOnlyList<MyReviewSessionResponse>> GetMySessions(CancellationToken cancellationToken)
    {
        IReadOnlyList<MyReviewSessionResult> results = await sessionService.GetLecturerSessionsAsync(CurrentUserId(), cancellationToken);
        return results.Select(x => new MyReviewSessionResponse(
            x.SessionId, x.SubmissionId, x.Code, x.Type, x.SessionStatus, x.GroupCode,
            x.SessionDate, x.Slot, x.Room, x.SubmissionStatus, x.LastSavedAt)).ToArray();
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);

    private static AssignReviewSessionCommand ToCommand(
        string code,
        int groupId,
        ReviewType type,
        IReadOnlyCollection<int> reviewerIds,
        IReadOnlyCollection<int>? previousReviewerIds,
        int slot,
        string room,
        DateTime sessionDate) =>
        new(code, groupId, type, reviewerIds, previousReviewerIds, slot, room, sessionDate);

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

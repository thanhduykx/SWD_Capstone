using System.Globalization;
using System.Security.Claims;
using CPMS.API.Contracts.Defense;
using CPMS.API.Hubs;
using CPMS.BLL.Defense.Sessions;
using CPMS.BLL.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/defense-sessions")]
[Authorize(Roles = "Lecturer")]
public sealed class DefenseSessionsController(
    IDefenseSessionService defenseService,
    IHubContext<DefenseScoringHub> hubContext) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<DefenseSessionListResult>>> GetAllSessions(
        [FromQuery] int? semesterId, CancellationToken cancellationToken) =>
        Ok(await defenseService.GetAllAsync(semesterId, cancellationToken));

    [HttpGet("resolve/{code}")]
    public async Task<ActionResult<DefenseSessionStateResult>> ResolveByCode(
        string code, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await defenseService.ResolveAsync(code, CurrentUserId(), cancellationToken));
        }
        catch (BusinessRuleException exception) { return BadRequest(new { error = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { error = exception.Message }); }
    }

    [HttpPost("{sessionId:int}/start")]
    public async Task<ActionResult<DefenseSessionStateResult>> Start(
        int sessionId, CancellationToken cancellationToken)
    {
        DefenseSessionStateResult response = await defenseService.StartAsync(sessionId, CurrentActor(), cancellationToken);
        await BroadcastAsync(sessionId, DefenseScoringHub.SessionStarted, response, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{sessionId:int}/comments")]
    public async Task<ActionResult<IReadOnlyList<DefenseCommentResult>>> GetComments(
        int sessionId, CancellationToken cancellationToken) =>
        Ok(await defenseService.GetCommentsAsync(sessionId, CurrentUserId(), cancellationToken));

    [HttpPost("{sessionId:int}/comments")]
    public async Task<ActionResult<DefenseCommentResult>> SubmitComment(
        int sessionId, SubmitDefenseCommentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DefenseCommentResult response = await defenseService.SubmitCommentAsync(
            sessionId, new SubmitDefenseCommentData(request.Content),
            CurrentActor(), cancellationToken);
        await BroadcastAsync(sessionId, DefenseScoringHub.CommentSubmitted, response, cancellationToken);
        return Ok(response);
    }

    [HttpGet("{sessionId:int}/evidences")]
    public async Task<ActionResult<IReadOnlyList<DefenseEvidenceResponse>>> GetEvidences(
        int sessionId, CancellationToken cancellationToken)
    {
        IReadOnlyList<DefenseEvidenceResult> results = await defenseService.GetEvidencesAsync(sessionId, CurrentUserId(), cancellationToken);
        return Ok(results.Select(MapEvidence).ToArray());
    }

    [HttpPost("{sessionId:int}/evidences")]
    [RequestSizeLimit(5_242_880)]
    public async Task<ActionResult<DefenseEvidenceResponse>> UploadEvidence(
        int sessionId, [FromForm] UploadDefenseEvidenceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.File is null)
            return BadRequest(new { error = "Evidence image is required." });
        await using Stream content = request.File.OpenReadStream();
        DefenseEvidenceResult result = await defenseService.UploadEvidenceAsync(
            sessionId,
            new UploadDefenseEvidenceData(
                request.File.FileName, request.File.ContentType, request.File.Length, request.Note, content),
            CurrentActor(), cancellationToken);
        await BroadcastAsync(sessionId, "defenseEvidenceCaptured", result, cancellationToken);
        return Ok(MapEvidence(result));
    }

    [HttpPost("{sessionId:int}/close")]
    public async Task<ActionResult<DefenseSessionStateResult>> Close(
        int sessionId, CancellationToken cancellationToken)
    {
        DefenseSessionStateResult response = await defenseService.CloseAsync(sessionId, CurrentActor(), cancellationToken);
        await BroadcastAsync(sessionId, DefenseScoringHub.SessionClosed, response, cancellationToken);
        return Ok(response);
    }

    private Task BroadcastAsync(
        int sessionId, string eventName, object payload, CancellationToken cancellationToken) =>
        hubContext.Clients.Group(DefenseScoringHub.GroupName(sessionId))
            .SendAsync(eventName, payload, cancellationToken);
    private DefenseSessionActor CurrentActor() => new(
        CurrentUserId(), HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent);
    private int CurrentUserId() => int.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Missing user identifier."), CultureInfo.InvariantCulture);
    private static DefenseEvidenceResponse MapEvidence(DefenseEvidenceResult x) =>
        new(x.Id, x.DefenseSessionId, x.CapturedByLecturerId, x.FileName, x.FilePath,
            x.ContentType, x.FileSize, x.Note, x.CapturedAt);
}

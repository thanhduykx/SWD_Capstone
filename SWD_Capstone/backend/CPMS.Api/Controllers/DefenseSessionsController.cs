using System.Security.Claims;
using System.Globalization;
using System.Text.Json;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Core.Services;
using CPMS.Api.Hubs;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/defense-sessions")]
[Authorize(Roles = "Lecturer,EvaluationPanel")]
public sealed class DefenseSessionsController(
    CpmsDbContext dbContext,
    AssignmentRules rules,
    IHubContext<DefenseScoringHub> hubContext) : ControllerBase
{
    [HttpPost("{sessionId:int}/start")]
    public async Task<ActionResult<DefenseSessionStateResponse>> Start(
        int sessionId,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var lecturerId = await CurrentLecturerId(userId, cancellationToken);
        if (lecturerId is null)
        {
            return Forbid();
        }

        var session = await dbContext.DefenseSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        var council = await dbContext.Councils.SingleAsync(x => x.Id == session.CouncilId, cancellationToken);
        rules.EnsureChairman(lecturerId.Value, council.ChairmanId);
        rules.EnsureSessionEditable(session.IsLocked);

        if (session.StartedAt is null)
        {
            session.StartedAt = DateTime.UtcNow;
            session.StartedById = userId;
            council.Status = CouncilStatus.Active;
            dbContext.AuditLogs.Add(NewAudit(userId, "START_DEFENSE_SESSION", session.Id, null, session.StartedAt));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var response = new DefenseSessionStateResponse(
            session.Id,
            session.CouncilId,
            council.Code,
            session.StartedAt,
            session.EndedAt,
            session.IsLocked,
            true);

        await hubContext.Clients.Group(DefenseScoringHub.GroupName(sessionId))
            .SendAsync(DefenseScoringHub.SessionStarted, response, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{sessionId:int}/scores")]
    public async Task<ActionResult> SubmitScore(
        int sessionId,
        SubmitScoreRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = CurrentUserId();
        var lecturerId = await CurrentLecturerId(userId, cancellationToken);
        if (lecturerId is null)
        {
            return Forbid();
        }

        var session = await dbContext.DefenseSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        var councilMemberIds = await dbContext.CouncilMembers
            .Where(x => x.CouncilId == session.CouncilId)
            .Select(x => x.LecturerId)
            .ToArrayAsync(cancellationToken);
        rules.EnsureCouncilMember(lecturerId.Value, councilMemberIds);
        rules.EnsureSessionStarted(session.StartedAt, session.IsLocked);
        rules.ValidateScore(request.ScoreValue);

        var score = await dbContext.Scores.SingleOrDefaultAsync(
            x => x.DefenseSessionId == sessionId && x.ScorerId == lecturerId &&
                 x.StudentId == request.StudentId && x.ScoreType == request.ScoreType,
            cancellationToken);
        var oldValue = score?.ScoreValue;
        if (score is null)
        {
            score = new Score
            {
                DefenseSessionId = sessionId,
                ScorerId = lecturerId.Value,
                StudentId = request.StudentId,
                ScoreType = request.ScoreType,
                ScoreValue = request.ScoreValue
            };
            dbContext.Scores.Add(score);
        }
        else
        {
            score.ScoreValue = request.ScoreValue;
            score.SubmittedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ScoreSubmissionHistories.Add(new ScoreSubmissionHistory
        {
            ScoreId = score.Id,
            DefenseSessionId = sessionId,
            ScorerId = lecturerId.Value,
            SubmittedByUserId = userId,
            StudentId = request.StudentId,
            ScoreType = request.ScoreType,
            OldScoreValue = oldValue,
            NewScoreValue = request.ScoreValue,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent,
            TrustReason = "JWT user matched an assigned council member and the chairman had started the session."
        });
        dbContext.AuditLogs.Add(NewAudit(userId, "SUBMIT_SCORE", score.Id, oldValue, request.ScoreValue));
        await dbContext.SaveChangesAsync(cancellationToken);

        await hubContext.Clients.Group(DefenseScoringHub.GroupName(sessionId))
            .SendAsync(DefenseScoringHub.ScoreSubmitted, new
            {
                sessionId,
                score.Id,
                scorerId = lecturerId.Value,
                request.StudentId,
                request.ScoreType,
                request.ScoreValue,
                score.SubmittedAt
            }, cancellationToken);

        return Ok(score);
    }

    [HttpPost("{sessionId:int}/close")]
    public async Task<ActionResult<DefenseSessionStateResponse>> Close(int sessionId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var lecturerId = await CurrentLecturerId(userId, cancellationToken);
        var session = await dbContext.DefenseSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        if (lecturerId is null)
        {
            return Forbid();
        }

        var council = await dbContext.Councils.SingleAsync(x => x.Id == session.CouncilId, cancellationToken);
        rules.EnsureChairman(lecturerId.Value, council.ChairmanId);
        rules.EnsureSessionStarted(session.StartedAt, session.IsLocked);
        rules.EnsureSessionEditable(session.IsLocked);
        session.IsLocked = true;
        session.EndedAt = DateTime.UtcNow;
        council.Status = CouncilStatus.Closed;
        var scores = await dbContext.Scores.Where(x => x.DefenseSessionId == sessionId).ToListAsync(cancellationToken);
        foreach (var score in scores)
        {
            score.IsLocked = true;
        }

        dbContext.AuditLogs.Add(NewAudit(userId, "LOCK_DEFENSE_SESSION", session.Id, false, true));
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new DefenseSessionStateResponse(
            session.Id,
            session.CouncilId,
            council.Code,
            session.StartedAt,
            session.EndedAt,
            session.IsLocked,
            true);

        await hubContext.Clients.Group(DefenseScoringHub.GroupName(sessionId))
            .SendAsync(DefenseScoringHub.SessionClosed, response, cancellationToken);
        return Ok(response);
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);

    private Task<int?> CurrentLecturerId(int userId, CancellationToken cancellationToken) =>
        dbContext.Lecturers
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private AuditLog NewAudit(int userId, string action, int entityId, object? oldValue, object? newValue) =>
        new()
        {
            UserId = userId,
            Action = action,
            EntityType = action == "SUBMIT_SCORE" ? nameof(Score) : nameof(DefenseSession),
            EntityId = entityId,
            OldValue = JsonSerializer.Serialize(oldValue),
            NewValue = JsonSerializer.Serialize(newValue),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent
        };
}

public sealed record SubmitScoreRequest(int StudentId, ScoreType ScoreType, decimal ScoreValue);
public sealed record DefenseSessionStateResponse(
    int SessionId,
    int CouncilId,
    string CouncilCode,
    DateTime? StartedAt,
    DateTime? EndedAt,
    bool IsLocked,
    bool IsChairman);

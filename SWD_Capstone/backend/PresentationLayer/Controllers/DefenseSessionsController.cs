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
    DefenseScoringService defenseScoringService,
    IWebHostEnvironment environment,
    IHubContext<DefenseScoringHub> hubContext) : ControllerBase
{
    [HttpGet("resolve/{code}")]
    public async Task<ActionResult<DefenseSessionStateDto>> ResolveByCode(
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { error = "Defense code is required." });
        }

        var userId = CurrentUserId();
        var lecturerId = await CurrentLecturerId(userId, cancellationToken);
        if (lecturerId is null)
        {
            return Forbid();
        }

        var normalizedCode = code.Trim();
        var sessionQuery = dbContext.DefenseSessions.AsQueryable();
        if (int.TryParse(normalizedCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sessionIdFromCode))
        {
            sessionQuery = sessionQuery.Where(x =>
                x.Id == sessionIdFromCode ||
                x.Code == normalizedCode ||
                dbContext.Councils.Any(c => c.Id == x.CouncilId && c.Code == normalizedCode));
        }
        else
        {
            sessionQuery = sessionQuery.Where(x =>
                x.Code == normalizedCode ||
                dbContext.Councils.Any(c => c.Id == x.CouncilId && c.Code == normalizedCode));
        }

        var session = await sessionQuery
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.DefenseRoundId,
                x.CouncilId,
                x.GroupId,
                x.SessionDate,
                x.Slot,
                x.Room,
                x.StartedAt,
                x.EndedAt,
                x.IsLocked,
                CouncilCode = dbContext.Councils.Where(c => c.Id == x.CouncilId).Select(c => c.Code).Single(),
                ChairmanId = dbContext.Councils.Where(c => c.Id == x.CouncilId).Select(c => c.ChairmanId).Single(),
                MemberIds = dbContext.CouncilMembers
                    .Where(m => m.CouncilId == x.CouncilId)
                    .Select(m => m.LecturerId)
                    .ToArray()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return NotFound(new { error = "Defense code was not found." });
        }

        rules.EnsureCouncilMember(lecturerId.Value, session.MemberIds);

        return Ok(new DefenseSessionStateDto(
            session.Id,
            session.Code,
            session.DefenseRoundId,
            session.CouncilId,
            session.CouncilCode,
            session.GroupId,
            session.SessionDate,
            session.Slot,
            session.Room,
            session.StartedAt,
            session.EndedAt,
            session.IsLocked,
            lecturerId.Value == session.ChairmanId));
    }

    [HttpPost("{sessionId:int}/start")]
    public async Task<ActionResult<DefenseSessionStateDto>> Start(
        int sessionId,
        CancellationToken cancellationToken)
    {
        var response = await defenseScoringService.StartAsync(
            sessionId,
            CurrentActor(),
            cancellationToken);

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

        var score = await defenseScoringService.SubmitScoreAsync(
            sessionId,
            new SubmitDefenseScoreCommand(request.StudentId, request.ScoreType, request.ScoreValue),
            CurrentActor(),
            cancellationToken);

        await hubContext.Clients.Group(DefenseScoringHub.GroupName(sessionId))
            .SendAsync(DefenseScoringHub.ScoreSubmitted, score, cancellationToken);

        return Ok(score);
    }

    [HttpGet("{sessionId:int}/evidences")]
    public async Task<ActionResult<IReadOnlyList<DefenseEvidenceResponse>>> GetEvidences(
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

        var memberIds = await dbContext.CouncilMembers
            .Where(x => x.CouncilId == session.CouncilId)
            .Select(x => x.LecturerId)
            .ToArrayAsync(cancellationToken);
        rules.EnsureCouncilMember(lecturerId.Value, memberIds);

        var evidences = await dbContext.DefenseEvidences
            .Where(x => x.DefenseSessionId == sessionId)
            .OrderByDescending(x => x.CapturedAt)
            .Select(x => new DefenseEvidenceResponse(
                x.Id,
                x.DefenseSessionId,
                x.CapturedByLecturerId,
                x.FileName,
                x.FilePath,
                x.ContentType,
                x.FileSize,
                x.Note,
                x.CapturedAt))
            .ToListAsync(cancellationToken);

        return Ok(evidences);
    }

    [HttpPost("{sessionId:int}/evidences")]
    [RequestSizeLimit(5_242_880)]
    public async Task<ActionResult<DefenseEvidenceResponse>> UploadEvidence(
        int sessionId,
        [FromForm] UploadDefenseEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new { error = "Evidence image is required." });
        }

        if (!request.File.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Only image evidence files are allowed." });
        }

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

        var memberIds = await dbContext.CouncilMembers
            .Where(x => x.CouncilId == session.CouncilId)
            .Select(x => x.LecturerId)
            .ToArrayAsync(cancellationToken);
        rules.EnsureCouncilMember(lecturerId.Value, memberIds);
        rules.EnsureSessionStarted(session.StartedAt, session.IsLocked);

        var evidenceRoot = Path.Combine(environment.WebRootPath, "evidence", sessionId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(evidenceRoot);

        var extension = Path.GetExtension(request.File.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(evidenceRoot, fileName);
        await using (var stream = System.IO.File.Create(physicalPath))
        {
            await request.File.CopyToAsync(stream, cancellationToken);
        }

        var publicPath = $"/evidence/{sessionId}/{fileName}";
        var evidence = new DefenseEvidence
        {
            DefenseSessionId = sessionId,
            CapturedByUserId = userId,
            CapturedByLecturerId = lecturerId.Value,
            FileName = fileName,
            FilePath = publicPath,
            ContentType = request.File.ContentType,
            FileSize = request.File.Length,
            Note = request.Note,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent
        };

        dbContext.DefenseEvidences.Add(evidence);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.AuditLogs.Add(NewAudit(userId, "CAPTURE_DEFENSE_EVIDENCE", nameof(DefenseEvidence), evidence.Id, null, new
        {
            evidence.DefenseSessionId,
            evidence.FilePath,
            evidence.Note
        }));
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new DefenseEvidenceResponse(
            evidence.Id,
            evidence.DefenseSessionId,
            evidence.CapturedByLecturerId,
            evidence.FileName,
            evidence.FilePath,
            evidence.ContentType,
            evidence.FileSize,
            evidence.Note,
            evidence.CapturedAt);

        await hubContext.Clients.Group(DefenseScoringHub.GroupName(sessionId))
            .SendAsync("defenseEvidenceCaptured", response, cancellationToken);

        return Ok(response);
    }

    [HttpPost("{sessionId:int}/close")]
    public async Task<ActionResult<DefenseSessionStateDto>> Close(int sessionId, CancellationToken cancellationToken)
    {
        var response = await defenseScoringService.CloseAsync(
            sessionId,
            CurrentActor(),
            cancellationToken);

        await hubContext.Clients.Group(DefenseScoringHub.GroupName(sessionId))
            .SendAsync(DefenseScoringHub.SessionClosed, response, cancellationToken);
        return Ok(response);
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);

    private ActorContext CurrentActor() =>
        new(
            CurrentUserId(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent);

    private Task<int?> CurrentLecturerId(int userId, CancellationToken cancellationToken) =>
        dbContext.Lecturers
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private AuditLog NewAudit(int userId, string action, string entityType, int entityId, object? oldValue, object? newValue) =>
        new()
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = JsonSerializer.Serialize(oldValue),
            NewValue = JsonSerializer.Serialize(newValue),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent
        };
}

public sealed record SubmitScoreRequest(int StudentId, ScoreType ScoreType, decimal ScoreValue);
public sealed class UploadDefenseEvidenceRequest
{
    public IFormFile? File { get; set; }
    public string? Note { get; set; }
}

public sealed record DefenseEvidenceResponse(
    int Id,
    int DefenseSessionId,
    int CapturedByLecturerId,
    string FileName,
    string FilePath,
    string ContentType,
    long FileSize,
    string? Note,
    DateTime CapturedAt);

using System.Text.Json;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Core.Exceptions;
using CPMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Core.Services;

public sealed class DefenseScoringService(CpmsDbContext dbContext, AssignmentRules rules)
{
    public async Task<DefenseSessionStateDto> StartAsync(
        int sessionId,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var lecturerId = await GetCurrentLecturerIdAsync(actor.UserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Only lecturers assigned to a council can control defense scoring.");

        var session = await GetSessionAsync(sessionId, cancellationToken);
        var council = await dbContext.Councils.SingleAsync(x => x.Id == session.CouncilId, cancellationToken);

        rules.EnsureChairman(lecturerId, council.ChairmanId);
        rules.EnsureSessionEditable(session.IsLocked);

        if (session.StartedAt is null)
        {
            session.StartedAt = DateTime.UtcNow;
            session.StartedById = actor.UserId;
            council.Status = CouncilStatus.Active;
            dbContext.AuditLogs.Add(NewAudit(actor, "START_DEFENSE_SESSION", nameof(DefenseSession), session.Id, null, session.StartedAt));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return MapState(session, council, isChairman: true);
    }

    public async Task<SubmittedScoreDto> SubmitScoreAsync(
        int sessionId,
        SubmitDefenseScoreCommand command,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(actor);

        var lecturerId = await GetCurrentLecturerIdAsync(actor.UserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Only lecturers assigned to a council can score a defense session.");

        var session = await GetSessionAsync(sessionId, cancellationToken);
        var councilMemberIds = await dbContext.CouncilMembers
            .Where(x => x.CouncilId == session.CouncilId)
            .Select(x => x.LecturerId)
            .ToArrayAsync(cancellationToken);

        rules.EnsureCouncilMember(lecturerId, councilMemberIds);
        rules.EnsureSessionStarted(session.StartedAt, session.IsLocked);
        rules.ValidateScore(command.ScoreValue);
        await EnsureStudentBelongsToDefenseProjectAsync(session.GroupId, command.StudentId, cancellationToken);

        var score = await dbContext.Scores.SingleOrDefaultAsync(
            x => x.DefenseSessionId == sessionId && x.ScorerId == lecturerId &&
                 x.StudentId == command.StudentId && x.ScoreType == command.ScoreType,
            cancellationToken);
        var oldValue = score?.ScoreValue;
        if (score is null)
        {
            score = new Score
            {
                DefenseSessionId = sessionId,
                ScorerId = lecturerId,
                StudentId = command.StudentId,
                ScoreType = command.ScoreType,
                ScoreValue = command.ScoreValue
            };
            dbContext.Scores.Add(score);
        }
        else
        {
            rules.EnsureSessionEditable(score.IsLocked);
            score.ScoreValue = command.ScoreValue;
            score.SubmittedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ScoreSubmissionHistories.Add(new ScoreSubmissionHistory
        {
            ScoreId = score.Id,
            DefenseSessionId = sessionId,
            ScorerId = lecturerId,
            SubmittedByUserId = actor.UserId,
            StudentId = command.StudentId,
            ScoreType = command.ScoreType,
            OldScoreValue = oldValue,
            NewScoreValue = command.ScoreValue,
            IpAddress = actor.IpAddress,
            UserAgent = actor.UserAgent,
            TrustReason = "JWT user matched an assigned council member, target student belonged to the council group list, and the chairman had started the session."
        });
        dbContext.AuditLogs.Add(NewAudit(actor, "SUBMIT_SCORE", nameof(Score), score.Id, oldValue, command.ScoreValue));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubmittedScoreDto(
            sessionId,
            score.Id,
            lecturerId,
            command.StudentId,
            command.ScoreType,
            command.ScoreValue,
            score.SubmittedAt);
    }

    public async Task<DefenseSessionStateDto> CloseAsync(
        int sessionId,
        ActorContext actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var lecturerId = await GetCurrentLecturerIdAsync(actor.UserId, cancellationToken)
            ?? throw new UnauthorizedAccessException("Only lecturers assigned to a council can close defense scoring.");

        var session = await GetSessionAsync(sessionId, cancellationToken);
        var council = await dbContext.Councils.SingleAsync(x => x.Id == session.CouncilId, cancellationToken);

        rules.EnsureChairman(lecturerId, council.ChairmanId);
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

        dbContext.AuditLogs.Add(NewAudit(actor, "LOCK_DEFENSE_SESSION", nameof(DefenseSession), session.Id, false, true));
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapState(session, council, isChairman: true);
    }

    private async Task<DefenseSession> GetSessionAsync(int sessionId, CancellationToken cancellationToken)
    {
        return await dbContext.DefenseSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new KeyNotFoundException("Defense session does not exist.");
    }

    private Task<int?> GetCurrentLecturerIdAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Lecturers
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task EnsureStudentBelongsToDefenseProjectAsync(
        int groupId,
        int studentId,
        CancellationToken cancellationToken)
    {
        var isValidTarget = await dbContext.Students
            .Where(student => student.Id == studentId && student.GroupId.HasValue)
            .AnyAsync(student => student.GroupId == groupId, cancellationToken);

        if (!isValidTarget)
        {
            throw new BusinessRuleException("The selected student does not belong to the project assigned to this defense session.");
        }
    }

    private static DefenseSessionStateDto MapState(DefenseSession session, Council council, bool isChairman) =>
        new(session.Id, session.Code, session.DefenseRoundId, session.CouncilId, council.Code, session.GroupId, session.SessionDate, session.Slot, session.Room, session.StartedAt, session.EndedAt, session.IsLocked, isChairman);

    private static AuditLog NewAudit(
        ActorContext actor,
        string action,
        string entityType,
        int entityId,
        object? oldValue,
        object? newValue) =>
        new()
        {
            UserId = actor.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = JsonSerializer.Serialize(oldValue),
            NewValue = JsonSerializer.Serialize(newValue),
            IpAddress = actor.IpAddress,
            UserAgent = actor.UserAgent
        };
}

public sealed record ActorContext(int UserId, string? IpAddress, string? UserAgent);

public sealed record SubmitDefenseScoreCommand(int StudentId, ScoreType ScoreType, decimal ScoreValue);

public sealed record DefenseSessionStateDto(
    int SessionId,
    string SessionCode,
    int DefenseRoundId,
    int CouncilId,
    string CouncilCode,
    int GroupId,
    DateTime SessionDate,
    int Slot,
    string Room,
    DateTime? StartedAt,
    DateTime? EndedAt,
    bool IsLocked,
    bool IsChairman);

public sealed record SubmittedScoreDto(
    int SessionId,
    int ScoreId,
    int ScorerId,
    int StudentId,
    ScoreType ScoreType,
    decimal ScoreValue,
    DateTime SubmittedAt);

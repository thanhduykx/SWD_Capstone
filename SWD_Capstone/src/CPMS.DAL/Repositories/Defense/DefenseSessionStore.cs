using System.Globalization;
using System.Text.Json;
using CPMS.BLL.Defense.Sessions;
using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore.Storage;

namespace CPMS.DAL.Repositories.Defense;

public sealed class DefenseSessionStore(CpmsDbContext dbContext, IHostEnvironment environment)
    : IDefenseSessionStore
{
    public async Task<IReadOnlyList<DefenseSessionListResult>> GetAllAsync(
        int? semesterId, CancellationToken cancellationToken)
    {
        IQueryable<DefenseSession> query = dbContext.DefenseSessions.AsNoTracking();
        if (semesterId.HasValue)
            query = query.Where(x => dbContext.DefenseRounds.Any(round =>
                round.Id == x.DefenseRoundId && round.SemesterId == semesterId.Value));
        var rows = await query.OrderBy(x => x.SessionDate).ThenBy(x => x.Slot)
            .Select(x => new
            {
                x.Id,
                x.Code,
                RoundName = dbContext.DefenseRounds.Where(round => round.Id == x.DefenseRoundId)
                    .Select(round => round.Name).FirstOrDefault() ?? "Defense",
                RoundType = dbContext.DefenseRounds.Where(round => round.Id == x.DefenseRoundId)
                    .Select(round => round.Type).FirstOrDefault(),
                GroupCode = dbContext.CapstoneGroups.Where(group => group.Id == x.GroupId)
                    .Select(group => group.Code).FirstOrDefault() ?? $"Group #{x.GroupId}",
                ProjectTitle = dbContext.CapstoneGroups.Where(group => group.Id == x.GroupId)
                    .Join(dbContext.Topics, group => group.TopicId, topic => topic.Id, (_, topic) => topic.NameEn)
                    .FirstOrDefault() ?? "Capstone Project System",
                x.SessionDate,
                x.Slot,
                x.Room,
                Reviewers = dbContext.CouncilMembers.Where(member => member.CouncilId == x.CouncilId)
                    .Join(dbContext.Lecturers, member => member.LecturerId, lecturer => lecturer.Id,
                        (_, lecturer) => lecturer.FullName).ToArray(),
                x.EndedAt,
                Defense1Result = dbContext.GroupResults.Where(result => result.GroupId == x.GroupId)
                    .Select(result => result.Defense1Result).FirstOrDefault(),
                Defense2Result = dbContext.GroupResults.Where(result => result.GroupId == x.GroupId)
                    .Select(result => result.Defense2Result).FirstOrDefault(),
                AverageScore = dbContext.Scores.Where(score => score.DefenseSessionId == x.Id)
                    .Select(score => (decimal?)score.ScoreValue).Average()
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x =>
        {
            FinalResult? result = x.RoundType == CouncilType.Defense2
                ? x.Defense2Result
                : x.Defense1Result;
            string? resultText = x.EndedAt is not null && result is not null && result != FinalResult.Pending
                ? result.Value.ToString()
                : null;
            return new DefenseSessionListResult(
                x.Id,
                x.Code,
                x.RoundName,
                x.GroupCode,
                x.ProjectTitle,
                x.SessionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                x.Slot,
                x.Room,
                x.Reviewers,
                x.EndedAt is not null ? "Completed" : "Scheduled",
                resultText,
                x.AverageScore ?? 0m,
                string.Empty);
        }).ToArray();
    }

    public async Task<DefenseSessionAccessState?> ResolveAsync(
        string code, CancellationToken cancellationToken)
    {
        IQueryable<DefenseSession> query = dbContext.DefenseSessions.AsQueryable();
        if (int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            query = query.Where(x => x.Id == id || x.Code == code ||
                dbContext.Councils.Any(c => c.Id == x.CouncilId && c.Code == code));
        else
            query = query.Where(x => x.Code == code ||
                dbContext.Councils.Any(c => c.Id == x.CouncilId && c.Code == code));
        var sessionId = await query.Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
        return sessionId.HasValue ? await FindSessionAsync(sessionId.Value, cancellationToken) : null;
    }

    public async Task<DefenseSessionAccessState?> FindSessionAsync(
        int sessionId, CancellationToken cancellationToken)
    {
        var row = await dbContext.DefenseSessions.Where(x => x.Id == sessionId)
            .Select(x => new
            {
                x.Id, x.Code, x.DefenseRoundId, x.CouncilId, x.GroupId, x.SessionDate,
                x.Slot, x.Room, x.StartedAt, x.EndedAt, x.IsLocked,
                CouncilCode = dbContext.Councils.Where(c => c.Id == x.CouncilId).Select(c => c.Code).Single(),
                ChairmanId = dbContext.Councils.Where(c => c.Id == x.CouncilId).Select(c => c.ChairmanId).Single()
            }).SingleOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var members = await dbContext.CouncilMembers.Where(x => x.CouncilId == row.CouncilId)
            .Select(x => x.LecturerId).ToArrayAsync(cancellationToken);
        var students = await dbContext.Students.AsNoTracking()
            .Where(x => x.GroupId == row.GroupId)
            .OrderBy(x => x.Code)
            .Select(x => new DefenseSessionStudentResult(x.Id, x.Code, x.FullName))
            .ToArrayAsync(cancellationToken);
        return new DefenseSessionAccessState(
            row.Id, row.Code, row.DefenseRoundId, row.CouncilId, row.CouncilCode, row.ChairmanId,
            members, row.GroupId, row.SessionDate, row.Slot, row.Room,
            row.StartedAt, row.EndedAt, row.IsLocked, students);
    }

    public Task<DefenseLecturerState?> FindLecturerAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Lecturers.Where(x => x.UserId == userId)
            .Select(x => new DefenseLecturerState(x.Id, x.Code, x.FullName))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<DefenseSessionAccessState> StartAsync(
        int sessionId, DefenseSessionActor actor, DateTime startedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        DefenseSession session = await dbContext.DefenseSessions.SingleAsync(x => x.Id == sessionId, cancellationToken);
        session.StartedAt = startedAt;
        session.StartedById = actor.UserId;
        Council council = await dbContext.Councils.SingleAsync(x => x.Id == session.CouncilId, cancellationToken);
        council.Status = CouncilStatus.Active;
        dbContext.AuditLogs.Add(NewAudit(
            actor, "START_DEFENSE_SESSION", nameof(DefenseSession), session.Id, null, startedAt));
        await dbContext.SaveChangesAsync(cancellationToken);
        return (await FindSessionAsync(sessionId, cancellationToken))!;
    }

    public Task<bool> StudentBelongsToGroupAsync(
        int studentId, int groupId, CancellationToken cancellationToken) =>
        dbContext.Students.AnyAsync(x => x.Id == studentId && x.GroupId == groupId, cancellationToken);

    public async Task<ExistingDefenseScore> FindScoreAsync(
        int sessionId,
        int lecturerId,
        int studentId,
        ScoreType scoreType,
        CancellationToken cancellationToken)
    {
        var score = await dbContext.Scores.Where(x =>
                x.DefenseSessionId == sessionId && x.ScorerId == lecturerId &&
                x.StudentId == studentId && x.ScoreType == scoreType)
            .Select(x => new { x.Id, x.ScoreValue, x.IsLocked })
            .SingleOrDefaultAsync(cancellationToken);
        return score is null ? new ExistingDefenseScore(null, null, false) :
            new ExistingDefenseScore(score.Id, score.ScoreValue, score.IsLocked);
    }

    public async Task<SubmittedDefenseScoreResult> SaveScoreAsync(
        int sessionId,
        int lecturerId,
        SubmitDefenseScoreData input,
        ExistingDefenseScore existing,
        DefenseSessionActor actor,
        DateTime submittedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(actor);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        Score score;
        if (existing.Id.HasValue)
        {
            score = await dbContext.Scores.SingleAsync(x => x.Id == existing.Id.Value, cancellationToken);
            score.ScoreValue = input.ScoreValue;
            score.SubmittedAt = submittedAt;
        }
        else
        {
            score = new Score
            {
                DefenseSessionId = sessionId, ScorerId = lecturerId, StudentId = input.StudentId,
                ScoreType = input.ScoreType, ScoreValue = input.ScoreValue, SubmittedAt = submittedAt
            };
            dbContext.Scores.Add(score);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.ScoreSubmissionHistories.Add(new ScoreSubmissionHistory
        {
            ScoreId = score.Id,
            DefenseSessionId = sessionId,
            ScorerId = lecturerId,
            SubmittedByUserId = actor.UserId,
            StudentId = input.StudentId,
            ScoreType = input.ScoreType,
            OldScoreValue = existing.ScoreValue,
            NewScoreValue = input.ScoreValue,
            IpAddress = actor.IpAddress,
            UserAgent = actor.UserAgent,
            TrustReason = "JWT user matched an assigned council member, target student belonged to the council group list, and the chairman had started the session."
        });
        dbContext.AuditLogs.Add(NewAudit(
            actor, "SUBMIT_SCORE", nameof(Score), score.Id, existing.ScoreValue, input.ScoreValue));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SubmittedDefenseScoreResult(
            sessionId, score.Id, lecturerId, input.StudentId, input.ScoreType, input.ScoreValue, score.SubmittedAt);
    }

    public async Task<DefenseSessionAccessState> CloseAsync(
        int sessionId, DefenseSessionActor actor, DateTime endedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        DefenseSession session = await dbContext.DefenseSessions.SingleAsync(x => x.Id == sessionId, cancellationToken);
        session.IsLocked = true;
        session.EndedAt = endedAt;
        Council council = await dbContext.Councils.SingleAsync(x => x.Id == session.CouncilId, cancellationToken);
        council.Status = CouncilStatus.Closed;
        await dbContext.Scores.Where(x => x.DefenseSessionId == sessionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsLocked, true), cancellationToken);
        dbContext.AuditLogs.Add(NewAudit(
            actor, "LOCK_DEFENSE_SESSION", nameof(DefenseSession), session.Id, false, true));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await FindSessionAsync(sessionId, cancellationToken))!;
    }

    public async Task<IReadOnlyList<DefenseEvidenceResult>> GetEvidencesAsync(
        int sessionId, CancellationToken cancellationToken) =>
        await dbContext.DefenseEvidences.Where(x => x.DefenseSessionId == sessionId)
            .OrderByDescending(x => x.CapturedAt)
            .Select(x => new DefenseEvidenceResult(
                x.Id, x.DefenseSessionId, x.CapturedByLecturerId, x.FileName, x.FilePath,
                x.ContentType, x.FileSize, x.Note, x.CapturedAt))
            .ToListAsync(cancellationToken);

    public async Task<DefenseEvidenceResult> SaveEvidenceAsync(
        int sessionId,
        int lecturerId,
        UploadDefenseEvidenceData input,
        DefenseSessionActor actor,
        DateTime capturedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(actor);
        var extension = input.ContentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => ".jpg"
        };
        var fileName = $"{capturedAt:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
        var relativeDirectory = Path.Combine("evidence", sessionId.ToString(CultureInfo.InvariantCulture));
        var root = Path.Combine(environment.ContentRootPath, "wwwroot", relativeDirectory);
        Directory.CreateDirectory(root);
        var physicalPath = Path.Combine(root, fileName);
        try
        {
            await using (FileStream output = File.Create(physicalPath))
                await input.Content.CopyToAsync(output, cancellationToken);
            var publicPath = $"/evidence/{sessionId}/{fileName}";
            var entity = new DefenseEvidence
            {
                DefenseSessionId = sessionId,
                CapturedByUserId = actor.UserId,
                CapturedByLecturerId = lecturerId,
                FileName = fileName,
                FilePath = publicPath,
                ContentType = input.ContentType,
                FileSize = input.FileSize,
                Note = input.Note,
                CapturedAt = capturedAt,
                IpAddress = actor.IpAddress,
                UserAgent = actor.UserAgent
            };
            dbContext.DefenseEvidences.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.AuditLogs.Add(NewAudit(
                actor, "CAPTURE_DEFENSE_EVIDENCE", nameof(DefenseEvidence), entity.Id, null,
                new { entity.DefenseSessionId, entity.FilePath, entity.Note }));
            await dbContext.SaveChangesAsync(cancellationToken);
            return new DefenseEvidenceResult(
                entity.Id, entity.DefenseSessionId, entity.CapturedByLecturerId, entity.FileName,
                entity.FilePath, entity.ContentType, entity.FileSize, entity.Note, entity.CapturedAt);
        }
        catch
        {
            if (File.Exists(physicalPath)) File.Delete(physicalPath);
            throw;
        }
    }

    private static AuditLog NewAudit(
        DefenseSessionActor actor, string action, string entityType, int entityId,
        object? oldValue, object? newValue) => new()
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

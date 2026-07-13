using CPMS.Core.Enums;

namespace CPMS.BLL.Defense.Sessions;

public sealed record DefenseSessionActor(int UserId, string? IpAddress, string? UserAgent);
public sealed record DefenseSessionListResult(
    int Id,
    string Code,
    string RoundName,
    string GroupCode,
    string ProjectTitle,
    string SessionDate,
    int Slot,
    string Room,
    IReadOnlyList<string> Reviewers,
    string Status,
    string? Result,
    decimal AverageScore,
    string Notes);
public sealed record DefenseSessionStateResult(
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
public sealed record SubmitDefenseScoreData(int StudentId, ScoreType ScoreType, decimal ScoreValue);
public sealed record SubmittedDefenseScoreResult(
    int SessionId, int ScoreId, int ScorerId, int StudentId,
    ScoreType ScoreType, decimal ScoreValue, DateTime SubmittedAt);
public sealed record DefenseEvidenceResult(
    int Id,
    int DefenseSessionId,
    int CapturedByLecturerId,
    string FileName,
    string FilePath,
    string ContentType,
    long FileSize,
    string? Note,
    DateTime CapturedAt);
public sealed record UploadDefenseEvidenceData(
    string OriginalFileName, string ContentType, long FileSize, string? Note, Stream Content);
public sealed record DefenseJoinResult(
    DefenseSessionStateResult Session,
    int LecturerId,
    string LecturerCode,
    string LecturerName);

public interface IDefenseSessionService
{
    Task<IReadOnlyList<DefenseSessionListResult>> GetAllAsync(
        int? semesterId, CancellationToken cancellationToken);
    Task<DefenseSessionStateResult> ResolveAsync(
        string code, int userId, CancellationToken cancellationToken);
    Task<DefenseSessionStateResult> StartAsync(
        int sessionId, DefenseSessionActor actor, CancellationToken cancellationToken);
    Task<SubmittedDefenseScoreResult> SubmitScoreAsync(
        int sessionId, SubmitDefenseScoreData score, DefenseSessionActor actor, CancellationToken cancellationToken);
    Task<IReadOnlyList<DefenseEvidenceResult>> GetEvidencesAsync(
        int sessionId, int userId, CancellationToken cancellationToken);
    Task<DefenseEvidenceResult> UploadEvidenceAsync(
        int sessionId, UploadDefenseEvidenceData evidence,
        DefenseSessionActor actor, CancellationToken cancellationToken);
    Task<DefenseSessionStateResult> CloseAsync(
        int sessionId, DefenseSessionActor actor, CancellationToken cancellationToken);
    Task<DefenseJoinResult> JoinAsync(int sessionId, int userId, CancellationToken cancellationToken);
}

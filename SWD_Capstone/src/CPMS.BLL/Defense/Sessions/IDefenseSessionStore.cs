using CPMS.BLL.Enums;

namespace CPMS.BLL.Defense.Sessions;

public sealed record DefenseSessionAccessState(
    int Id,
    string Code,
    int DefenseRoundId,
    int CouncilId,
    string CouncilCode,
    int ChairmanId,
    IReadOnlyList<int> MemberIds,
    int GroupId,
    DateTime SessionDate,
    int Slot,
    string Room,
    DateTime? StartedAt,
    DateTime? EndedAt,
    bool IsLocked);
public sealed record DefenseLecturerState(int Id, string Code, string FullName);
public sealed record ExistingDefenseScore(int? Id, decimal? ScoreValue, bool IsLocked);

public interface IDefenseSessionStore
{
    Task<IReadOnlyList<DefenseSessionListResult>> GetAllAsync(
        int? semesterId, CancellationToken cancellationToken);
    Task<DefenseSessionAccessState?> ResolveAsync(string code, CancellationToken cancellationToken);
    Task<DefenseSessionAccessState?> FindSessionAsync(int sessionId, CancellationToken cancellationToken);
    Task<DefenseLecturerState?> FindLecturerAsync(int userId, CancellationToken cancellationToken);
    Task<DefenseSessionAccessState> StartAsync(
        int sessionId, DefenseSessionActor actor, DateTime startedAt, CancellationToken cancellationToken);
    Task<bool> StudentBelongsToGroupAsync(int studentId, int groupId, CancellationToken cancellationToken);
    Task<ExistingDefenseScore> FindScoreAsync(
        int sessionId, int lecturerId, int studentId, ScoreType scoreType, CancellationToken cancellationToken);
    Task<SubmittedDefenseScoreResult> SaveScoreAsync(
        int sessionId,
        int lecturerId,
        SubmitDefenseScoreData input,
        ExistingDefenseScore existing,
        DefenseSessionActor actor,
        DateTime submittedAt,
        CancellationToken cancellationToken);
    Task<DefenseSessionAccessState> CloseAsync(
        int sessionId, DefenseSessionActor actor, DateTime endedAt, CancellationToken cancellationToken);
    Task<IReadOnlyList<DefenseEvidenceResult>> GetEvidencesAsync(
        int sessionId, CancellationToken cancellationToken);
    Task<DefenseEvidenceResult> SaveEvidenceAsync(
        int sessionId,
        int lecturerId,
        UploadDefenseEvidenceData input,
        DefenseSessionActor actor,
        DateTime capturedAt,
        CancellationToken cancellationToken);
}

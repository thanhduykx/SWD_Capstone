using CPMS.BLL.Exceptions;
using CPMS.BLL.Services;

namespace CPMS.BLL.Defense.Sessions;

public sealed class DefenseSessionService(IDefenseSessionStore store)
    : IDefenseSessionService
{
    private const long MaxEvidenceBytes = 5_242_880;
    private static readonly HashSet<string> AllowedEvidenceTypes =
        ["image/jpeg", "image/png", "image/gif", "image/webp"];

    public Task<IReadOnlyList<DefenseSessionListResult>> GetAllAsync(
        int? semesterId, CancellationToken cancellationToken) =>
        store.GetAllAsync(semesterId is > 0 ? semesterId : null, cancellationToken);

    public async Task<DefenseSessionStateResult> ResolveAsync(
        string code, int userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new BusinessRuleException("Defense code is required.");
        DefenseLecturerState lecturer = await RequireLecturerAsync(userId, cancellationToken);
        DefenseSessionAccessState session = await store.ResolveAsync(code.Trim(), cancellationToken)
            ?? throw new KeyNotFoundException("Defense code was not found.");
        AssignmentRules.EnsureCouncilMember(lecturer.Id, session.MemberIds);
        return Map(session, lecturer.Id == session.ChairmanId);
    }

    public async Task<DefenseSessionStateResult> StartAsync(
        int sessionId, DefenseSessionActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        DefenseLecturerState lecturer = await RequireLecturerAsync(actor.UserId, cancellationToken);
        DefenseSessionAccessState session = await RequireSessionAsync(sessionId, cancellationToken);
        AssignmentRules.EnsureChairman(lecturer.Id, session.ChairmanId);
        AssignmentRules.EnsureSessionEditable(session.IsLocked);
        if (session.StartedAt is null)
            session = await store.StartAsync(sessionId, actor, DateTime.UtcNow, cancellationToken);
        return Map(session, true);
    }

    public async Task<IReadOnlyList<DefenseCommentResult>> GetCommentsAsync(
        int sessionId, int userId, CancellationToken cancellationToken)
    {
        DefenseLecturerState lecturer = await RequireLecturerAsync(userId, cancellationToken);
        DefenseSessionAccessState session = await RequireSessionAsync(sessionId, cancellationToken);
        AssignmentRules.EnsureCouncilMember(lecturer.Id, session.MemberIds);
        return await store.GetCommentsAsync(sessionId, lecturer.Id, cancellationToken);
    }

    public async Task<DefenseCommentResult> SubmitCommentAsync(
        int sessionId,
        SubmitDefenseCommentData comment,
        DefenseSessionActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comment);
        ArgumentNullException.ThrowIfNull(actor);
        string content = comment.Content?.Trim() ?? string.Empty;
        if (content.Length is < 1 or > 2000)
            throw new BusinessRuleException("A defense comment is required and cannot exceed 2000 characters.");
        DefenseLecturerState lecturer = await RequireLecturerAsync(actor.UserId, cancellationToken);
        DefenseSessionAccessState session = await RequireSessionAsync(sessionId, cancellationToken);
        AssignmentRules.EnsureCouncilMember(lecturer.Id, session.MemberIds);
        AssignmentRules.EnsureSessionStarted(session.StartedAt, session.IsLocked);
        return await store.SaveCommentAsync(
            sessionId, lecturer.Id, comment with { Content = content }, actor, DateTime.UtcNow, cancellationToken);
    }

    public async Task<IReadOnlyList<DefenseEvidenceResult>> GetEvidencesAsync(
        int sessionId, int userId, CancellationToken cancellationToken)
    {
        DefenseLecturerState lecturer = await RequireLecturerAsync(userId, cancellationToken);
        DefenseSessionAccessState session = await RequireSessionAsync(sessionId, cancellationToken);
        AssignmentRules.EnsureCouncilMember(lecturer.Id, session.MemberIds);
        return await store.GetEvidencesAsync(sessionId, cancellationToken);
    }

    public async Task<DefenseEvidenceResult> UploadEvidenceAsync(
        int sessionId,
        UploadDefenseEvidenceData evidence,
        DefenseSessionActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(actor);
        if (evidence.FileSize is <= 0 or > MaxEvidenceBytes)
            throw new BusinessRuleException("Evidence image is required and cannot exceed 5 MB.");
        if (!AllowedEvidenceTypes.Contains(evidence.ContentType.ToLowerInvariant()))
            throw new BusinessRuleException("Only JPEG, PNG, GIF, or WebP evidence files are allowed.");
        if (evidence.Note?.Length > 1000)
            throw new BusinessRuleException("Evidence note cannot exceed 1000 characters.");
        DefenseLecturerState lecturer = await RequireLecturerAsync(actor.UserId, cancellationToken);
        DefenseSessionAccessState session = await RequireSessionAsync(sessionId, cancellationToken);
        AssignmentRules.EnsureCouncilMember(lecturer.Id, session.MemberIds);
        AssignmentRules.EnsureSessionStarted(session.StartedAt, session.IsLocked);
        return await store.SaveEvidenceAsync(
            sessionId, lecturer.Id, evidence with { Note = evidence.Note?.Trim() },
            actor, DateTime.UtcNow, cancellationToken);
    }

    public async Task<DefenseSessionStateResult> CloseAsync(
        int sessionId, DefenseSessionActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        DefenseLecturerState lecturer = await RequireLecturerAsync(actor.UserId, cancellationToken);
        DefenseSessionAccessState session = await RequireSessionAsync(sessionId, cancellationToken);
        AssignmentRules.EnsureChairman(lecturer.Id, session.ChairmanId);
        AssignmentRules.EnsureSessionStarted(session.StartedAt, session.IsLocked);
        AssignmentRules.EnsureSessionEditable(session.IsLocked);
        return Map(await store.CloseAsync(sessionId, actor, DateTime.UtcNow, cancellationToken), true);
    }

    public async Task<DefenseJoinResult> JoinAsync(
        int sessionId, int userId, CancellationToken cancellationToken)
    {
        DefenseLecturerState lecturer = await RequireLecturerAsync(userId, cancellationToken);
        DefenseSessionAccessState session = await RequireSessionAsync(sessionId, cancellationToken);
        AssignmentRules.EnsureCouncilMember(lecturer.Id, session.MemberIds);
        return new DefenseJoinResult(
            Map(session, lecturer.Id == session.ChairmanId), lecturer.Id, lecturer.Code, lecturer.FullName);
    }

    private async Task<DefenseLecturerState> RequireLecturerAsync(
        int userId, CancellationToken cancellationToken) =>
        await store.FindLecturerAsync(userId, cancellationToken)
        ?? throw new UnauthorizedAccessException("Only lecturers assigned to a council can access the defense room.");
    private async Task<DefenseSessionAccessState> RequireSessionAsync(
        int sessionId, CancellationToken cancellationToken) =>
        await store.FindSessionAsync(sessionId, cancellationToken)
        ?? throw new KeyNotFoundException("Defense session does not exist.");
    private static DefenseSessionStateResult Map(DefenseSessionAccessState x, bool isChairman) =>
        new(x.Id, x.Code, x.DefenseRoundId, x.CouncilId, x.CouncilCode, x.GroupId,
            x.SessionDate, x.Slot, x.Room, x.StartedAt, x.EndedAt, x.IsLocked, isChairman, x.Students);
}

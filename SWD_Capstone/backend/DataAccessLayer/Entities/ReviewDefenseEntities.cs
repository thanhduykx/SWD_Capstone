using CPMS.Core.Enums;

namespace CPMS.Core.Entities;

public sealed class ReviewSession
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public int SemesterId { get; set; }
    public int DayOfWeek { get; set; }
    public int Slot { get; set; }
    public required string Room { get; set; }
    public ReviewType Type { get; set; }
    public int? Reviewer1Id { get; set; }
    public int? Reviewer2Id { get; set; }
    public DateTime SessionDate { get; set; }
}

public sealed class GroupReviewSlot
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int GroupId { get; set; }
    public int GroupPosition { get; set; }
    public ReviewResult? Result { get; set; }
    public string? Notes { get; set; }
    public bool ConflictFlag { get; set; }
}

public sealed class Council
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public int SemesterId { get; set; }
    public int ChairmanId { get; set; }
    public int SecretaryId { get; set; }
    public CouncilType Type { get; set; }
    public CouncilStatus Status { get; set; } = CouncilStatus.Pending;
}

public sealed class CouncilMember
{
    public int CouncilId { get; set; }
    public int LecturerId { get; set; }
    public CouncilMemberRole Role { get; set; }
}

public sealed class CouncilGroup
{
    public int Id { get; set; }
    public int CouncilId { get; set; }
    public int GroupId { get; set; }
    public int Position { get; set; }
    public bool ConflictFlag { get; set; }
}

public sealed class DefenseSession
{
    public int Id { get; set; }
    public int CouncilId { get; set; }
    public DateTime SessionDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? StartedById { get; set; }
    public bool IsLocked { get; set; }
}

public sealed class ScoreSubmissionHistory
{
    public long Id { get; set; }
    public int? ScoreId { get; set; }
    public int DefenseSessionId { get; set; }
    public int ScorerId { get; set; }
    public int SubmittedByUserId { get; set; }
    public int StudentId { get; set; }
    public ScoreType ScoreType { get; set; }
    public decimal? OldScoreValue { get; set; }
    public decimal NewScoreValue { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsTrusted { get; set; } = true;
    public required string TrustReason { get; set; }
}

public sealed class Score
{
    public int Id { get; set; }
    public int DefenseSessionId { get; set; }
    public int ScorerId { get; set; }
    public int StudentId { get; set; }
    public ScoreType ScoreType { get; set; }
    public decimal ScoreValue { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public bool IsLocked { get; set; }
}

public sealed class GroupResult
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int SemesterId { get; set; }
    public ReviewResult? Review1Result { get; set; }
    public ReviewResult? Review2Result { get; set; }
    public ReviewResult? Review3Result { get; set; }
    public ReviewResult? SupervisorResult { get; set; }
    public FinalResult? Defense1Result { get; set; }
    public FinalResult? Defense2Result { get; set; }
    public FinalResult FinalResult { get; set; } = FinalResult.Pending;
}

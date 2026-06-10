using CPMS.Core.Enums;

namespace CPMS.Core.Entities;

public sealed class ReviewAvailability
{
    public int Id { get; set; }
    public int SemesterId { get; set; }
    public int LecturerId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public int DayOfWeek { get; set; }
    public int Slot { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ReviewAvailabilitySubmission
{
    public int Id { get; set; }
    public int SemesterId { get; set; }
    public int LecturerId { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
}

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
    public ReviewSessionStatus Status { get; set; } = ReviewSessionStatus.Draft;
    public DateTime? PublishedAt { get; set; }
    public int? PublishedByTrainingDepartmentId { get; set; }
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

public sealed class ReviewChecklistSubmission
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int GroupId { get; set; }
    public int ReviewerId { get; set; }
    public ReviewType Type { get; set; }
    public ReviewSubmissionStatus Status { get; set; } = ReviewSubmissionStatus.Draft;
    public string? WorkProductVersion { get; set; }
    public string? WorkProductSize { get; set; }
    public decimal? EffortHours { get; set; }
    public string? ReviewerComment { get; set; }
    public string? Suggestion { get; set; }
    public string? ResultText { get; set; }
    public DateTime LastSavedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
}

public sealed class ReviewChecklistItemResponse
{
    public int Id { get; set; }
    public int SubmissionId { get; set; }
    public required string ItemKey { get; set; }
    public ReviewChecklistAnswer? Answer { get; set; }
    public string? Comment { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ReviewSchedulePublication
{
    public int Id { get; set; }
    public int SemesterId { get; set; }
    public ReviewType ReviewType { get; set; }
    public DateOnly WeekStartDate { get; set; }
    public int PublishedByTrainingDepartmentId { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public required string Subject { get; set; }
    public required string Body { get; set; }
}

public sealed class EmailDeliveryLog
{
    public int Id { get; set; }
    public int? PublicationId { get; set; }
    public int? RecipientUserId { get; set; }
    public required string RecipientEmail { get; set; }
    public required string Subject { get; set; }
    public EmailDeliveryStatus Status { get; set; } = EmailDeliveryStatus.Pending;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}

public sealed class Council
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public int SemesterId { get; set; }
    public int? ManagedByTrainingDepartmentId { get; set; }
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
    public required string Code { get; set; }
    public int DefenseRoundId { get; set; }
    public int CouncilId { get; set; }
    public int GroupId { get; set; }
    public DateTime SessionDate { get; set; }
    public int Slot { get; set; }
    public required string Room { get; set; }
    public int? AssignedByTrainingDepartmentId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? StartedById { get; set; }
    public bool IsLocked { get; set; }
}

public sealed class DefenseRound
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int SemesterId { get; set; }
    public CouncilType Type { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DefenseRoundStatus Status { get; set; } = DefenseRoundStatus.Draft;
    public int CreatedByTrainingDepartmentId { get; set; }
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

public sealed class DefenseEvidence
{
    public int Id { get; set; }
    public int DefenseSessionId { get; set; }
    public int CapturedByUserId { get; set; }
    public int CapturedByLecturerId { get; set; }
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public required string ContentType { get; set; }
    public long FileSize { get; set; }
    public string? Note { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
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

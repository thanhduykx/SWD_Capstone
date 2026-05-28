using CPMS.Core.Common;
using CPMS.Core.Enums;

namespace CPMS.Core.Entities;

public sealed class CapstoneDocument : AuditableEntity
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int SyllabusId { get; set; }
    public int UploadedById { get; set; }
    public DocumentType DocType { get; set; }
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public long FileSize { get; set; }
    public int VersionNo { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Submitted;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
}

public sealed class EvaluationReport
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int ReviewedById { get; set; }
    public decimal OverallScore { get; set; }
    public decimal MatchPercentage { get; set; }
    public string? GapAnalysisSummary { get; set; }
    public EvaluationTriggerType TriggerType { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class EvaluationDetail
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public int CloId { get; set; }
    public decimal Score { get; set; }
    public decimal MaxScore { get; set; }
    public decimal MatchPercentage { get; set; }
    public string? Feedback { get; set; }
    public string? MissingEvidence { get; set; }
}

public sealed class InlineComment : AuditableEntity
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int AuthorId { get; set; }
    public int ParagraphIndex { get; set; }
    public required string Content { get; set; }
    public int? ParentCommentId { get; set; }
    public CommentStatus Status { get; set; } = CommentStatus.Open;
}

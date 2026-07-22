using CPMS.DAL.Common;
using CPMS.BLL.Enums;

namespace CPMS.DAL.Entities;

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

public sealed class DocumentPayload
{
    public required string StorageKey { get; set; }
    public required byte[] Content { get; set; }
    public required string ContentType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class InlineComment : AuditableEntity
{
    public int Id { get; set; }
    public int DocumentId { get; set; }
    public int AuthorId { get; set; }
    public int ParagraphIndex { get; set; }
    public required string Content { get; set; }
    public string? Reference { get; set; }
    public int? ParentCommentId { get; set; }
    public CommentStatus Status { get; set; } = CommentStatus.Open;
}

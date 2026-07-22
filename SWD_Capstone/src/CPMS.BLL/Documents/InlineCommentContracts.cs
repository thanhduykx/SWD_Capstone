using CPMS.BLL.Enums;

namespace CPMS.BLL.Documents;

public sealed record InlineCommentData(
    int Id,
    int DocumentId,
    int AuthorId,
    string AuthorName,
    string Content,
    string? Reference,
    int? ParagraphIndex,
    CommentStatus Status,
    DateTime CreatedAt);

public sealed record CreateInlineCommentData(string Content, string? Reference, int? ParagraphIndex);

public interface IInlineCommentStore
{
    Task<IReadOnlyList<InlineCommentData>> ListAsync(int documentId, CancellationToken cancellationToken);
    Task<InlineCommentData> CreateAsync(
        int documentId, int authorId, CreateInlineCommentData input, CancellationToken cancellationToken);
}

public interface IInlineCommentService
{
    Task<IReadOnlyList<InlineCommentData>> ListAsync(
        ProjectDocumentActor actor, int documentId, CancellationToken cancellationToken);
    Task<InlineCommentData> CreateAsync(
        ProjectDocumentActor actor, int documentId, CreateInlineCommentData input, CancellationToken cancellationToken);
}

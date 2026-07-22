namespace CPMS.API.Contracts.Documents;

public sealed record CreateInlineCommentRequest(string Content, string? Reference, int? ParagraphIndex);
public sealed record InlineCommentResponse(
    int Id,
    int DocumentId,
    int AuthorId,
    string AuthorName,
    string Content,
    string? Reference,
    int? ParagraphIndex,
    string Status,
    DateTime CreatedAt);

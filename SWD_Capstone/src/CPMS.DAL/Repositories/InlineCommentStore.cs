using CPMS.BLL.Documents;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using CPMS.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories;

public sealed class InlineCommentStore(CpmsDbContext dbContext) : IInlineCommentStore
{
    public async Task<IReadOnlyList<InlineCommentData>> ListAsync(
        int documentId, CancellationToken cancellationToken)
    {
        List<InlineComment> comments = await dbContext.InlineComments.AsNoTracking()
            .Where(x => x.DocumentId == documentId)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        int[] authorIds = comments.Select(x => x.AuthorId).Distinct().ToArray();
        Dictionary<int, string> names = await dbContext.Users.AsNoTracking()
            .Where(x => authorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Username, cancellationToken);
        foreach (var author in await dbContext.Lecturers.AsNoTracking()
                     .Where(x => authorIds.Contains(x.UserId))
                     .Select(x => new { x.UserId, x.FullName }).ToListAsync(cancellationToken))
            names[author.UserId] = author.FullName;
        return comments.Select(x => Map(x, names.GetValueOrDefault(x.AuthorId, "Tài khoản không xác định"))).ToArray();
    }

    public async Task<InlineCommentData> CreateAsync(
        int documentId, int authorId, CreateInlineCommentData input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var entity = new InlineComment
        {
            DocumentId = documentId,
            AuthorId = authorId,
            Content = input.Content,
            Reference = input.Reference,
            ParagraphIndex = input.ParagraphIndex ?? 0,
            Status = CommentStatus.Open
        };
        dbContext.InlineComments.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        string authorName = await dbContext.Lecturers.Where(x => x.UserId == authorId)
            .Select(x => x.FullName).SingleOrDefaultAsync(cancellationToken)
            ?? await dbContext.Users.Where(x => x.Id == authorId)
                .Select(x => x.Username).SingleAsync(cancellationToken);
        return Map(entity, authorName);
    }

    private static InlineCommentData Map(InlineComment x, string authorName) => new(
        x.Id, x.DocumentId, x.AuthorId, authorName, x.Content, x.Reference,
        x.ParagraphIndex, x.Status, x.CreatedAt);
}

using CPMS.BLL.Enums;

namespace CPMS.BLL.Documents;

public sealed class InlineCommentService(
    IInlineCommentStore store,
    IProjectDocumentStore documentStore) : IInlineCommentService
{
    public async Task<IReadOnlyList<InlineCommentData>> ListAsync(
        ProjectDocumentActor actor, int documentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        await EnsureDocumentAccessAsync(actor, documentId, cancellationToken);
        return await store.ListAsync(documentId, cancellationToken);
    }

    public async Task<InlineCommentData> CreateAsync(
        ProjectDocumentActor actor, int documentId, CreateInlineCommentData input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(input);
        if (actor.Role != UserRole.Lecturer)
            throw new UnauthorizedAccessException("Chỉ giảng viên được tạo nhận xét gắn với vị trí tài liệu.");
        await EnsureDocumentAccessAsync(actor, documentId, cancellationToken);
        var content = input.Content.Trim();
        if (content.Length is < 1 or > 4000)
            throw new ArgumentException("Nhận xét phải dài từ 1 đến 4000 ký tự.", nameof(input));
        var reference = string.IsNullOrWhiteSpace(input.Reference) ? null : input.Reference.Trim();
        if (reference?.Length > 200)
            throw new ArgumentException("Vị trí tham chiếu không được vượt quá 200 ký tự.", nameof(input));
        return await store.CreateAsync(documentId, actor.UserId,
            new CreateInlineCommentData(content, reference, input.ParagraphIndex), cancellationToken);
    }

    private async Task EnsureDocumentAccessAsync(
        ProjectDocumentActor actor, int documentId, CancellationToken cancellationToken)
    {
        var document = await documentStore.FindAsync(documentId, cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");
        bool allowed = actor.Role is UserRole.TrainingDepartment or UserRole.SystemAdministrator
            || actor.Role == UserRole.Student && await documentStore.IsStudentInGroupAsync(actor.UserId, document.GroupId, cancellationToken)
            || actor.Role == UserRole.Lecturer && await documentStore.CanLecturerAccessGroupAsync(actor.UserId, document.GroupId, cancellationToken);
        if (!allowed)
            throw new UnauthorizedAccessException("Bạn không có quyền xem nhận xét của tài liệu này.");
    }
}

using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using CPMS.BLL.AI.ProjectSuggestions;
using CPMS.BLL.Enums;
using CPMS.BLL.Common.Exceptions;

namespace CPMS.BLL.Documents;

public sealed class ProjectDocumentService(
    IProjectDocumentStore store,
    IProjectDocumentStorage storage,
    IProjectSuggestionService suggestionService) : IProjectDocumentService
{
    public const long MaxFileSize = 50 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".zip", ".txt" };

    public async Task<IReadOnlyList<ProjectDocumentData>> ListAsync(ProjectDocumentActor actor, int groupId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        await RequireGroupAccessAsync(actor, groupId, cancellationToken);
        return await store.ListAsync(groupId, cancellationToken);
    }

    public async Task<ProjectDocumentData> UploadAsync(ProjectDocumentActor actor, UploadProjectDocumentInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(input);
        if (actor.Role != UserRole.Student || !await store.IsStudentInGroupAsync(actor.UserId, input.GroupId, cancellationToken))
            throw new UnauthorizedAccessException("Bạn không được tải tài liệu cho nhóm này.");
        if (input.FileSize is <= 0 or > MaxFileSize) throw new ArgumentException("Tài liệu phải có dung lượng từ 1 byte đến 50 MB.", nameof(input));
        var fileName = Path.GetFileName(input.FileName);
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension)) throw new ArgumentException("Chỉ hỗ trợ PDF, DOCX, ZIP hoặc TXT.", nameof(input));
        if (!await HasValidSignatureAsync(input.Content, extension, cancellationToken)) throw new ArgumentException("Nội dung tệp không đúng với định dạng đã chọn.", nameof(input));
        if (input.Content.CanSeek) input.Content.Position = 0;

        var syllabusId = await store.FindActiveSyllabusIdAsync(cancellationToken)
            ?? throw new ConflictException("Chưa có đề cương đang hoạt động để lưu tài liệu.");
        var version = await store.GetNextVersionAsync(input.GroupId, input.DocType, cancellationToken);
        var objectPath = $"groups/{input.GroupId.ToString(CultureInfo.InvariantCulture)}/{input.DocType.ToString().ToLowerInvariant()}/v{version.ToString(CultureInfo.InvariantCulture)}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var stored = await storage.SaveAsync(objectPath, GetContentType(fileName), input.Content, cancellationToken);
        try
        {
            return await store.CreateAsync(new CreateProjectDocumentData(
                input.GroupId, syllabusId, actor.UserId, input.DocType, fileName,
                stored.StorageKey, input.FileSize, version), cancellationToken);
        }
        catch
        {
            await storage.DeleteAsync(stored.StorageKey, cancellationToken);
            throw;
        }
    }

    public async Task<ProjectDocumentDownload> OpenAsync(ProjectDocumentActor actor, int documentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var document = await RequireDocumentAsync(actor, documentId, cancellationToken);
        var stored = await storage.OpenReadAsync(document.StorageKey, cancellationToken)
            ?? throw new KeyNotFoundException("Tệp không còn tồn tại trong kho lưu trữ.");
        return new ProjectDocumentDownload(stored.Content, document.FileName, GetContentType(document.FileName));
    }

    public async Task<ProjectSuggestionResult> GenerateSuggestionsAsync(ProjectDocumentActor actor, int documentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Role != UserRole.Lecturer) throw new UnauthorizedAccessException("Chỉ giảng viên được sử dụng AI phân tích tài liệu.");
        var document = await RequireDocumentAsync(actor, documentId, cancellationToken);
        var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
        if (extension == ".zip") throw new ArgumentException("AI chỉ phân tích PDF, DOCX hoặc TXT; hãy giải nén trước khi tải lên.", nameof(documentId));
        var stored = await storage.OpenReadAsync(document.StorageKey, cancellationToken)
            ?? throw new KeyNotFoundException("Tệp không còn tồn tại trong kho lưu trữ.");
        await using var content = stored.Content;
        var projectName = await store.GetProjectNameAsync(document.GroupId, cancellationToken);
        if (extension == ".pdf")
        {
            using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, cancellationToken);
            return await suggestionService.GenerateFromDocumentAsync(
                new ProjectDocumentSuggestionInput(projectName, document.FileName, "application/pdf", buffer.ToArray()), cancellationToken);
        }
        var text = extension == ".docx" ? ExtractDocxText(content) : await ReadTextAsync(content, cancellationToken);
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Không trích xuất được nội dung văn bản từ tài liệu.", nameof(documentId));
        return await suggestionService.GenerateAsync(new ProjectSuggestionInput(projectName,
            text[..Math.Min(text.Length, ProjectSuggestionService.MaxProjectContentLength)]), cancellationToken);
    }

    private async Task<ProjectDocumentData> RequireDocumentAsync(ProjectDocumentActor actor, int id, CancellationToken ct)
    {
        var document = await store.FindAsync(id, ct) ?? throw new KeyNotFoundException("Không tìm thấy tài liệu.");
        await RequireGroupAccessAsync(actor, document.GroupId, ct);
        return document;
    }
    private async Task RequireGroupAccessAsync(ProjectDocumentActor actor, int groupId, CancellationToken ct)
    {
        var allowed = actor.Role is UserRole.TrainingDepartment or UserRole.SystemAdministrator
            || actor.Role == UserRole.Student && await store.IsStudentInGroupAsync(actor.UserId, groupId, ct)
            || actor.Role == UserRole.Lecturer && await store.CanLecturerAccessGroupAsync(actor.UserId, groupId, ct);
        if (!allowed) throw new UnauthorizedAccessException("Bạn không có quyền truy cập tài liệu của nhóm này.");
    }
    private static async Task<bool> HasValidSignatureAsync(Stream stream, string extension, CancellationToken ct)
    {
        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)) return true;
        var header = new byte[4]; var read = await stream.ReadAsync(header, ct);
        return read == 4 && (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            ? header.SequenceEqual("%PDF"u8.ToArray()) : header[0] == 0x50 && header[1] == 0x4B);
    }
    private static string GetContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    { ".pdf" => "application/pdf", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".zip" => "application/zip", _ => "text/plain; charset=utf-8" };
    private static async Task<string> ReadTextAsync(Stream content, CancellationToken ct)
    { using var reader = new StreamReader(content, Encoding.UTF8, true, leaveOpen: true); return await reader.ReadToEndAsync(ct); }
    private static string ExtractDocxText(Stream content)
    {
        using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("DOCX document.xml is missing.");
        using var stream = entry.Open(); var xml = XDocument.Load(stream);
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        return string.Join(' ', xml.Descendants(word + "t").Select(x => x.Value));
    }
}

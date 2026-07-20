using CPMS.BLL.AI.ProjectSuggestions;
using CPMS.BLL.Enums;

namespace CPMS.BLL.Documents;

public sealed record ProjectDocumentActor(int UserId, UserRole Role);
public sealed record ProjectDocumentData(
    int Id, int GroupId, DocumentType DocType, string FileName, string StorageKey,
    long FileSize, int VersionNo, DocumentStatus Status, DateTime UploadedAt, int UploadedById);
public sealed record CreateProjectDocumentData(
    int GroupId, int SyllabusId, int UploadedById, DocumentType DocType,
    string FileName, string StorageKey, long FileSize, int VersionNo);
public sealed record UploadProjectDocumentInput(
    int GroupId, DocumentType DocType, string FileName, long FileSize, Stream Content);
public sealed record ProjectDocumentDownload(Stream Content, string FileName, string ContentType);

public interface IProjectDocumentStore
{
    Task<bool> IsStudentInGroupAsync(int userId, int groupId, CancellationToken cancellationToken);
    Task<bool> CanLecturerAccessGroupAsync(int userId, int groupId, CancellationToken cancellationToken);
    Task<int?> FindActiveSyllabusIdAsync(CancellationToken cancellationToken);
    Task<int> GetNextVersionAsync(int groupId, DocumentType docType, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectDocumentData>> ListAsync(int groupId, CancellationToken cancellationToken);
    Task<ProjectDocumentData?> FindAsync(int documentId, CancellationToken cancellationToken);
    Task<ProjectDocumentData> CreateAsync(CreateProjectDocumentData input, CancellationToken cancellationToken);
    Task<string> GetProjectNameAsync(int groupId, CancellationToken cancellationToken);
}

public interface IProjectDocumentService
{
    Task<IReadOnlyList<ProjectDocumentData>> ListAsync(ProjectDocumentActor actor, int groupId, CancellationToken cancellationToken);
    Task<ProjectDocumentData> UploadAsync(ProjectDocumentActor actor, UploadProjectDocumentInput input, CancellationToken cancellationToken);
    Task<ProjectDocumentDownload> OpenAsync(ProjectDocumentActor actor, int documentId, CancellationToken cancellationToken);
    Task<ProjectSuggestionResult> GenerateSuggestionsAsync(ProjectDocumentActor actor, int documentId, CancellationToken cancellationToken);
}

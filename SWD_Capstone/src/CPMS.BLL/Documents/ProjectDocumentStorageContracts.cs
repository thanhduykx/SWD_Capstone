namespace CPMS.BLL.Documents;

public sealed record StoredProjectDocument(string StorageKey);
public sealed record ProjectDocumentContent(Stream Content);

public interface IProjectDocumentStorage
{
    Task<StoredProjectDocument> SaveAsync(string objectPath, string contentType, Stream content, CancellationToken cancellationToken);
    Task<ProjectDocumentContent?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

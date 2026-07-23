using System.Net.Http.Headers;
using CPMS.BLL.Documents;
using CPMS.DAL.Data;
using CPMS.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CPMS.DAL.ExternalServices;

public sealed class ProjectDocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";
    public string Provider { get; init; } = "Local";
    public string LocalRoot { get; init; } = "App_Data/documents";
    public string SupabaseUrl { get; init; } = "";
    public string ServiceRoleKey { get; init; } = "";
    public string Bucket { get; init; } = "cpms-documents";
}

internal sealed class ProjectDocumentStorage(
    HttpClient httpClient,
    ProjectDocumentStorageOptions options,
    ILogger<ProjectDocumentStorage> logger,
    CpmsDbContext? db = null) : IProjectDocumentStorage
{
    private const string SupabasePrefix = "supabase:";
    private const string DatabasePrefix = "database:";
    private const string LocalPrefix = "local:";
    public async Task<StoredProjectDocument> SaveAsync(
        string objectPath, string contentType, Stream content, CancellationToken cancellationToken)
    {
        if (SupabaseRequested)
        {
            EnsureSupabaseConfigured();
            using var request = CreateSupabaseRequest(HttpMethod.Post, $"object/{Encode(options.Bucket)}/{EncodePath(objectPath)}");
            request.Headers.TryAddWithoutValidation("x-upsert", "false");
            request.Content = new StreamContent(content);
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Supabase document upload failed with {StatusCode}.", (int)response.StatusCode);
                throw new InvalidOperationException("Không thể lưu tài liệu vào kho lưu trữ riêng tư.");
            }
            return new StoredProjectDocument($"supabase:{objectPath}");
        }

        if (DatabaseRequested && db is not null)
        {
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            db.DocumentPayloads.Add(new DocumentPayload
            {
                StorageKey = objectPath,
                Content = buffer.ToArray(),
                ContentType = contentType
            });
            await db.SaveChangesAsync(cancellationToken);
            return new StoredProjectDocument($"database:{objectPath}");
        }

        var root = Path.GetFullPath(options.LocalRoot, AppContext.BaseDirectory);
        var fullPath = Path.GetFullPath(Path.Combine(root, objectPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid document path.");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using var output = File.Create(fullPath);
        await content.CopyToAsync(output, cancellationToken);
        return new StoredProjectDocument($"local:{fullPath}");
    }

    public async Task<ProjectDocumentContent?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (storageKey.StartsWith(SupabasePrefix, StringComparison.Ordinal))
        {
            var objectPath = storageKey[SupabasePrefix.Length..];
            using var request = CreateSupabaseRequest(HttpMethod.Get, $"object/authenticated/{Encode(options.Bucket)}/{EncodePath(objectPath)}");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return new ProjectDocumentContent(new MemoryStream(bytes, writable: false));
        }

        if (storageKey.StartsWith(DatabasePrefix, StringComparison.Ordinal) && db is not null)
        {
            var key = storageKey[DatabasePrefix.Length..];
            var payload = await db.DocumentPayloads.AsNoTracking().SingleOrDefaultAsync(x => x.StorageKey == key, cancellationToken);
            return payload is null ? null : new ProjectDocumentContent(new MemoryStream(payload.Content, writable: false));
        }

        var path = storageKey.StartsWith(LocalPrefix, StringComparison.Ordinal) ? storageKey[LocalPrefix.Length..] : storageKey;
        return File.Exists(path) ? new ProjectDocumentContent(File.OpenRead(path)) : null;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (storageKey.StartsWith(SupabasePrefix, StringComparison.Ordinal))
        {
            var objectPath = storageKey[SupabasePrefix.Length..];
            using var request = CreateSupabaseRequest(HttpMethod.Delete, $"object/{Encode(options.Bucket)}/{EncodePath(objectPath)}");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) logger.LogWarning("Could not roll back Supabase object {ObjectPath}.", objectPath);
            return;
        }
        if (storageKey.StartsWith(DatabasePrefix, StringComparison.Ordinal) && db is not null)
        {
            var key = storageKey[DatabasePrefix.Length..];
            var payload = await db.DocumentPayloads.SingleOrDefaultAsync(x => x.StorageKey == key, cancellationToken);
            if (payload is not null) { db.DocumentPayloads.Remove(payload); await db.SaveChangesAsync(cancellationToken); }
            return;
        }
        var path = storageKey.StartsWith(LocalPrefix, StringComparison.Ordinal) ? storageKey[LocalPrefix.Length..] : storageKey;
        if (File.Exists(path)) File.Delete(path);
    }

    private bool SupabaseRequested => string.Equals(options.Provider, "Supabase", StringComparison.OrdinalIgnoreCase);
    private bool DatabaseRequested => string.Equals(options.Provider, "Database", StringComparison.OrdinalIgnoreCase);
    private bool SupabaseConfigured => !string.IsNullOrWhiteSpace(options.SupabaseUrl) && !string.IsNullOrWhiteSpace(options.ServiceRoleKey);

    private HttpRequestMessage CreateSupabaseRequest(HttpMethod method, string relativePath)
    {
        EnsureSupabaseConfigured();
        var request = new HttpRequestMessage(method, $"{options.SupabaseUrl.TrimEnd('/')}/storage/v1/{relativePath}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ServiceRoleKey);
        request.Headers.Add("apikey", options.ServiceRoleKey);
        return request;
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);
    private static string EncodePath(string path) => string.Join('/', path.Split('/').Select(Encode));
    private void EnsureSupabaseConfigured()
    {
        if (!SupabaseConfigured) throw new InvalidOperationException("Supabase document storage is not configured.");
    }
}

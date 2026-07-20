using CPMS.DAL.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;

namespace CPMS.IntegrationTests;

public sealed class ProjectDocumentStorageTests
{
    [Fact]
    public async Task LocalProvider_RoundTripsPrivateObjectAndDeletesIt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cpms-documents-{Guid.NewGuid():N}");
        try
        {
            var storage = new ProjectDocumentStorage(new HttpClient(), new ProjectDocumentStorageOptions
            {
                Provider = "Local",
                LocalRoot = root
            }, NullLogger<ProjectDocumentStorage>.Instance);
            await using var source = new MemoryStream("%PDF-test"u8.ToArray());

            var saved = await storage.SaveAsync("groups/1/final/v1/report.pdf", "application/pdf", source, CancellationToken.None);
            var opened = await storage.OpenReadAsync(saved.StorageKey, CancellationToken.None);

            Assert.NotNull(opened);
            await using (opened!.Content)
            using (var reader = new StreamReader(opened.Content))
                Assert.Equal("%PDF-test", await reader.ReadToEndAsync());
            await storage.DeleteAsync(saved.StorageKey, CancellationToken.None);
            Assert.Null(await storage.OpenReadAsync(saved.StorageKey, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

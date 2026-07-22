using CPMS.BLL.AI.ProjectSuggestions;
using CPMS.BLL.Documents;
using CPMS.BLL.Enums;

namespace CPMS.UnitTests.Services;

public sealed class ProjectDocumentServiceTests
{
    [Fact]
    public async Task UploadAsync_ValidPdf_PersistsVersionedPrivateStorageKey()
    {
        var store = new StubStore { StudentInGroup = true, ActiveSyllabusId = 4, NextVersion = 3 };
        var storage = new StubStorage();
        var service = new ProjectDocumentService(store, storage, new StubSuggestionService());
        await using var content = new MemoryStream("%PDF-test"u8.ToArray());

        ProjectDocumentData result = await service.UploadAsync(
            new ProjectDocumentActor(11, UserRole.Student),
            new UploadProjectDocumentInput(8, "report.pdf", content.Length, content),
            CancellationToken.None);

        Assert.Contains("groups/8/history/v3/", storage.LastObjectPath, StringComparison.Ordinal);
        Assert.Equal("private:test-key", store.Created?.StorageKey);
        Assert.Equal(DocumentType.General, store.Created?.DocType);
        Assert.Equal(3, result.VersionNo);
    }

    [Fact]
    public async Task GenerateSuggestionsAsync_AssignedLecturer_UsesPdfDocumentContract()
    {
        var store = new StubStore { LecturerCanAccess = true };
        store.Document = new ProjectDocumentData(6, 8, DocumentType.Review3, "report.pdf", "private:test-key", 9, 1, DocumentStatus.Submitted, DateTime.UtcNow, 11, "Sinh viên Duy");
        var suggestions = new StubSuggestionService();
        var service = new ProjectDocumentService(store, new StubStorage(), suggestions);

        ProjectSuggestionResult result = await service.GenerateSuggestionsAsync(
            new ProjectDocumentActor(20, UserRole.Lecturer), 6, CancellationToken.None);

        Assert.Equal("Summary", result.ContentSummary);
        Assert.Equal("application/pdf", suggestions.LastDocument?.MimeType);
        Assert.Equal("CPMS", suggestions.LastDocument?.ProjectName);
    }

    private sealed class StubStorage : IProjectDocumentStorage
    {
        public string? LastObjectPath { get; private set; }
        public Task<StoredProjectDocument> SaveAsync(string objectPath, string contentType, Stream content, CancellationToken cancellationToken)
        { LastObjectPath = objectPath; return Task.FromResult(new StoredProjectDocument("private:test-key")); }
        public Task<ProjectDocumentContent?> OpenReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectDocumentContent?>(new(new MemoryStream("%PDF-test"u8.ToArray())));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubSuggestionService : IProjectSuggestionService
    {
        public ProjectDocumentSuggestionInput? LastDocument { get; private set; }
        public Task<ProjectSuggestionResult> GenerateAsync(ProjectSuggestionInput input, CancellationToken cancellationToken) =>
            Task.FromResult(new ProjectSuggestionResult("Summary", "Strengths", "Improvements"));
        public Task<ProjectSuggestionResult> GenerateFromDocumentAsync(ProjectDocumentSuggestionInput input, CancellationToken cancellationToken)
        { LastDocument = input; return Task.FromResult(new ProjectSuggestionResult("Summary", "Strengths", "Improvements")); }
    }

    private sealed class StubStore : IProjectDocumentStore
    {
        public bool StudentInGroup { get; init; }
        public bool LecturerCanAccess { get; init; }
        public int? ActiveSyllabusId { get; init; } = 1;
        public int NextVersion { get; init; } = 1;
        public ProjectDocumentData? Document { get; set; }
        public CreateProjectDocumentData? Created { get; private set; }
        public Task<bool> IsStudentInGroupAsync(int userId, int groupId, CancellationToken cancellationToken) => Task.FromResult(StudentInGroup);
        public Task<bool> CanLecturerAccessGroupAsync(int userId, int groupId, CancellationToken cancellationToken) => Task.FromResult(LecturerCanAccess);
        public Task<int?> FindActiveSyllabusIdAsync(CancellationToken cancellationToken) => Task.FromResult(ActiveSyllabusId);
        public Task<int> GetNextVersionAsync(int groupId, CancellationToken cancellationToken) => Task.FromResult(NextVersion);
        public Task<IReadOnlyList<ProjectDocumentData>> ListAsync(int groupId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProjectDocumentData>>(Document is null ? [] : [Document]);
        public Task<ProjectDocumentData?> FindAsync(int documentId, CancellationToken cancellationToken) => Task.FromResult(Document);
        public Task<ProjectDocumentData> CreateAsync(CreateProjectDocumentData input, CancellationToken cancellationToken)
        {
            Created = input;
            return Task.FromResult(new ProjectDocumentData(1, input.GroupId, input.DocType, input.FileName, input.StorageKey,
                input.FileSize, input.VersionNo, DocumentStatus.Submitted, DateTime.UtcNow, input.UploadedById, "Sinh viên Duy"));
        }
        public Task<string> GetProjectNameAsync(int groupId, CancellationToken cancellationToken) => Task.FromResult("CPMS");
    }
}

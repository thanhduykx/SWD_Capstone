namespace CPMS.BLL.AI.ProjectSuggestions;

public sealed record ProjectSuggestionInput(string ProjectName, string ProjectContent);
public sealed record ProjectDocumentSuggestionInput(
    string ProjectName,
    string FileName,
    string MimeType,
    ReadOnlyMemory<byte> Content);

public sealed record ProjectSuggestionResult(
    string ContentSummary,
    string StrengthsSummary,
    string ImprovementSummary);

public interface IProjectSuggestionGenerator
{
    Task<ProjectSuggestionResult> GenerateAsync(
        ProjectSuggestionInput input,
        CancellationToken cancellationToken);
    Task<ProjectSuggestionResult> GenerateFromDocumentAsync(
        ProjectDocumentSuggestionInput input,
        CancellationToken cancellationToken);
}

public interface IProjectSuggestionService
{
    Task<ProjectSuggestionResult> GenerateAsync(
        ProjectSuggestionInput input,
        CancellationToken cancellationToken);
    Task<ProjectSuggestionResult> GenerateFromDocumentAsync(
        ProjectDocumentSuggestionInput input,
        CancellationToken cancellationToken);
}

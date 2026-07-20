namespace CPMS.BLL.AI.ProjectSuggestions;

public sealed class ProjectSuggestionService(IProjectSuggestionGenerator generator)
    : IProjectSuggestionService
{
    public const int MaxProjectNameLength = 200;
    public const int MaxProjectContentLength = 20_000;
    public const int MaxPdfFileSize = 50 * 1024 * 1024;

    public Task<ProjectSuggestionResult> GenerateAsync(
        ProjectSuggestionInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var projectName = input.ProjectName?.Trim();
        var projectContent = input.ProjectContent?.Trim();

        if (string.IsNullOrWhiteSpace(projectName))
            throw new ArgumentException("Project name is required.", nameof(input));
        if (projectName.Length > MaxProjectNameLength)
            throw new ArgumentException(
                $"Project name must not exceed {MaxProjectNameLength} characters.", nameof(input));
        if (string.IsNullOrWhiteSpace(projectContent))
            throw new ArgumentException("Project content is required.", nameof(input));
        if (projectContent.Length > MaxProjectContentLength)
            throw new ArgumentException(
                $"Project content must not exceed {MaxProjectContentLength} characters.", nameof(input));

        return generator.GenerateAsync(
            new ProjectSuggestionInput(projectName, projectContent), cancellationToken);
    }

    public Task<ProjectSuggestionResult> GenerateFromDocumentAsync(
        ProjectDocumentSuggestionInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var projectName = input.ProjectName?.Trim();
        if (string.IsNullOrWhiteSpace(projectName) || projectName.Length > MaxProjectNameLength)
            throw new ArgumentException("Project name is invalid.", nameof(input));
        if (!string.Equals(input.MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only PDF documents can be processed directly.", nameof(input));
        if (input.Content.IsEmpty || input.Content.Length > MaxPdfFileSize)
            throw new ArgumentException("PDF content must be between 1 byte and 50 MB.", nameof(input));

        return generator.GenerateFromDocumentAsync(
            input with { ProjectName = projectName }, cancellationToken);
    }
}

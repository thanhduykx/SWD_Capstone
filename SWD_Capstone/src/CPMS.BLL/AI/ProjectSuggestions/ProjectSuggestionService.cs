namespace CPMS.BLL.AI.ProjectSuggestions;

public sealed class ProjectSuggestionService(IProjectSuggestionGenerator generator)
    : IProjectSuggestionService
{
    public const int MaxProjectNameLength = 200;
    public const int MaxProjectContentLength = 20_000;

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
}

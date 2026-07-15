using CPMS.BLL.AI.ProjectSuggestions;

namespace CPMS.UnitTests.Services;

public sealed class ProjectSuggestionServiceTests
{
    [Fact]
    public async Task GenerateAsync_TrimsInputAndReturnsGeneratorResult()
    {
        var expected = new ProjectSuggestionResult("Content", "Strengths", "Improvements");
        var generator = new StubGenerator(expected);
        var service = new ProjectSuggestionService(generator);

        var result = await service.GenerateAsync(
            new ProjectSuggestionInput("  CPMS  ", "  Project details  "),
            CancellationToken.None);

        Assert.Equal(expected, result);
        Assert.Equal("CPMS", generator.LastInput?.ProjectName);
        Assert.Equal("Project details", generator.LastInput?.ProjectContent);
    }

    [Theory]
    [InlineData("", "Project details")]
    [InlineData("CPMS", "  ")]
    public async Task GenerateAsync_RejectsBlankRequiredInput(
        string projectName,
        string projectContent)
    {
        var service = new ProjectSuggestionService(new StubGenerator(
            new ProjectSuggestionResult("Content", "Strengths", "Improvements")));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAsync(
            new ProjectSuggestionInput(projectName, projectContent),
            CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_RejectsOversizedProjectContent()
    {
        var service = new ProjectSuggestionService(new StubGenerator(
            new ProjectSuggestionResult("Content", "Strengths", "Improvements")));

        await Assert.ThrowsAsync<ArgumentException>(() => service.GenerateAsync(
            new ProjectSuggestionInput(
                "CPMS",
                new string('x', ProjectSuggestionService.MaxProjectContentLength + 1)),
            CancellationToken.None));
    }

    private sealed class StubGenerator(ProjectSuggestionResult result) : IProjectSuggestionGenerator
    {
        public ProjectSuggestionInput? LastInput { get; private set; }

        public Task<ProjectSuggestionResult> GenerateAsync(
            ProjectSuggestionInput input,
            CancellationToken cancellationToken)
        {
            LastInput = input;
            return Task.FromResult(result);
        }
    }
}

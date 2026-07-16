using System.Reflection;
using CPMS.API.Contracts.AI;
using CPMS.API.Controllers;
using CPMS.BLL.AI.ProjectSuggestions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CPMS.IntegrationTests;

public sealed class ProjectSuggestionsControllerTests
{
    [Fact]
    public async Task GenerateSummary_ExposesLecturerOnlyPostEndpointAndMapsAllSections()
    {
        var expected = new ProjectSuggestionResult("Nội dung", "Điểm mạnh", "Cần cải thiện");
        var service = new StubSuggestionService(expected);
        var controller = new ProjectSuggestionsController(service);
        MethodInfo? method = typeof(ProjectSuggestionsController).GetMethod(
            nameof(ProjectSuggestionsController.GenerateSummary));

        ActionResult<ProjectSuggestionResponse> result = await controller.GenerateSummary(
            new GenerateProjectSuggestionRequest("CPMS", "Project content"),
            CancellationToken.None);

        Assert.Equal(
            "api/project-suggestions",
            typeof(ProjectSuggestionsController).GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal(
            "Lecturer",
            typeof(ProjectSuggestionsController).GetCustomAttribute<AuthorizeAttribute>()?.Roles);
        Assert.Equal("summary", method?.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.Equal(
            "ai-suggestions",
            method?.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);

        ProjectSuggestionResponse response = Assert.IsType<ProjectSuggestionResponse>(
            Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(expected.ContentSummary, response.ContentSummary);
        Assert.Equal(expected.StrengthsSummary, response.StrengthsSummary);
        Assert.Equal(expected.ImprovementSummary, response.ImprovementSummary);
        Assert.Equal("CPMS", service.LastInput?.ProjectName);
        Assert.Equal("Project content", service.LastInput?.ProjectContent);
    }

    private sealed class StubSuggestionService(ProjectSuggestionResult result)
        : IProjectSuggestionService
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

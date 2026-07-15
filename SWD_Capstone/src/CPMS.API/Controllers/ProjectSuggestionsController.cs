using CPMS.API.Contracts.AI;
using CPMS.BLL.AI.ProjectSuggestions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/project-suggestions")]
[Authorize(Roles = "Lecturer")]
public sealed class ProjectSuggestionsController(IProjectSuggestionService suggestionService)
    : ControllerBase
{
    [HttpPost("summary")]
    [EnableRateLimiting("ai-suggestions")]
    [ProducesResponseType<ProjectSuggestionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ProjectSuggestionResponse>> GenerateSummary(
        GenerateProjectSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await suggestionService.GenerateAsync(
            new ProjectSuggestionInput(request.ProjectName, request.ProjectContent),
            cancellationToken);

        return Ok(new ProjectSuggestionResponse(
            result.ContentSummary,
            result.StrengthsSummary,
            result.ImprovementSummary));
    }
}

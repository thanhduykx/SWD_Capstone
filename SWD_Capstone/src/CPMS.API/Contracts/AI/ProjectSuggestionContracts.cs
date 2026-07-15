using System.ComponentModel.DataAnnotations;
using CPMS.BLL.AI.ProjectSuggestions;

namespace CPMS.API.Contracts.AI;

public sealed record GenerateProjectSuggestionRequest(
    [property: Required, StringLength(ProjectSuggestionService.MaxProjectNameLength, MinimumLength = 1)]
    string ProjectName,
    [property: Required, StringLength(ProjectSuggestionService.MaxProjectContentLength, MinimumLength = 1)]
    string ProjectContent);

public sealed record ProjectSuggestionResponse(
    string ContentSummary,
    string StrengthsSummary,
    string ImprovementSummary);

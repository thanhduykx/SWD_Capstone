using System.Globalization;
using System.Security.Claims;
using CPMS.API.Contracts.AI;
using CPMS.API.Contracts.Documents;
using CPMS.BLL.AI.ProjectSuggestions;
using CPMS.BLL.Documents;
using CPMS.BLL.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Student,Lecturer,TrainingDepartment,SystemAdministrator")]
public sealed class ProjectDocumentsController(IProjectDocumentService documentService) : ControllerBase
{
    [HttpGet("group/{groupId:int}")]
    [ProducesResponseType<IReadOnlyList<ProjectDocumentResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(int groupId, CancellationToken cancellationToken)
    {
        var documents = await documentService.ListAsync(CurrentActor(), groupId, cancellationToken);
        return Ok(documents.Select(Map).ToArray());
    }

    [HttpPost]
    [RequestSizeLimit(ProjectDocumentService.MaxFileSize + 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ProjectDocumentResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Upload([FromForm] UploadProjectDocumentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var content = request.File.OpenReadStream();
        var document = await documentService.UploadAsync(CurrentActor(), new UploadProjectDocumentInput(
            request.GroupId, request.DocType, request.File.FileName, request.File.Length, content), cancellationToken);
        return Created($"/api/documents/{document.Id}", Map(document));
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var document = await documentService.OpenAsync(CurrentActor(), id, cancellationToken);
        return File(document.Content, document.ContentType, document.FileName, enableRangeProcessing: true);
    }

    [HttpPost("{id:int}/suggestions")]
    [Authorize(Roles = "Lecturer")]
    [EnableRateLimiting("ai-suggestions")]
    [ProducesResponseType<ProjectSuggestionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateSuggestions(int id, CancellationToken cancellationToken)
    {
        ProjectSuggestionResult result = await documentService.GenerateSuggestionsAsync(CurrentActor(), id, cancellationToken);
        return Ok(new ProjectSuggestionResponse(result.ContentSummary, result.StrengthsSummary, result.ImprovementSummary));
    }

    private ProjectDocumentActor CurrentActor() => new(CurrentUserId(), CurrentRole());
    private int CurrentUserId() => int.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException(), CultureInfo.InvariantCulture);
    private UserRole CurrentRole() => Enum.Parse<UserRole>(
        User.FindFirstValue(ClaimTypes.Role) ?? throw new UnauthorizedAccessException(), ignoreCase: true);
    private static ProjectDocumentResponse Map(ProjectDocumentData x) => new(
        x.Id, x.GroupId, x.DocType.ToString(), x.FileName, x.FileSize,
        x.VersionNo, x.Status.ToString(), x.UploadedAt, x.UploadedById);
}

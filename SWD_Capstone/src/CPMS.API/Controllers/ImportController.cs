using CPMS.API.Contracts.Academic;
using CPMS.BLL.Academic.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/import")]
[Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
public sealed class ImportController(IGroupImportService importService) : ControllerBase
{
    [HttpPost("groups")]
    [RequestSizeLimit(GroupImportLimits.MaximumWorkbookBytes)]
    public async Task<ActionResult<GroupImportResult>> ImportGroups(
        [FromForm] ImportGroupsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new { error = "No Excel file was uploaded." });
        }

        if (request.File.Length > GroupImportLimits.MaximumWorkbookBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                new { error = "The uploaded Excel workbook must not exceed 5 MB." });
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (extension != ".xlsx")
        {
            return BadRequest(new { error = "Only .xlsx Excel files are supported." });
        }

        using Stream stream = request.File.OpenReadStream();
        GroupImportResult result = await importService.ImportAsync(stream, request.LecturerId, cancellationToken);
        return Ok(result);
    }
}

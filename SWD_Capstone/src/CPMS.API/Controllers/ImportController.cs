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
    public async Task<ActionResult<GroupImportResult>> ImportGroups(
        [FromForm] ImportGroupsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new { error = "No Excel file was uploaded." });
        }

        var extension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (extension != ".xlsx")
        {
            return BadRequest(new { error = "Only .xlsx Excel files are supported." });
        }

        using var stream = request.File.OpenReadStream();
        var result = await importService.ImportAsync(stream, request.LecturerId, cancellationToken);
        return Ok(result);
    }
}

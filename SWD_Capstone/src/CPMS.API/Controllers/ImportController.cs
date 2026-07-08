using CPMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/import")]
[Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
public sealed class ImportController(ExcelImportService importService) : ControllerBase
{
    [HttpPost("groups")]
    public async Task<ActionResult<ExcelImportResult>> ImportGroups(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No Excel file was uploaded." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".xlsx")
        {
            return BadRequest(new { error = "Only .xlsx Excel files are supported." });
        }

        using var stream = file.OpenReadStream();
        var result = await importService.ImportGroupsAsync(stream, cancellationToken);
        return Ok(result);
    }
}

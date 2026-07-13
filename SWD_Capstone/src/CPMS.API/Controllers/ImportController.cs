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
        [FromForm] IFormFile file,
        [FromForm] int lecturerId,
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
        var result = await importService.ImportAsync(stream, lecturerId, cancellationToken);
        return Ok(result);
    }
}

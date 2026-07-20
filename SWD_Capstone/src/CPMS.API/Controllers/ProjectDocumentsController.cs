using System.Security.Claims;
using System.Globalization;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using CPMS.DAL.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Student,Lecturer,TrainingDepartment,SystemAdministrator")]
public sealed class ProjectDocumentsController(CpmsDbContext db, IWebHostEnvironment environment) : ControllerBase
{
    private const long MaxFileSize = 50 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".zip", ".txt" };

    [HttpGet("group/{groupId:int}")]
    public async Task<IActionResult> List(int groupId, CancellationToken cancellationToken)
    {
        if (!await CanAccessGroupAsync(groupId, cancellationToken)) return Forbid();
        var documents = await db.Documents.AsNoTracking().Where(x => x.GroupId == groupId)
            .OrderByDescending(x => x.UploadedAt)
            .Select(x => new { x.Id, x.GroupId, docType = x.DocType.ToString(), x.FileName, x.FileSize, x.VersionNo, status = x.Status.ToString(), x.UploadedAt, x.UploadedById })
            .ToListAsync(cancellationToken);
        return Ok(documents);
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] int groupId, [FromForm] DocumentType docType, IFormFile file, CancellationToken cancellationToken)
    {
        if (!await CanUploadAsync(groupId, cancellationToken)) return Forbid();
        if (file is null || file.Length == 0) return BadRequest(new { error = "Vui lòng chọn tài liệu." });
        if (file.Length > MaxFileSize) return BadRequest(new { error = "Tài liệu không được vượt quá 50 MB." });
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension)) return BadRequest(new { error = "Chỉ hỗ trợ PDF, DOCX, ZIP hoặc TXT." });

        var userId = CurrentUserId();
        var version = (await db.Documents.Where(x => x.GroupId == groupId && x.DocType == docType).MaxAsync(x => (int?)x.VersionNo, cancellationToken) ?? 0) + 1;
        var folder = Path.Combine(environment.ContentRootPath, "App_Data", "documents", groupId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = Path.Combine(folder, storedName);
        await using (var stream = System.IO.File.Create(path)) await file.CopyToAsync(stream, cancellationToken);
        var syllabusId = await db.Syllabuses.Where(x => x.IsActive).Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken) ?? 0;
        if (syllabusId == 0) { System.IO.File.Delete(path); return Conflict(new { error = "Chưa có đề cương đang hoạt động để lưu tài liệu." }); }
        var document = new CapstoneDocument { GroupId = groupId, SyllabusId = syllabusId, UploadedById = userId, DocType = docType, FileName = Path.GetFileName(file.FileName), FilePath = path, FileSize = file.Length, VersionNo = version };
        db.Documents.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/documents/{document.Id}", new { document.Id, document.GroupId, docType = document.DocType.ToString(), document.FileName, document.FileSize, document.VersionNo, status = document.Status.ToString(), document.UploadedAt });
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        var document = await db.Documents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is null || !await CanAccessGroupAsync(document.GroupId, cancellationToken)) return NotFound();
        if (!System.IO.File.Exists(document.FilePath)) return NotFound(new { error = "Tệp không còn tồn tại trên máy chủ." });
        return PhysicalFile(document.FilePath, GetContentType(document.FileName), document.FileName, enableRangeProcessing: true);
    }

    private int CurrentUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException(), CultureInfo.InvariantCulture);
    private async Task<bool> CanUploadAsync(int groupId, CancellationToken ct) => User.IsInRole("Student") && await db.Students.AnyAsync(x => x.UserId == CurrentUserId() && x.GroupId == groupId, ct);
    private async Task<bool> CanAccessGroupAsync(int groupId, CancellationToken ct)
    {
        if (User.IsInRole("TrainingDepartment") || User.IsInRole("SystemAdministrator")) return true;
        var userId = CurrentUserId();
        if (User.IsInRole("Student")) return await db.Students.AnyAsync(x => x.UserId == userId && x.GroupId == groupId, ct);
        if (User.IsInRole("Lecturer"))
        {
            var lecturerId = await db.Lecturers.Where(x => x.UserId == userId).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
            return lecturerId is not null && (await db.CapstoneGroups.AnyAsync(x => x.Id == groupId && x.LecturerId == lecturerId, ct)
                || await db.GroupReviewSlots.AnyAsync(slot => slot.GroupId == groupId && db.ReviewSessions.Any(session => session.Id == slot.SessionId && (session.Reviewer1Id == lecturerId || session.Reviewer2Id == lecturerId)), ct));
        }
        return false;
    }
    private static string GetContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".zip" => "application/zip", _ => "text/plain" };
}

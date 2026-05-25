using CPMS.Core.Entities;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/semesters")]
[Authorize]
public sealed class SemestersController(CpmsDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<Semester>> GetAll(CancellationToken cancellationToken) =>
        await dbContext.Semesters.OrderByDescending(x => x.StartDate).ToListAsync(cancellationToken);

    [HttpPost]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<Semester>> Create(CreateSemesterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EndDate < request.StartDate)
        {
            return BadRequest(new { error = "Semester end date must not be before start date." });
        }

        if (request.IsActive)
        {
            await dbContext.Semesters.Where(x => x.IsActive)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);
        }

        var semester = new Semester
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            AcademicYear = request.AcademicYear.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive
        };
        dbContext.Semesters.Add(semester);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { semester.Id }, semester);
    }
}

public sealed record CreateSemesterRequest(
    string Code,
    string Name,
    string AcademicYear,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive);

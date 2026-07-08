using CPMS.Api.Services;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/semesters")]
[Authorize]
public sealed class SemestersController(
    CpmsDbContext dbContext,
    SemesterResolverService semesterResolver) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<Semester>> GetAll(CancellationToken cancellationToken)
    {
        await semesterResolver.ResolveForDateAsync(DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        return await dbContext.Semesters.OrderByDescending(x => x.StartDate).ToListAsync(cancellationToken);
    }

    [HttpGet("resolve")]
    public async Task<ActionResult<Semester>> Resolve(DateOnly date, CancellationToken cancellationToken) =>
        await semesterResolver.ResolveForDateAsync(date, cancellationToken);

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

    [HttpGet("{semesterId:int}/groups")]
    public async Task<ActionResult<IReadOnlyList<SemesterGroupResponse>>> GetGroups(
        int semesterId,
        CancellationToken cancellationToken)
    {
        var groups = await dbContext.CapstoneGroups
            .Where(x => x.SemesterId == semesterId)
            .OrderBy(x => x.Code)
            .Select(x => new SemesterGroupResponse(
                x.Id,
                x.Code,
                dbContext.Topics.Where(topic => topic.Id == x.TopicId).Select(topic => topic.NameEn).SingleOrDefault() ?? "No Topic",
                dbContext.Lecturers.Where(lecturer => lecturer.Id == x.LecturerId).Select(lecturer => lecturer.FullName).SingleOrDefault() ?? "No Supervisor",
                x.Status,
                dbContext.Students
                    .Where(student => student.GroupId == x.Id)
                    .OrderBy(student => student.Code)
                    .Select(student => new GroupMemberResponse(
                        student.Id,
                        student.Code,
                        student.FullName,
                        student.ClassCode,
                        student.Major,
                        dbContext.Users.Where(u => u.Id == student.UserId).Select(u => u.Email).FirstOrDefault() ?? "")
                    {
                        IsLeader = student.IsLeader
                    })
                    .ToList()))
            .ToListAsync(cancellationToken);

        return Ok(groups);
    }
}

public sealed record CreateSemesterRequest(
    string Code,
    string Name,
    string AcademicYear,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive);

public sealed record SemesterGroupResponse(
    int Id,
    string Code,
    string TopicName,
    string SupervisorName,
    GroupStatus Status,
    IReadOnlyList<GroupMemberResponse> Members);

public sealed record GroupMemberResponse(
    int Id,
    string Code,
    string FullName,
    string ClassCode,
    string Major,
    string Email)
{
    public bool IsLeader { get; set; }
}

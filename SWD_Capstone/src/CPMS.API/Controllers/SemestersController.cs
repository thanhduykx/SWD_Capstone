using CPMS.Api.Services;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Infrastructure.Data;
using CPMS.Shared.Models;
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
    [AllowAnonymous]
    public async Task<PagedResult<SemesterResponse>> GetAll(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        await semesterResolver.ResolveForDateAsync(DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        var query = dbContext.Semesters.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Code.ToLower().Contains(search) ||
                x.Name.ToLower().Contains(search) ||
                x.AcademicYear.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(request.FilterBy) && !string.IsNullOrWhiteSpace(request.FilterValue))
        {
            var filterBy = request.FilterBy.Trim().ToLowerInvariant();
            var filterValue = request.FilterValue.Trim();
            query = filterBy switch
            {
                "isactive" when bool.TryParse(filterValue, out var isActive) => query.Where(x => x.IsActive == isActive),
                "academicyear" => query.Where(x => x.AcademicYear == filterValue),
                _ => query
            };
        }

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "code" => request.Desc ? query.OrderByDescending(x => x.Code) : query.OrderBy(x => x.Code),
            "name" => request.Desc ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
            "academicyear" => request.Desc ? query.OrderByDescending(x => x.AcademicYear) : query.OrderBy(x => x.AcademicYear),
            "enddate" => request.Desc ? query.OrderByDescending(x => x.EndDate) : query.OrderBy(x => x.EndDate),
            "isactive" => request.Desc ? query.OrderByDescending(x => x.IsActive) : query.OrderBy(x => x.IsActive),
            _ => request.Desc ? query.OrderByDescending(x => x.StartDate) : query.OrderBy(x => x.StartDate)
        };

        var total = await query.CountAsync(cancellationToken);
        var semesters = await query
            .Skip((request.SafePage - 1) * request.SafePageSize)
            .Take(request.SafePageSize)
            .Select(x => new SemesterResponse(
                x.Id,
                x.Code,
                x.Name,
                x.AcademicYear,
                x.StartDate,
                x.EndDate,
                x.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<SemesterResponse>(semesters, request.SafePage, request.SafePageSize, total);
    }

    [HttpGet("resolve")]
    [AllowAnonymous]
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

public sealed record SemesterResponse(
    int Id,
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

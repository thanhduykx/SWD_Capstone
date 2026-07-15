using CPMS.API.Contracts.Academic;
using CPMS.BLL.Academic;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/semesters")]
[Authorize]
public sealed class SemestersController(ISemesterService semesterService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<PagedResult<SemesterResponse>> GetAll(
        [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var result = await semesterService.GetAllAsync(request, cancellationToken);
        return new PagedResult<SemesterResponse>(
            result.Items.Select(Map).ToArray(), result.Page, result.PageSize, result.TotalCount);
    }

    [HttpGet("resolve")]
    [AllowAnonymous]
    public async Task<ActionResult<SemesterResponse>> Resolve(
        DateOnly date, CancellationToken cancellationToken) =>
        Ok(Map(await semesterService.ResolveAsync(date == default ? DateOnly.FromDateTime(DateTime.Today) : date, cancellationToken)));

    [HttpPost]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<SemesterResponse>> Create(
        CreateSemesterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var result = await semesterService.CreateAsync(new CreateSemesterCommand(
                request.Code, request.Name, request.AcademicYear, request.StartDate, request.EndDate, request.IsActive),
                cancellationToken);
            return CreatedAtAction(nameof(GetAll), new { result.Id }, Map(result));
        }
        catch (BusinessRuleException exception) { return BadRequest(new { error = exception.Message }); }
    }

    [HttpGet("{semesterId:int}/groups")]
    public async Task<ActionResult<IReadOnlyList<SemesterGroupResponse>>> GetGroups(
        int semesterId, CancellationToken cancellationToken)
    {
        var groups = await semesterService.GetGroupsAsync(semesterId, cancellationToken);
        return Ok(groups.Select(x => new SemesterGroupResponse(
            x.Id, x.Code, x.TopicName, x.SupervisorName, x.Status,
            x.Members.Select(member => new GroupMemberResponse(
                member.Id, member.Code, member.FullName, member.ClassCode, member.Major,
                member.Email, member.IsLeader)).ToArray())).ToArray());
    }

    private static SemesterResponse Map(SemesterResult x) =>
        new(x.Id, x.Code, x.Name, x.AcademicYear, x.StartDate, x.EndDate, x.IsActive);
}

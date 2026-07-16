using System.Globalization;
using System.Security.Claims;
using CPMS.API.Contracts.Defense;
using CPMS.BLL.Defense.Management;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/defense-management")]
public sealed class DefenseManagementController(IDefenseManagementService defenseService) : ControllerBase
{
    [HttpGet("rounds")]
    [AllowAnonymous]
    public async Task<PagedResult<DefenseRoundResponse>> GetRounds(
        [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        PagedResult<DefenseRoundResult> result = await defenseService.GetRoundsAsync(request, cancellationToken);
        return new PagedResult<DefenseRoundResponse>(
            result.Items.Select(MapRound).ToArray(), result.Page, result.PageSize, result.TotalCount);
    }

    [HttpPost("rounds")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<DefenseRoundResponse>> CreateRound(
        CreateDefenseRoundRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DefenseRoundResult result = await defenseService.CreateRoundAsync(
            new CreateDefenseRoundCommand(
                request.Code, request.Name, request.SemesterId, request.Type, request.StartDate, request.EndDate),
            CurrentActor(), cancellationToken);
        return CreatedAtAction(nameof(GetRounds), new { result.Id }, MapRound(result));
    }

    [HttpPost("boards")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<DefenseBoardResponse>> CreateBoard(
        CreateDefenseBoardRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DefenseBoardResult result = await defenseService.CreateBoardAsync(
            new CreateDefenseBoardCommand(
                request.Code, request.SemesterId, request.Type, request.ChairmanId,
                request.SecretaryId, request.MemberLecturerIds),
            CurrentActor(), cancellationToken);
        return CreatedAtAction(nameof(GetBoardSessionsForCurrentLecturer), new { result.Id }, MapBoard(result));
    }

    [HttpPost("boards/{councilId:int}/members")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult> AddBoardMember(
        int councilId, AddDefenseBoardMemberRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            await defenseService.AddBoardMemberAsync(
                councilId, request.LecturerId, request.Role, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("sessions")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<DefenseSessionAssignmentResponse>> AssignProjectToBoard(
        AssignDefenseSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        DefenseSessionAssignmentResult result = await defenseService.AssignSessionAsync(
            new AssignDefenseSessionCommand(
                request.Code, request.DefenseRoundId, request.CouncilId, request.GroupId,
                request.SessionDate, request.Slot, request.Room),
            CurrentActor(), cancellationToken);
        return CreatedAtAction(nameof(GetBoardSessionsForCurrentLecturer), new { result.Id }, MapSession(result));
    }

    [HttpGet("my-board-sessions")]
    [Authorize(Roles = "Lecturer")]
    public async Task<IReadOnlyList<DefenseSessionAssignmentResponse>> GetBoardSessionsForCurrentLecturer(
        CancellationToken cancellationToken) =>
        (await defenseService.GetLecturerSessionsAsync(CurrentUserId(), cancellationToken))
        .Select(MapSession).ToArray();

    private DefenseActor CurrentActor() =>
        new(CurrentUserId(), User.IsInRole(nameof(UserRole.SystemAdministrator)));
    private int CurrentUserId() => int.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Missing user identifier."), CultureInfo.InvariantCulture);
    private static DefenseRoundResponse MapRound(DefenseRoundResult x) =>
        new(x.Id, x.Code, x.Name, x.SemesterId, x.Type, x.StartDate, x.EndDate, x.Status);
    private static DefenseBoardResponse MapBoard(DefenseBoardResult x) =>
        new(x.Id, x.Code, x.SemesterId, x.Type, x.Status, x.ChairmanId, x.SecretaryId, x.MemberLecturerIds);
    private static DefenseSessionAssignmentResponse MapSession(DefenseSessionAssignmentResult x) =>
        new(x.Id, x.Code, x.DefenseRoundId, x.CouncilId, x.CouncilCode, x.GroupId, x.GroupCode,
            x.SessionDate, x.Slot, x.Room, x.StartedAt, x.EndedAt, x.IsLocked);
}

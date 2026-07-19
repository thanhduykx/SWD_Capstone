using System.Globalization;
using System.Security.Claims;
using CPMS.API.Contracts.Reviews;
using CPMS.BLL.Reviews.Registration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/student-review")]
[Authorize]
public sealed class StudentReviewController(IReviewRegistrationService registrationService) : ControllerBase
{
    [HttpGet("rounds")]
    [Authorize(Roles = "Student")]
    public async Task<IReadOnlyList<StudentReviewRoundResponse>> GetRounds(
        [FromQuery] int? semesterId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<StudentReviewRoundResult> results = await registrationService.GetStudentRoundsAsync(CurrentUserId(), semesterId, cancellationToken);
        return results.Select(Map).ToArray();
    }

    [HttpGet("slots")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<StudentSlotGridResponse>> GetSlotGrid(
        int roundId,
        CancellationToken cancellationToken)
    {
        StudentSlotGridResult result = await registrationService.GetStudentSlotGridAsync(CurrentUserId(), roundId, cancellationToken);
        return Ok(Map(result));
    }

    [HttpPut("slots")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<StudentSlotGridResponse>> SaveAvailability(
        SaveStudentAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StudentSlotGridResult result = await registrationService.SaveStudentAvailabilityAsync(
            CurrentUserId(),
            request.RoundId,
            request.Slots?.Select(x => new ReviewAvailabilitySlot(x.DayOfWeek, x.Slot)).ToArray(),
            cancellationToken);
        return Ok(Map(result));
    }

    [HttpDelete("slots")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult> ClearAvailability(int roundId, CancellationToken cancellationToken)
    {
        await registrationService.ClearStudentAvailabilityAsync(CurrentUserId(), roundId, cancellationToken);
        return Ok(new { message = "Slot registration cancelled." });
    }

    [HttpGet("schedule")]
    [HttpGet("/api/review-schedules/my")]
    public async Task<IReadOnlyList<StudentScheduleResponse>> GetMySchedule(CancellationToken cancellationToken)
    {
        IReadOnlyList<StudentScheduleResult> results = await registrationService.GetStudentScheduleAsync(CurrentUserId(), cancellationToken);
        return results.Select(x => new StudentScheduleResponse(
            x.SessionId, x.SessionCode, x.Type, x.SessionDate, x.Slot, x.Room,
            x.Status, x.GroupStatus, x.CompletedAt)).ToArray();
    }

    [HttpGet("attendance")]
    public async Task<IReadOnlyList<StudentAttendanceResponse>> GetMyAttendance(CancellationToken cancellationToken)
    {
        IReadOnlyList<StudentAttendanceResult> results = await registrationService.GetStudentAttendanceAsync(CurrentUserId(), cancellationToken);
        return results.Select(x => new StudentAttendanceResponse(
            x.SessionId, x.SessionCode, x.IsPresent, x.Note, x.MarkedAt)).ToArray();
    }

    [HttpGet("comments")]
    public async Task<IReadOnlyList<ReviewCommentResponse>> GetComments(
        int sessionId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<StudentReviewCommentResult> results = await registrationService.GetStudentCommentsAsync(CurrentUserId(), sessionId, cancellationToken);
        return results.Select(x => new ReviewCommentResponse(
            x.Id, x.AuthorUserId, x.AuthorName, x.Content, x.CreatedAt, x.UpdatedAt)).ToArray();
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);

    private static StudentReviewRoundResponse Map(StudentReviewRoundResult result) =>
        new(result.Id, result.SemesterId, result.Type, result.Status,
            result.WeekStartDate, result.WeekEndDate, result.IsGroupRegistered,
            result.IsLeader, result.GroupId, result.GroupCode);

    private static StudentSlotGridResponse Map(StudentSlotGridResult result) =>
        new(
            result.RoundId,
            result.Type,
            result.Status,
            result.WeekStartDate,
            result.WeekEndDate,
            result.RequiredAvailabilityCount,
            result.MyAvailability.Select(x => new StudentSlotRegistrationResponse(
                x.Id, x.GroupId, x.GroupCode, x.DayOfWeek, x.Slot)).ToArray(),
            result.IsLeader,
            result.GroupId,
            result.GroupCode);
}

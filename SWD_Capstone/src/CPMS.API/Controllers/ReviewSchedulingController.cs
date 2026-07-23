using System.Globalization;
using System.Security.Claims;
using CPMS.API.Contracts.Reviews;
using CPMS.BLL.Abstractions.Persistence;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Reviews.Scheduling;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/review-scheduling")]
[Authorize]
public sealed class ReviewSchedulingController(
    IReviewRoundService roundService,
    IReviewSchedulingBoardService boardService,
    IReviewAssignmentService assignmentService,
    IDatabaseLifecycleService databaseLifecycle) : ControllerBase
{
    private static readonly string[] LecturerDatasetChecks =
    [
        "GET /api/review-sessions/my",
        "GET /api/defense-management/my-board-sessions"
    ];

    /// <summary>
    /// Idempotent upsert of shared lecturer review/defense sample rows on the active semester.
    /// Training Department only — does not require ExposeTestSupport and does not truncate site data.
    /// </summary>
    [HttpPost("ensure-lecturer-dataset")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<IActionResult> EnsureLecturerDataset(CancellationToken cancellationToken)
    {
        await databaseLifecycle.EnsureMobileLecturerDatasetAsync(cancellationToken);
        return Ok(new
        {
            message = "Lecturer dataset ensured on the active semester (web + mobile share the same rows).",
            loginEmail = "minhnd.gv24001@fpt.edu.vn",
            check = LecturerDatasetChecks
        });
    }

    [HttpGet("board")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment,Moderator,Lecturer")]
    public async Task<ActionResult<ReviewSchedulingBoardResponse>> GetBoard(
        int semesterId,
        ReviewType reviewType,
        DateOnly weekStart,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(MapBoard(await boardService.GetBoardAsync(
                semesterId, reviewType, weekStart, cancellationToken)));
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
    }

    [HttpPost("random-assign")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment,Moderator")]
    public async Task<ActionResult<RandomAssignReviewSessionsResponse>> RandomAssign(
        RandomAssignReviewSessionsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            GenerateReviewAssignmentsResult result = await assignmentService.GenerateAsync(
                new GenerateReviewAssignmentsCommand(
                    request.SemesterId,
                    request.ReviewType,
                    request.WeekStart,
                    request.ReviewersPerSession,
                    request.RoomPrefix,
                    request.Seed),
                cancellationToken);
            return Ok(new RandomAssignReviewSessionsResponse(
                result.TotalCandidateGroups,
                result.AssignedCount,
                0,
                0,
                result.UnassignedGroups.Select(x => new RandomAssignUnassignedGroupResponse(
                    x.GroupId, x.GroupCode, x.Reason)).ToArray(),
                result.Sessions.Select(MapSession).ToArray()));
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ConflictException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    /// <summary>
    /// Lecturers need round list to register availability (keyed by roundId).
    /// Write endpoints remain TrainingDepartment/Admin only.
    /// </summary>
    [HttpGet("rounds")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment,Moderator,Lecturer,Student")]
    public async Task<IReadOnlyList<ReviewRoundManagementResponse>> GetRounds(
        int semesterId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ReviewRoundManagementResult> results = await roundService.GetRoundsAsync(semesterId, cancellationToken);
        return results.Select(MapRound).ToArray();
    }

    [HttpPost("rounds")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment,Moderator")]
    public async Task<ActionResult<ReviewRoundManagementResponse>> CreateRound(
        CreateReviewRoundRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ReviewRoundManagementResult result = await roundService.CreateRoundAsync(
            CurrentUserId(),
            User.IsInRole(nameof(UserRole.SystemAdministrator)),
            request.SemesterId,
            request.ReviewType,
            request.RegistrationEndDate,
            request.WeekStartDate,
            cancellationToken);
        return Ok(MapRound(result));
    }

    [HttpPatch("rounds/{roundId:int}/status")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment,Moderator")]
    public async Task<ActionResult<ReviewRoundManagementResponse>> UpdateRoundStatus(
        int roundId,
        UpdateRoundStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return Ok(MapRound(await roundService.UpdateStatusAsync(roundId, request.Status, cancellationToken)));
        }
        catch (ConflictException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (ReviewRoundClosureException exception)
        {
            return Conflict(new
            {
                error = exception.Message,
                activeGroups = exception.ActiveGroupCount,
                completedRegistrations = exception.CompletedRegistrationCount
            });
        }
    }

    [HttpGet("student-registrations")]
    public async Task<IReadOnlyList<StudentSlotRegistrationResponse>> GetStudentRegistrations(
        int roundId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<StudentSlotRegistrationResult> results = await boardService.GetStudentRegistrationsAsync(roundId, cancellationToken);
        return results.Select(x => new StudentSlotRegistrationResponse(
            x.Id, x.GroupId, x.GroupCode, x.DayOfWeek, x.Slot)).ToArray();
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);

    private static ReviewRoundManagementResponse MapRound(ReviewRoundManagementResult result) =>
        new(result.Id, result.SemesterId, result.Type, result.Status,
            result.RegistrationEndDate, result.WeekStartDate, result.WeekEndDate, result.CreatedAt,
            result.OpenedAt, result.ClosedAt, result.RegisteredGroupCount);

    private static ReviewSchedulingBoardResponse MapBoard(ReviewSchedulingBoardResult result) =>
        new(
            result.SemesterId,
            result.ReviewType,
            result.WeekStart,
            result.Lecturers.Select(x => new ReviewSchedulingLecturerResponse(
                x.Id, x.Code, x.FullName, x.Department, x.Email)).ToArray(),
            result.Availability.Select(x => new ReviewSchedulingAvailabilityResponse(
                x.LecturerId, x.DayOfWeek, x.Slot)).ToArray(),
            result.AvailabilitySubmissions.Select(x => new ReviewSchedulingAvailabilitySubmissionResponse(
                x.LecturerId, x.IsSubmitted, x.SubmittedAt, x.SlotCount)).ToArray(),
            result.Groups.Select(x => new ReviewSchedulingGroupResponse(
                x.Id, x.Code, x.ProjectName, x.SupervisorId, x.SupervisorCode, x.StudentCount)).ToArray(),
            result.Sessions.Select(MapSession).ToArray());

    private static ReviewSchedulingSessionResponse MapSession(ReviewSchedulingSession result) =>
        new(result.Id, result.Code, result.GroupId, result.GroupCode, result.Type, result.Status,
            result.ReviewerIds, result.SessionDate, result.DayOfWeek, result.Slot, result.Room,
            result.GroupStatus, (result.ReviewerFeedback ?? []).Select(x =>
                new ReviewSchedulingReviewerFeedbackResponse(
                    x.ReviewerId, x.ReviewerCode, x.ReviewerName, x.Comment, x.SubmittedAt)).ToArray(),
            result.HasAccessCode, result.AccessCodeGeneratedAt, result.AccessCode);
}

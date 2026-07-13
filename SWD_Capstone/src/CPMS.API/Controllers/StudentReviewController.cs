using System.Globalization;
using System.Security.Claims;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Infrastructure.Data;
using CPMS.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/student-review")]
[Authorize]
public sealed class StudentReviewController(CpmsDbContext dbContext, AssignmentRules rules) : ControllerBase
{
    private const int RequiredAvailabilityCount = AssignmentRules.RequiredStudentAvailabilityCount;

    [HttpGet("rounds")]
    [Authorize(Roles = "Student")]
    public async Task<IReadOnlyList<StudentReviewRoundResponse>> GetRounds([FromQuery] int? semesterId, CancellationToken cancellationToken)
    {
        var student = await CurrentStudentAsyncSafe(cancellationToken);
        int groupId = student?.GroupId ?? 0;

        var query = dbContext.ReviewRounds.AsQueryable();
        if (semesterId.HasValue)
        {
            query = query.Where(x => x.SemesterId == semesterId.Value);
        }
        else
        {
            var semesterIds = await dbContext.Semesters
                .Where(x => x.IsActive)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            query = query.Where(x => semesterIds.Contains(x.SemesterId));
        }

        return await query
            .OrderBy(x => x.Type)
            .ThenByDescending(x => x.WeekStartDate)
            .Select(x => new StudentReviewRoundResponse(
                x.Id,
                x.SemesterId,
                x.Type,
                x.Status,
                x.WeekStartDate,
                x.WeekEndDate,
                dbContext.StudentSlotRegistrations.Count(reg =>
                    reg.ReviewRoundId == x.Id && reg.GroupId == groupId) == RequiredAvailabilityCount))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("slots")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<StudentSlotGridResponse>> GetSlotGrid(
        int roundId,
        CancellationToken cancellationToken)
    {
        var student = await CurrentStudentAsyncSafe(cancellationToken);

        var round = await dbContext.ReviewRounds.SingleOrDefaultAsync(x => x.Id == roundId, cancellationToken);
        if (round is null)
        {
            return NotFound(new { error = "Review round not found." });
        }

        var registrations = student?.GroupId is int groupId
            ? await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == roundId && x.GroupId == groupId)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.Slot)
            .Select(x => new StudentSlotRegistrationResponse(
                x.Id,
                x.GroupId,
                dbContext.CapstoneGroups.Where(g => g.Id == x.GroupId).Select(g => g.Code).Single(),
                x.DayOfWeek,
                x.Slot))
            .ToListAsync(cancellationToken)
            : [];

        return Ok(new StudentSlotGridResponse(
            round.Id,
            round.Type,
            round.Status,
            round.WeekStartDate,
            round.WeekEndDate,
            RequiredAvailabilityCount,
            registrations));
    }

    [HttpPut("slots")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult<StudentSlotGridResponse>> SaveAvailability(
        SaveStudentAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var student = await CurrentStudentAsync(cancellationToken);
        if (student is null || !student.GroupId.HasValue)
        {
            return BadRequest(new { error = "You must be a member of a capstone group to register." });
        }

        var round = await dbContext.ReviewRounds.SingleOrDefaultAsync(x => x.Id == request.RoundId, cancellationToken);
        if (round is null)
        {
            return NotFound(new { error = "Review round not found." });
        }

        if (round.Status != ReviewRoundStatus.Open)
        {
            return BadRequest(new { error = "Registration is not open for this review round." });
        }

        if (request.Slots is null)
        {
            return BadRequest(new { error = "Slots are required." });
        }

        var slots = request.Slots.Distinct().ToArray();
        try
        {
            rules.ValidateStudentAvailability(slots.Select(x => (x.DayOfWeek, x.Slot)).ToArray());
        }
        catch (CPMS.Core.Exceptions.BusinessRuleException exception)
        {
            return BadRequest(new { error = exception.Message });
        }

        // Verify student is group leader (first student by code in group)
        var isLeader = await IsGroupLeaderAsync(student, cancellationToken);
        if (!isLeader)
        {
            return BadRequest(new { error = "Only the group leader can register a review slot." });
        }

        var groupSemesterId = await dbContext.CapstoneGroups
            .Where(x => x.Id == student.GroupId.Value)
            .Select(x => (int?)x.SemesterId)
            .SingleOrDefaultAsync(cancellationToken);
        if (groupSemesterId != round.SemesterId)
        {
            return BadRequest(new { error = "Your capstone group does not belong to this review round's semester." });
        }

        var existing = await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == request.RoundId && x.GroupId == student.GroupId.Value)
            .ToListAsync(cancellationToken);
        dbContext.StudentSlotRegistrations.RemoveRange(existing);
        dbContext.StudentSlotRegistrations.AddRange(slots.Select(slot => new StudentSlotRegistration
        {
            ReviewRoundId = request.RoundId,
            GroupId = student.GroupId.Value,
            RegisteredByStudentId = student.Id,
            DayOfWeek = slot.DayOfWeek,
            Slot = slot.Slot
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetSlotGrid(request.RoundId, cancellationToken);
    }

    [HttpDelete("slots")]
    [Authorize(Roles = "Student")]
    public async Task<ActionResult> ClearAvailability(
        int roundId,
        CancellationToken cancellationToken)
    {
        var student = await CurrentStudentAsync(cancellationToken);
        if (student is null || !student.GroupId.HasValue)
        {
            return BadRequest(new { error = "You must be a member of a capstone group." });
        }

        var round = await dbContext.ReviewRounds.SingleOrDefaultAsync(x => x.Id == roundId, cancellationToken);
        if (round is null)
        {
            return NotFound(new { error = "Review round not found." });
        }

        if (round.Status != ReviewRoundStatus.Open)
        {
            return BadRequest(new { error = "Registration is not open for this review round." });
        }

        var isLeader = await IsGroupLeaderAsync(student, cancellationToken);
        if (!isLeader)
        {
            return BadRequest(new { error = "Only the group leader can unregister." });
        }

        var registrations = await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == roundId && x.GroupId == student.GroupId.Value)
            .ToListAsync(cancellationToken);
        if (registrations.Count == 0)
        {
            return NotFound(new { error = "No registration found for your group in this round." });
        }

        dbContext.StudentSlotRegistrations.RemoveRange(registrations);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Slot registration cancelled." });
    }

    [HttpGet("schedule")]
    [HttpGet("/api/review-schedules/my")]
    public async Task<IReadOnlyList<StudentScheduleResponse>> GetMySchedule(CancellationToken cancellationToken)
    {
        var student = await CurrentStudentAsyncSafe(cancellationToken);
        if (student is null || !student.GroupId.HasValue)
        {
            return [];
        }

        return await dbContext.GroupReviewSlots
            .Where(x => x.GroupId == student.GroupId.Value)
            .Join(
                dbContext.ReviewSessions.Where(s => s.Status == ReviewSessionStatus.Published),
                slot => slot.SessionId,
                session => session.Id,
                (slot, session) => new StudentScheduleResponse(
                    session.Id,
                    session.Code,
                    session.Type,
                    session.SessionDate,
                    session.Slot,
                    session.Room,
                    session.Status,
                    slot.Status,
                    slot.CompletedAt))
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.Slot)
            .ToListAsync(cancellationToken);
    }

    [HttpGet("attendance")]
    public async Task<IReadOnlyList<StudentAttendanceResponse>> GetMyAttendance(CancellationToken cancellationToken)
    {
        var student = await CurrentStudentAsyncSafe(cancellationToken);
        if (student is null)
        {
            return [];
        }

        return await dbContext.ReviewAttendances
            .Where(x => x.StudentId == student.Id &&
                        dbContext.GroupReviewSlots.Any(slot =>
                            slot.SessionId == x.SessionId &&
                            slot.GroupId == student.GroupId &&
                            slot.Status == ReviewGroupStatus.Completed))
            .OrderByDescending(x => x.MarkedAt)
            .Select(x => new StudentAttendanceResponse(
                x.SessionId,
                dbContext.ReviewSessions.Where(s => s.Id == x.SessionId).Select(s => s.Code).Single(),
                x.IsPresent,
                x.Note,
                x.MarkedAt))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("comments")]
    public async Task<IReadOnlyList<ReviewCommentResponse>> GetComments(
        int sessionId,
        CancellationToken cancellationToken)
    {
        var student = await CurrentStudentAsyncSafe(cancellationToken);
        if (student is null || !student.GroupId.HasValue)
        {
            return [];
        }

        var canView = await dbContext.ReviewSessions.AnyAsync(x =>
            x.Id == sessionId &&
            x.Status == ReviewSessionStatus.Published &&
            dbContext.GroupReviewSlots.Any(slot =>
                slot.SessionId == x.Id &&
                slot.GroupId == student.GroupId.Value &&
                slot.Status == ReviewGroupStatus.Completed),
            cancellationToken);
        if (!canView)
        {
            return [];
        }

        return await dbContext.ReviewComments
            .Where(x => x.SessionId == sessionId && x.GroupId == student.GroupId.Value)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ReviewCommentResponse(
                x.Id,
                x.AuthorUserId,
                dbContext.Users.Where(u => u.Id == x.AuthorUserId).Select(u => u.Username).Single(),
                x.Content,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<Student?> CurrentStudentAsyncSafe(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return null;
        }
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return null;
        }
        return await dbContext.Students
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    private async Task<Student?> CurrentStudentAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        return await dbContext.Students
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);

    private static async Task<bool> IsGroupLeaderAsync(Student student, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        return student.IsLeader;
    }
}

public sealed record StudentReviewRoundResponse(
    int Id,
    int SemesterId,
    ReviewType Type,
    ReviewRoundStatus Status,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    bool IsGroupRegistered);

public sealed record StudentSlotGridResponse(
    int RoundId,
    ReviewType Type,
    ReviewRoundStatus Status,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    int RequiredAvailabilityCount,
    IReadOnlyCollection<StudentSlotRegistrationResponse> MyAvailability);

public sealed record StudentSlotRegistrationResponse(
    int Id,
    int GroupId,
    string GroupCode,
    int DayOfWeek,
    int Slot);

public sealed record ReviewSlotSelection(int DayOfWeek, int Slot);

public sealed record SaveStudentAvailabilityRequest(
    int RoundId,
    IReadOnlyCollection<ReviewSlotSelection> Slots);

public sealed record StudentScheduleResponse(
    int SessionId,
    string SessionCode,
    ReviewType Type,
    DateTime SessionDate,
    int Slot,
    string Room,
    ReviewSessionStatus Status,
    ReviewGroupStatus GroupStatus,
    DateTime? CompletedAt);

public sealed record StudentAttendanceResponse(
    int SessionId,
    string SessionCode,
    bool IsPresent,
    string? Note,
    DateTime MarkedAt);

public sealed record ReviewCommentResponse(
    int Id,
    int AuthorUserId,
    string AuthorName,
    string Content,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

using System.Globalization;
using System.Security.Claims;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/student-review")]
[Authorize]
public sealed class StudentReviewController(CpmsDbContext dbContext) : ControllerBase
{
    private const int MaxGroupsPerSlot = 3;

    [HttpGet("rounds")]
    [AllowAnonymous]
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
                dbContext.StudentSlotRegistrations.Any(reg =>
                    reg.ReviewRoundId == x.Id && reg.GroupId == groupId)))
            .ToListAsync(cancellationToken);
    }

    [HttpGet("slots")]
    [AllowAnonymous]
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

        var registrations = await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == roundId)
            .Select(x => new StudentSlotRegistrationResponse(
                x.Id,
                x.GroupId,
                dbContext.CapstoneGroups.Where(g => g.Id == x.GroupId).Select(g => g.Code).Single(),
                x.DayOfWeek,
                x.Slot))
            .ToListAsync(cancellationToken);

        var myRegistration = student != null && student.GroupId.HasValue
            ? registrations.FirstOrDefault(r => r.GroupId == student.GroupId.Value)
            : null;

        return Ok(new StudentSlotGridResponse(
            round.Id,
            round.Type,
            round.Status,
            round.WeekStartDate,
            round.WeekEndDate,
            registrations,
            myRegistration));
    }

    [HttpPost("register")]
    public async Task<ActionResult<StudentSlotRegistrationResponse>> RegisterSlot(
        RegisterSlotRequest request,
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

        if (request.DayOfWeek is < 1 or > 6)
        {
            return BadRequest(new { error = "Day of week must be between 1 (Mon) and 6 (Sat)." });
        }

        if (request.Slot is < 1 or > 5)
        {
            return BadRequest(new { error = "Slot must be between 1 and 5." });
        }

        // Verify student is group leader (first student by code in group)
        var isLeader = await IsGroupLeaderAsync(student, cancellationToken);
        if (!isLeader)
        {
            return BadRequest(new { error = "Only the group leader can register a review slot." });
        }

        // Check if already registered in this round
        var existingRegistration = await dbContext.StudentSlotRegistrations
            .AnyAsync(x => x.ReviewRoundId == request.RoundId && x.GroupId == student.GroupId.Value, cancellationToken);
        if (existingRegistration)
        {
            return BadRequest(new { error = "Your group is already registered for a slot in this round. Unregister first to change." });
        }

        // Check max 3 groups per slot
        var groupsInSlot = await dbContext.StudentSlotRegistrations
            .CountAsync(x => x.ReviewRoundId == request.RoundId &&
                           x.DayOfWeek == request.DayOfWeek &&
                           x.Slot == request.Slot, cancellationToken);
        if (groupsInSlot >= MaxGroupsPerSlot)
        {
            return BadRequest(new { error = $"This slot already has {MaxGroupsPerSlot} groups registered. Choose another slot." });
        }

        var registration = new StudentSlotRegistration
        {
            ReviewRoundId = request.RoundId,
            GroupId = student.GroupId.Value,
            RegisteredByStudentId = student.Id,
            DayOfWeek = request.DayOfWeek,
            Slot = request.Slot
        };

        dbContext.StudentSlotRegistrations.Add(registration);
        await dbContext.SaveChangesAsync(cancellationToken);

        var groupCode = await dbContext.CapstoneGroups
            .Where(g => g.Id == student.GroupId.Value)
            .Select(g => g.Code)
            .SingleAsync(cancellationToken);

        return Ok(new StudentSlotRegistrationResponse(
            registration.Id,
            registration.GroupId,
            groupCode,
            registration.DayOfWeek,
            registration.Slot));
    }

    [HttpDelete("register")]
    public async Task<ActionResult> UnregisterSlot(
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

        var registration = await dbContext.StudentSlotRegistrations
            .SingleOrDefaultAsync(x => x.ReviewRoundId == roundId && x.GroupId == student.GroupId.Value, cancellationToken);
        if (registration is null)
        {
            return NotFound(new { error = "No registration found for your group in this round." });
        }

        dbContext.StudentSlotRegistrations.Remove(registration);
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
                dbContext.ReviewSessions.Where(s => s.Status != ReviewSessionStatus.Cancelled),
                slot => slot.SessionId,
                session => session.Id,
                (slot, session) => new StudentScheduleResponse(
                    session.Id,
                    session.Code,
                    session.Type,
                    session.SessionDate,
                    session.Slot,
                    session.Room,
                    session.Status))
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
            .Where(x => x.StudentId == student.Id)
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
    IReadOnlyCollection<StudentSlotRegistrationResponse> Registrations,
    StudentSlotRegistrationResponse? MyRegistration);

public sealed record StudentSlotRegistrationResponse(
    int Id,
    int GroupId,
    string GroupCode,
    int DayOfWeek,
    int Slot);

public sealed record RegisterSlotRequest(int RoundId, int DayOfWeek, int Slot);

public sealed record StudentScheduleResponse(
    int SessionId,
    string SessionCode,
    ReviewType Type,
    DateTime SessionDate,
    int Slot,
    string Room,
    ReviewSessionStatus Status);

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

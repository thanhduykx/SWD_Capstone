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
[Route("api/review-attendance")]
[Authorize]
public sealed class ReviewAttendanceController(CpmsDbContext dbContext) : ControllerBase
{
    [HttpGet("{sessionId:int}")]
    public async Task<ActionResult<ReviewAttendanceListResponse>> GetAttendance(
        int sessionId,
        [FromQuery] int? groupId,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.ReviewSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound(new { error = "Review session not found." });
        }

        var groupSlot = await ResolveGroupSlotAsync(sessionId, groupId, cancellationToken);
        if (groupSlot is null)
        {
            return NotFound(new { error = "No group assigned to this session." });
        }

        if (session.Status != ReviewSessionStatus.Published)
        {
            return Forbid();
        }

        if (User.IsInRole("Lecturer"))
        {
            var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
            if (lecturerId is null || !IsAssignedReviewer(session, lecturerId.Value)) return Forbid();
        }
        else if (User.IsInRole("Student"))
        {
            var userId = CurrentUserId();
            var student = await dbContext.Students.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (student is null || student.GroupId != groupSlot.GroupId || groupSlot.Status != ReviewGroupStatus.Completed) return Forbid();
        }
        else
        {
            return Forbid();
        }

        var students = await dbContext.Students
            .Where(x => x.GroupId == groupSlot.GroupId)
            .OrderBy(x => x.Code)
            .Select(x => new AttendanceStudentResponse(
                x.Id,
                x.Code,
                x.FullName,
                dbContext.ReviewAttendances
                    .Where(a => a.SessionId == sessionId && a.StudentId == x.Id)
                    .Select(a => (bool?)a.IsPresent)
                    .SingleOrDefault(),
                dbContext.ReviewAttendances
                    .Where(a => a.SessionId == sessionId && a.StudentId == x.Id)
                    .Select(a => a.Note)
                    .SingleOrDefault()))
            .ToListAsync(cancellationToken);

        var groupCode = await dbContext.CapstoneGroups
            .Where(g => g.Id == groupSlot.GroupId)
            .Select(g => g.Code)
            .SingleAsync(cancellationToken);

        return Ok(new ReviewAttendanceListResponse(
            sessionId,
            session.Code,
            groupSlot.GroupId,
            groupCode,
            session.SessionDate,
            session.Slot,
            session.Room,
            students));
    }

    [HttpPost("{sessionId:int}")]
    public async Task<ActionResult<ReviewAttendanceListResponse>> SubmitAttendance(
        int sessionId,
        SubmitAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
        if (lecturerId is null)
        {
            return Forbid();
        }

        var session = await dbContext.ReviewSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound(new { error = "Review session not found." });
        }

        if (session.Status != ReviewSessionStatus.Published || !IsAssignedReviewer(session, lecturerId.Value))
        {
            return Forbid();
        }

        var groupSlot = await ResolveGroupSlotAsync(sessionId, request.GroupId, cancellationToken);
        if (groupSlot is null)
        {
            return NotFound(new { error = "The group is not assigned to this review session." });
        }

        if (groupSlot.Status == ReviewGroupStatus.Completed)
        {
            return Conflict(new { error = "Attendance is locked after the group's review is completed." });
        }

        if (request.Entries.Count == 0 || request.Entries.Select(x => x.StudentId).Distinct().Count() != request.Entries.Count)
        {
            return BadRequest(new { error = "Attendance entries must contain distinct students." });
        }

        if (request.Entries.Any(x => x.Note?.Length > 1000))
        {
            return BadRequest(new { error = "Attendance notes cannot exceed 1000 characters." });
        }

        var validStudentIds = await dbContext.Students
            .Where(x => x.GroupId == groupSlot.GroupId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (request.Entries.Any(x => !validStudentIds.Contains(x.StudentId)))
        {
            return BadRequest(new { error = "Every attendance entry must belong to the selected group." });
        }

        foreach (var item in request.Entries)
        {
            var existing = await dbContext.ReviewAttendances
                .SingleOrDefaultAsync(x => x.SessionId == sessionId && x.StudentId == item.StudentId, cancellationToken);

            if (existing is not null)
            {
                existing.IsPresent = item.IsPresent;
                existing.Note = item.Note;
                existing.MarkedByLecturerId = lecturerId.Value;
                existing.MarkedAt = DateTime.UtcNow;
            }
            else
            {
                dbContext.ReviewAttendances.Add(new ReviewAttendance
                {
                    SessionId = sessionId,
                    StudentId = item.StudentId,
                    IsPresent = item.IsPresent,
                    Note = item.Note,
                    MarkedByLecturerId = lecturerId.Value
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAttendance(sessionId, request.GroupId, cancellationToken);
    }

    [HttpGet("{sessionId:int}/comments")]
    public async Task<ActionResult<IReadOnlyList<ReviewCommentResponse>>> GetComments(
        int sessionId,
        [FromQuery] int? groupId,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.ReviewSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        var groupSlot = await ResolveGroupSlotAsync(sessionId, groupId, cancellationToken);
        if (session is null || groupSlot is null || !await CanViewGroupAsync(session, groupSlot.GroupId, cancellationToken))
        {
            return Forbid();
        }

        return Ok(await dbContext.ReviewComments
            .Where(x => x.SessionId == sessionId && x.GroupId == groupSlot.GroupId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ReviewCommentResponse(
                x.Id,
                x.AuthorUserId,
                dbContext.Users.Where(u => u.Id == x.AuthorUserId).Select(u => u.Username).Single(),
                x.Content,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken));
    }

    [HttpPost("{sessionId:int}/comments")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewCommentResponse>> AddComment(
        int sessionId,
        AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = CurrentUserId();
        var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
        var session = await dbContext.ReviewSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        var groupSlot = await ResolveGroupSlotAsync(sessionId, request.GroupId, cancellationToken);
        if (session is null || groupSlot is null)
        {
            return NotFound(new { error = "The session or assigned group was not found." });
        }

        if (lecturerId is null || session.Status != ReviewSessionStatus.Published || !IsAssignedReviewer(session, lecturerId.Value))
        {
            return Forbid();
        }

        if (groupSlot.Status == ReviewGroupStatus.Completed)
        {
            return Conflict(new { error = "Comments are locked after the group's review is completed." });
        }

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > 2000)
        {
            return BadRequest(new { error = "Comment content is required and cannot exceed 2000 characters." });
        }

        var comment = new ReviewComment
        {
            SessionId = sessionId,
            GroupId = groupSlot.GroupId,
            AuthorUserId = userId,
            Content = content
        };

        dbContext.ReviewComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        var authorName = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Username)
            .SingleAsync(cancellationToken);

        return Ok(new ReviewCommentResponse(
            comment.Id,
            comment.AuthorUserId,
            authorName,
            comment.Content,
            comment.CreatedAt,
            comment.UpdatedAt));
    }

    [HttpPost("{sessionId:int}/groups/{groupId:int}/complete")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult> CompleteReview(
        int sessionId,
        int groupId,
        CancellationToken cancellationToken)
    {
        var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
        var session = await dbContext.ReviewSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        var groupSlot = await ResolveGroupSlotAsync(sessionId, groupId, cancellationToken);
        if (lecturerId is null || session is null || groupSlot is null || !IsAssignedReviewer(session, lecturerId.Value))
        {
            return Forbid();
        }

        if (session.Status != ReviewSessionStatus.Published)
        {
            return Conflict(new { error = "Only a published review session can be completed." });
        }

        if (groupSlot.Status == ReviewGroupStatus.Completed)
        {
            return Ok(new { sessionId, groupId, status = groupSlot.Status, groupSlot.CompletedAt });
        }

        var studentCount = await dbContext.Students.CountAsync(x => x.GroupId == groupId, cancellationToken);
        var attendanceCount = await dbContext.ReviewAttendances.CountAsync(x =>
            x.SessionId == sessionId && dbContext.Students.Any(student => student.Id == x.StudentId && student.GroupId == groupId), cancellationToken);
        var expectedSubmissionCount = new[] { session.Reviewer1Id, session.Reviewer2Id }.Count(x => x.HasValue);
        var submittedCount = await dbContext.ReviewChecklistSubmissions.CountAsync(x =>
            x.SessionId == sessionId && x.GroupId == groupId && x.Status == ReviewSubmissionStatus.Submitted, cancellationToken);
        if (studentCount == 0 || attendanceCount != studentCount || submittedCount != expectedSubmissionCount)
        {
            return Conflict(new { error = "Attendance for every student and all reviewer submissions are required before completion." });
        }

        groupSlot.Status = ReviewGroupStatus.Completed;
        groupSlot.CompletedAt = DateTime.UtcNow;
        groupSlot.CompletedByLecturerId = lecturerId.Value;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { sessionId, groupId, status = groupSlot.Status, groupSlot.CompletedAt });
    }

    private async Task<GroupReviewSlot?> ResolveGroupSlotAsync(
        int sessionId,
        int? groupId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.GroupReviewSlots.Where(x => x.SessionId == sessionId);
        if (groupId.HasValue)
        {
            return await query.SingleOrDefaultAsync(x => x.GroupId == groupId.Value, cancellationToken);
        }

        var slots = await query.Take(2).ToListAsync(cancellationToken);
        return slots.Count == 1 ? slots[0] : null;
    }

    private async Task<bool> CanViewGroupAsync(ReviewSession session, int groupId, CancellationToken cancellationToken)
    {
        if (User.IsInRole(nameof(UserRole.SystemAdministrator)) || User.IsInRole(nameof(UserRole.TrainingDepartment)))
        {
            return true;
        }

        if (User.IsInRole(nameof(UserRole.Lecturer)))
        {
            var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
            return session.Status == ReviewSessionStatus.Published &&
                   lecturerId.HasValue && IsAssignedReviewer(session, lecturerId.Value);
        }

        if (User.IsInRole(nameof(UserRole.Student)) && session.Status == ReviewSessionStatus.Published)
        {
            var userId = CurrentUserId();
            return await dbContext.Students.AnyAsync(x => x.UserId == userId && x.GroupId == groupId, cancellationToken) &&
                   await dbContext.GroupReviewSlots.AnyAsync(x =>
                       x.SessionId == session.Id && x.GroupId == groupId && x.Status == ReviewGroupStatus.Completed,
                       cancellationToken);
        }

        return false;
    }

    private static bool IsAssignedReviewer(ReviewSession session, int lecturerId) =>
        session.Reviewer1Id == lecturerId || session.Reviewer2Id == lecturerId;

    private async Task<int?> CurrentLecturerIdAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        return await dbContext.Lecturers
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);
}

public sealed record ReviewAttendanceListResponse(
    int SessionId,
    string SessionCode,
    int GroupId,
    string GroupCode,
    DateTime SessionDate,
    int Slot,
    string Room,
    IReadOnlyCollection<AttendanceStudentResponse> Students);

public sealed record AttendanceStudentResponse(
    int StudentId,
    string StudentCode,
    string FullName,
    bool? IsPresent,
    string? Note);

public sealed record SubmitAttendanceRequest(int GroupId, IReadOnlyCollection<AttendanceEntryRequest> Entries);

public sealed record AttendanceEntryRequest(int StudentId, bool IsPresent, string? Note);

public sealed record AddCommentRequest(int GroupId, string Content);

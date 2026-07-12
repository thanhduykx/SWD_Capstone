using System.Globalization;
using System.Security.Claims;
using CPMS.Core.Entities;
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
        CancellationToken cancellationToken)
    {
        var session = await dbContext.ReviewSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound(new { error = "Review session not found." });
        }

        var groupSlot = await dbContext.GroupReviewSlots
            .Where(x => x.SessionId == sessionId)
            .Select(x => new { x.GroupId })
            .SingleOrDefaultAsync(cancellationToken);
        if (groupSlot is null)
        {
            return NotFound(new { error = "No group assigned to this session." });
        }

        if (User.IsInRole("Lecturer"))
        {
            var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
            if (lecturerId is null) return Forbid();
        }
        else if (User.IsInRole("Student"))
        {
            var userId = CurrentUserId();
            var student = await dbContext.Students.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (student is null || student.GroupId != groupSlot.GroupId) return Forbid();
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
        return await GetAttendance(sessionId, cancellationToken);
    }

    [HttpGet("{sessionId:int}/comments")]
    public async Task<IReadOnlyList<ReviewCommentResponse>> GetComments(
        int sessionId,
        CancellationToken cancellationToken)
    {
        var groupId = await dbContext.GroupReviewSlots
            .Where(x => x.SessionId == sessionId)
            .Select(x => x.GroupId)
            .SingleOrDefaultAsync(cancellationToken);

        return await dbContext.ReviewComments
            .Where(x => x.SessionId == sessionId && x.GroupId == groupId)
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

    [HttpPost("{sessionId:int}/comments")]
    public async Task<ActionResult<ReviewCommentResponse>> AddComment(
        int sessionId,
        AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = CurrentUserId();
        var groupId = await dbContext.GroupReviewSlots
            .Where(x => x.SessionId == sessionId)
            .Select(x => x.GroupId)
            .SingleOrDefaultAsync(cancellationToken);

        if (groupId == 0)
        {
            return NotFound(new { error = "No group assigned to this session." });
        }

        if (User.IsInRole("Student"))
        {
            var student = await dbContext.Students.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            if (student is null || student.GroupId != groupId) return Forbid();
        }

        var comment = new ReviewComment
        {
            SessionId = sessionId,
            GroupId = groupId,
            AuthorUserId = userId,
            Content = request.Content.Trim()
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

public sealed record SubmitAttendanceRequest(IReadOnlyCollection<AttendanceEntryRequest> Entries);

public sealed record AttendanceEntryRequest(int StudentId, bool IsPresent, string? Note);

public sealed record AddCommentRequest(string Content);

using CPMS.BLL.Reviews.Assessment;
using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories.Reviews;

public sealed class ReviewAttendanceStore(CpmsDbContext dbContext) : IReviewAttendanceStore
{
    public async Task<ReviewAttendanceContextLookup> LoadContextAsync(
        int sessionId, int? groupId, int userId, CancellationToken cancellationToken)
    {
        var session = await dbContext.ReviewSessions.Where(x => x.Id == sessionId)
            .Select(x => new
            {
                x.Id, x.Code, x.Status, x.Reviewer1Id, x.Reviewer2Id,
                x.SessionDate, x.Slot, x.Room
            }).SingleOrDefaultAsync(cancellationToken);
        if (session is null) return new ReviewAttendanceContextLookup(false, null);
        var groupQuery = dbContext.GroupReviewSlots.Where(x => x.SessionId == sessionId);
        GroupReviewSlot? groupSlot;
        if (groupId.HasValue)
        {
            groupSlot = await groupQuery.SingleOrDefaultAsync(x => x.GroupId == groupId.Value, cancellationToken);
        }
        else
        {
            var slots = await groupQuery.Take(2).ToListAsync(cancellationToken);
            groupSlot = slots.Count == 1 ? slots[0] : null;
        }
        if (groupSlot is null) return new ReviewAttendanceContextLookup(true, null);
        var groupCode = await dbContext.CapstoneGroups.Where(x => x.Id == groupSlot.GroupId)
            .Select(x => x.Code).SingleAsync(cancellationToken);
        var lecturerId = await dbContext.Lecturers.Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
        var studentGroupId = await dbContext.Students.Where(x => x.UserId == userId)
            .Select(x => x.GroupId).SingleOrDefaultAsync(cancellationToken);
        return new ReviewAttendanceContextLookup(true, new ReviewAttendanceContext(
            session.Id, session.Code, session.Status,
            new[] { session.Reviewer1Id, session.Reviewer2Id }.Where(x => x.HasValue).Select(x => x!.Value).ToArray(),
            session.SessionDate, session.Slot, session.Room, groupSlot.GroupId, groupCode,
            groupSlot.Status, groupSlot.CompletedAt, lecturerId, studentGroupId));
    }

    public async Task<IReadOnlyList<AttendanceStudent>> GetStudentsAsync(
        int sessionId, int groupId, CancellationToken cancellationToken) =>
        await dbContext.Students.Where(x => x.GroupId == groupId).OrderBy(x => x.Code)
            .Select(x => new AttendanceStudent(
                x.Id, x.Code, x.FullName,
                dbContext.ReviewAttendances.Where(a => a.SessionId == sessionId && a.StudentId == x.Id)
                    .Select(a => (bool?)a.IsPresent).SingleOrDefault(),
                dbContext.ReviewAttendances.Where(a => a.SessionId == sessionId && a.StudentId == x.Id)
                    .Select(a => a.Note).SingleOrDefault()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<int>> GetStudentIdsAsync(int groupId, CancellationToken cancellationToken) =>
        (await dbContext.Students.Where(x => x.GroupId == groupId).Select(x => x.Id).ToListAsync(cancellationToken))
        .ToHashSet();

    public async Task SaveAttendanceAsync(
        int sessionId,
        int lecturerId,
        IReadOnlyCollection<AttendanceEntry> entries,
        DateTime markedAt,
        CancellationToken cancellationToken)
    {
        var studentIds = entries.Select(x => x.StudentId).ToArray();
        var existing = await dbContext.ReviewAttendances
            .Where(x => x.SessionId == sessionId && studentIds.Contains(x.StudentId))
            .ToDictionaryAsync(x => x.StudentId, cancellationToken);
        foreach (var entry in entries)
        {
            if (existing.TryGetValue(entry.StudentId, out var attendance))
            {
                attendance.IsPresent = entry.IsPresent;
                attendance.Note = entry.Note;
                attendance.MarkedByLecturerId = lecturerId;
                attendance.MarkedAt = markedAt;
            }
            else
            {
                dbContext.ReviewAttendances.Add(new ReviewAttendance
                {
                    SessionId = sessionId,
                    StudentId = entry.StudentId,
                    IsPresent = entry.IsPresent,
                    Note = entry.Note,
                    MarkedByLecturerId = lecturerId,
                    MarkedAt = markedAt
                });
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ReviewCommentResult>> GetCommentsAsync(
        int sessionId, int groupId, CancellationToken cancellationToken) =>
        await dbContext.ReviewComments.Where(x => x.SessionId == sessionId && x.GroupId == groupId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ReviewCommentResult(
                x.Id, x.AuthorUserId,
                dbContext.Users.Where(user => user.Id == x.AuthorUserId).Select(user => user.Username).Single(),
                x.Content, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(cancellationToken);

    public async Task<ReviewCommentResult> AddCommentAsync(
        int sessionId,
        int groupId,
        int authorUserId,
        string content,
        CancellationToken cancellationToken)
    {
        var comment = new ReviewComment
        {
            SessionId = sessionId,
            GroupId = groupId,
            AuthorUserId = authorUserId,
            Content = content
        };
        dbContext.ReviewComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);
        var authorName = await dbContext.Users.Where(x => x.Id == authorUserId)
            .Select(x => x.Username).SingleAsync(cancellationToken);
        return new ReviewCommentResult(
            comment.Id, comment.AuthorUserId, authorName, comment.Content, comment.CreatedAt, comment.UpdatedAt);
    }

    public async Task<(int StudentCount, int AttendanceCount, int SubmittedCount)> GetCompletionCountsAsync(
        int sessionId, int groupId, CancellationToken cancellationToken)
    {
        var studentCount = await dbContext.Students.CountAsync(x => x.GroupId == groupId, cancellationToken);
        var attendanceCount = await dbContext.ReviewAttendances.CountAsync(x =>
            x.SessionId == sessionId && dbContext.Students.Any(student =>
                student.Id == x.StudentId && student.GroupId == groupId), cancellationToken);
        var submittedCount = await dbContext.ReviewChecklistSubmissions.CountAsync(x =>
            x.SessionId == sessionId && x.GroupId == groupId &&
            x.Status == ReviewSubmissionStatus.Submitted, cancellationToken);
        return (studentCount, attendanceCount, submittedCount);
    }

    public async Task<CompleteReviewResult> CompleteAsync(
        int sessionId,
        int groupId,
        int lecturerId,
        DateTime completedAt,
        CancellationToken cancellationToken)
    {
        var slot = await dbContext.GroupReviewSlots.SingleAsync(
            x => x.SessionId == sessionId && x.GroupId == groupId, cancellationToken);
        slot.Status = ReviewGroupStatus.Completed;
        slot.CompletedAt = completedAt;
        slot.CompletedByLecturerId = lecturerId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CompleteReviewResult(sessionId, groupId, slot.Status, slot.CompletedAt);
    }
}

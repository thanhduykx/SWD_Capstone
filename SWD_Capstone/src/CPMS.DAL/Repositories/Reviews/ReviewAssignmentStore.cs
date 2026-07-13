using System.Data;
using CPMS.BLL.Reviews.Scheduling;
using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories.Reviews;

public sealed class ReviewAssignmentStore(CpmsDbContext dbContext) : IReviewAssignmentStore
{
    public async Task<TResult> ExecuteLockedAsync<TResult>(
        ReviewAssignmentScope scope,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var lockKey = $"review-round:{scope.SemesterId}:{scope.ReviewType}:{scope.WeekStart:yyyy-MM-dd}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<ReviewAssignmentSnapshot> LoadSnapshotAsync(
        ReviewAssignmentScope scope, CancellationToken cancellationToken)
    {
        var semesterCode = await dbContext.Semesters.Where(x => x.Id == scope.SemesterId)
            .Select(x => x.Code).SingleOrDefaultAsync(cancellationToken);
        if (semesterCode is null)
        {
            return EmptySnapshot();
        }

        var round = await dbContext.ReviewRounds
            .Where(x => x.SemesterId == scope.SemesterId && x.Type == scope.ReviewType &&
                        x.WeekStartDate == scope.WeekStart)
            .Select(x => new { x.Id, x.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (round is null)
        {
            return EmptySnapshot(true, semesterCode);
        }

        var weekStartDate = DateTime.SpecifyKind(
            scope.WeekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var weekEndDate = weekStartDate.AddDays(7);
        var availability = await dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == round.Id &&
                        dbContext.ReviewAvailabilitySubmissions.Any(submission =>
                            submission.ReviewRoundId == x.ReviewRoundId &&
                            submission.LecturerId == x.LecturerId && submission.SubmittedAt != null))
            .Select(x => new LecturerSlot(x.LecturerId, x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);
        var scheduledGroupIds = await dbContext.GroupReviewSlots
            .Where(slot => dbContext.ReviewSessions.Any(session =>
                session.Id == slot.SessionId && session.SemesterId == scope.SemesterId &&
                session.Type == scope.ReviewType && session.SessionDate >= weekStartDate &&
                session.SessionDate < weekEndDate && session.Status != ReviewSessionStatus.Cancelled))
            .Select(slot => slot.GroupId).ToListAsync(cancellationToken);
        var groups = await dbContext.CapstoneGroups
            .Where(x => x.SemesterId == scope.SemesterId && x.Status == GroupStatus.Active &&
                        dbContext.Students.Any(student => student.GroupId == x.Id) &&
                        !scheduledGroupIds.Contains(x.Id))
            .OrderBy(x => x.Code)
            .Select(x => new AssignmentGroup(x.Id, x.Code, x.LecturerId))
            .ToListAsync(cancellationToken);
        var groupIds = groups.Select(x => x.Id).ToArray();
        var previousReviewers = groupIds.ToDictionary<int, int, IReadOnlySet<int>>(
            groupId => groupId, _ => new HashSet<int>());
        if (scope.ReviewType == ReviewType.Review2 && groupIds.Length > 0)
        {
            var previousRows = await dbContext.GroupReviewSlots.Where(slot => groupIds.Contains(slot.GroupId))
                .Join(
                    dbContext.ReviewSessions.Where(session =>
                        session.SemesterId == scope.SemesterId && session.Type == ReviewType.Review1),
                    slot => slot.SessionId,
                    session => session.Id,
                    (slot, session) => new { slot.GroupId, session.Reviewer1Id, session.Reviewer2Id })
                .ToListAsync(cancellationToken);
            var mutable = groupIds.ToDictionary(groupId => groupId, _ => new HashSet<int>());
            foreach (var row in previousRows)
            {
                if (row.Reviewer1Id.HasValue) mutable[row.GroupId].Add(row.Reviewer1Id.Value);
                if (row.Reviewer2Id.HasValue) mutable[row.GroupId].Add(row.Reviewer2Id.Value);
            }
            previousReviewers = mutable.ToDictionary(x => x.Key, x => (IReadOnlySet<int>)x.Value);
        }

        var occupiedRows = await dbContext.ReviewSessions
            .Where(session => session.SemesterId == scope.SemesterId &&
                              session.SessionDate >= weekStartDate && session.SessionDate < weekEndDate &&
                              session.Status != ReviewSessionStatus.Cancelled)
            .Select(session => new { session.SessionDate, session.Slot, session.Reviewer1Id, session.Reviewer2Id })
            .ToListAsync(cancellationToken);
        var occupied = occupiedRows.SelectMany(row => new[] { row.Reviewer1Id, row.Reviewer2Id }
                .Where(id => id.HasValue)
                .Select(id => new OccupiedReviewerSlot(id!.Value, row.SessionDate, row.Slot)))
            .ToArray();
        var usedCodes = (await dbContext.ReviewSessions.Where(x => x.SemesterId == scope.SemesterId)
                .Select(x => x.Code).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var studentRows = await dbContext.StudentSlotRegistrations.Where(x => x.ReviewRoundId == round.Id)
            .Select(x => new { x.GroupId, x.DayOfWeek, x.Slot }).ToListAsync(cancellationToken);
        var studentAvailability = studentRows.GroupBy(x => x.GroupId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlySet<string>)group.Select(x => $"{x.DayOfWeek}:{x.Slot}")
                    .ToHashSet(StringComparer.Ordinal));
        var panelRows = await dbContext.ReviewSessions
            .Where(x => x.SemesterId == scope.SemesterId && x.Type == scope.ReviewType &&
                        x.SessionDate >= weekStartDate && x.SessionDate < weekEndDate &&
                        x.Status == ReviewSessionStatus.Draft)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Type,
                x.Status,
                x.Reviewer1Id,
                x.Reviewer2Id,
                x.SessionDate,
                x.DayOfWeek,
                x.Slot,
                x.Room,
                GroupCount = dbContext.GroupReviewSlots.Count(slot => slot.SessionId == x.Id)
            })
            .ToListAsync(cancellationToken);
        var panels = panelRows.Where(x => x.GroupCount < 3)
            .Select(x => new AssignmentPanel(
                x.Id, x.Code, x.Type, x.Status,
                new[] { x.Reviewer1Id, x.Reviewer2Id }.Where(id => id.HasValue).Select(id => id!.Value).ToArray(),
                x.SessionDate, x.DayOfWeek, x.Slot, x.Room ?? string.Empty, x.GroupCount))
            .ToArray();

        return new ReviewAssignmentSnapshot(
            true, semesterCode, round.Id, round.Status, availability, groups, previousReviewers,
            occupied, usedCodes, studentAvailability, panels);
    }

    public async Task<AssignmentPanel> CreatePanelAsync(
        CreateAssignmentPanel panel, CancellationToken cancellationToken)
    {
        var session = new ReviewSession
        {
            Code = panel.Code,
            SemesterId = panel.SemesterId,
            DayOfWeek = panel.DayOfWeek,
            Slot = panel.Slot,
            Room = panel.Room,
            Type = panel.Type,
            Reviewer1Id = panel.ReviewerIds.ElementAtOrDefault(0),
            Reviewer2Id = panel.ReviewerIds.Count > 1 ? panel.ReviewerIds[1] : null,
            SessionDate = panel.SessionDate,
            Status = ReviewSessionStatus.Draft
        };
        dbContext.ReviewSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AssignmentPanel(
            session.Id, session.Code, session.Type, session.Status, panel.ReviewerIds,
            session.SessionDate, session.DayOfWeek, session.Slot, session.Room ?? string.Empty, 0);
    }

    public async Task AssignGroupAsync(
        int sessionId,
        int groupId,
        int groupPosition,
        IReadOnlyList<int> reviewerIds,
        ReviewType reviewType,
        DateTime changedAt,
        CancellationToken cancellationToken)
    {
        dbContext.GroupReviewSlots.Add(new GroupReviewSlot
        {
            SessionId = sessionId,
            GroupId = groupId,
            GroupPosition = groupPosition,
            ConflictFlag = false
        });
        foreach (var reviewerId in reviewerIds)
        {
            dbContext.ReviewChecklistSubmissions.Add(new ReviewChecklistSubmission
            {
                SessionId = sessionId,
                GroupId = groupId,
                ReviewerId = reviewerId,
                Type = reviewType,
                Status = ReviewSubmissionStatus.Draft,
                LastSavedAt = changedAt
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ReviewAssignmentSnapshot EmptySnapshot(bool semesterExists = false, string? semesterCode = null) =>
        new(semesterExists, semesterCode, null, null, [], [],
            new Dictionary<int, IReadOnlySet<int>>(), [], new HashSet<string>(),
            new Dictionary<int, IReadOnlySet<string>>(), []);
}

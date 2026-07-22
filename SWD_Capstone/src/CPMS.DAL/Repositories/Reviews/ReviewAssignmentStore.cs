using System.Data;
using CPMS.BLL.Reviews.Scheduling;
using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CPMS.DAL.Repositories.Reviews;

public sealed class ReviewAssignmentStore(CpmsDbContext dbContext) : IReviewAssignmentStore
{
    public async Task<TResult> ExecuteLockedAsync<TResult>(
        ReviewAssignmentScope scope,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(operation);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var lockKey = $"review-round:{scope.SemesterId}:{scope.ReviewType}:{scope.WeekStart:yyyy-MM-dd}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        TResult? result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<ReviewAssignmentSnapshot> LoadSnapshotAsync(
        ReviewAssignmentScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
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
        DateTime weekEndDate = weekStartDate.AddDays(7);
        List<LecturerSlot> availability = await dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == round.Id &&
                        dbContext.ReviewAvailabilitySubmissions.Any(submission =>
                            submission.ReviewRoundId == x.ReviewRoundId &&
                            submission.LecturerId == x.LecturerId && submission.SubmittedAt != null))
            .Select(x => new LecturerSlot(x.LecturerId, x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);
        List<int> scheduledGroupIds = await dbContext.GroupReviewSlots
            .Where(slot => dbContext.ReviewSessions.Any(session =>
                session.Id == slot.SessionId && session.SemesterId == scope.SemesterId &&
                session.Type == scope.ReviewType && session.SessionDate >= weekStartDate &&
                session.SessionDate < weekEndDate && session.Status != ReviewSessionStatus.Cancelled))
            .Select(slot => slot.GroupId).ToListAsync(cancellationToken);
        List<AssignmentGroup> groups = await dbContext.CapstoneGroups
            .Where(x => x.SemesterId == scope.SemesterId && x.Status == GroupStatus.Active &&
                        dbContext.Students.Any(student => student.GroupId == x.Id) &&
                        !scheduledGroupIds.Contains(x.Id))
            .OrderBy(x => x.Code)
            .Select(x => new AssignmentGroup(x.Id, x.Code, x.LecturerId ?? 0))
            .ToListAsync(cancellationToken);
        var groupIds = groups.Select(x => x.Id).ToArray();
        Dictionary<int, IReadOnlySet<int>> previousReviewers = await LoadPreviousReviewersAsync(
            scope, groupIds, cancellationToken);

        var occupiedRows = await dbContext.ReviewSessionReviewers
            .Where(assignment => dbContext.ReviewSessions.Any(session =>
                session.Id == assignment.SessionId && session.SemesterId == scope.SemesterId &&
                session.SessionDate >= weekStartDate && session.SessionDate < weekEndDate &&
                session.Status != ReviewSessionStatus.Cancelled))
            .Select(assignment => new
            {
                assignment.LecturerId,
                SessionDate = dbContext.ReviewSessions.Where(session => session.Id == assignment.SessionId)
                    .Select(session => session.SessionDate).Single(),
                Slot = dbContext.ReviewSessions.Where(session => session.Id == assignment.SessionId)
                    .Select(session => session.Slot).Single()
            })
            .ToListAsync(cancellationToken);
        OccupiedReviewerSlot[] occupied = occupiedRows
            .Select(row => new OccupiedReviewerSlot(row.LecturerId, row.SessionDate, row.Slot))
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
                x.SessionDate,
                x.DayOfWeek,
                x.Slot,
                x.Room,
                GroupCount = dbContext.GroupReviewSlots.Count(slot => slot.SessionId == x.Id)
            })
            .ToListAsync(cancellationToken);
        var panelIds = panelRows.Select(x => x.Id).ToArray();
        var panelReviewerRows = await dbContext.ReviewSessionReviewers
            .Where(x => panelIds.Contains(x.SessionId))
            .OrderBy(x => x.Position)
            .Select(x => new { x.SessionId, x.LecturerId })
            .ToListAsync(cancellationToken);
        AssignmentPanel[] panels = panelRows.Where(x => x.GroupCount < 3)
            .Select(x => new AssignmentPanel(
                x.Id, x.Code, x.Type, x.Status,
                panelReviewerRows.Where(reviewer => reviewer.SessionId == x.Id)
                    .Select(reviewer => reviewer.LecturerId).ToArray(),
                x.SessionDate, x.DayOfWeek, x.Slot, x.Room ?? string.Empty, x.GroupCount))
            .ToArray();

        return new ReviewAssignmentSnapshot(
            true, semesterCode, round.Id, round.Status, availability, groups, previousReviewers,
            occupied, usedCodes, studentAvailability, panels);
    }

    private async Task<Dictionary<int, IReadOnlySet<int>>> LoadPreviousReviewersAsync(
        ReviewAssignmentScope scope,
        int[] groupIds,
        CancellationToken cancellationToken)
    {
        Dictionary<int, IReadOnlySet<int>> empty = groupIds.ToDictionary<int, int, IReadOnlySet<int>>(
            groupId => groupId, _ => new HashSet<int>());
        if (scope.ReviewType != ReviewType.Review2 || groupIds.Length == 0) return empty;

        var previousRows = await dbContext.GroupReviewSlots.Where(slot => groupIds.Contains(slot.GroupId))
            .Join(dbContext.ReviewSessionReviewers,
                slot => slot.SessionId,
                reviewer => reviewer.SessionId,
                (slot, reviewer) => new { slot.GroupId, reviewer.SessionId, reviewer.LecturerId })
            .Where(row => dbContext.ReviewSessions.Any(session =>
                session.Id == row.SessionId && session.SemesterId == scope.SemesterId &&
                session.Type == ReviewType.Review1))
            .ToListAsync(cancellationToken);
        Dictionary<int, HashSet<int>> mutable = groupIds.ToDictionary(groupId => groupId, _ => new HashSet<int>());
        foreach (var row in previousRows)
        {
            mutable[row.GroupId].Add(row.LecturerId);
        }
        return mutable.ToDictionary(x => x.Key, x => (IReadOnlySet<int>)x.Value);
    }

    public async Task<AssignmentPanel> CreatePanelAsync(
        CreateAssignmentPanel panel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(panel);
        var session = new ReviewSession
        {
            Code = panel.Code,
            SemesterId = panel.SemesterId,
            DayOfWeek = panel.DayOfWeek,
            Slot = panel.Slot,
            Room = panel.Room,
            Type = panel.Type,
            SessionDate = panel.SessionDate,
            Status = ReviewSessionStatus.Draft
        };
        dbContext.ReviewSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        ReviewSessionReviewerPersistence.Add(dbContext, session.Id, panel.ReviewerIds);
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
        ArgumentNullException.ThrowIfNull(reviewerIds);
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

using CPMS.BLL.Reviews.Sessions;
using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories.Reviews;

public sealed class ReviewSessionStore(CpmsDbContext dbContext) : IReviewSessionStore
{
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task<ReviewGroupState?> FindGroupAsync(int groupId, CancellationToken cancellationToken) =>
        dbContext.CapstoneGroups.Where(x => x.Id == groupId)
            .Select(x => new ReviewGroupState(x.Id, x.SemesterId, x.LecturerId ?? 0))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ReviewSessionEditState?> FindSessionForEditAsync(
        int sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.ReviewSessions.Where(x => x.Id == sessionId)
            .Select(x => new { x.Id, x.SemesterId, x.Type, x.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null) return null;
        var groups = await dbContext.GroupReviewSlots.Where(x => x.SessionId == sessionId)
            .Join(dbContext.CapstoneGroups, slot => slot.GroupId, group => group.Id,
                (slot, group) => new { group.Id, group.LecturerId })
            .ToListAsync(cancellationToken);
        return new ReviewSessionEditState(
            session.Id, session.SemesterId, session.Type, session.Status,
            groups.Select(x => x.Id).ToArray(), groups.Select(x => x.LecturerId ?? 0).ToArray());
    }

    public async Task<ReviewRoundEditState?> AcquireRoundAsync(
        int semesterId, ReviewType reviewType, DateOnly weekStart, CancellationToken cancellationToken)
    {
        var lockKey = $"review-round:{semesterId}:{reviewType}:{weekStart:yyyy-MM-dd}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        return await dbContext.ReviewRounds
            .Where(x => x.SemesterId == semesterId && x.Type == reviewType && x.WeekStartDate == weekStart)
            .Select(x => new ReviewRoundEditState(x.Id, x.Status))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountExistingLecturersAsync(
        IReadOnlyCollection<int> reviewerIds, CancellationToken cancellationToken) =>
        dbContext.Lecturers.CountAsync(x => reviewerIds.Contains(x.Id), cancellationToken);

    public Task<int> CountAvailableReviewersAsync(
        int roundId,
        int dayOfWeek,
        int slot,
        IReadOnlyCollection<int> reviewerIds,
        CancellationToken cancellationToken) =>
        dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == roundId && x.DayOfWeek == dayOfWeek && x.Slot == slot &&
                        reviewerIds.Contains(x.LecturerId) &&
                        dbContext.ReviewAvailabilitySubmissions.Any(submission =>
                            submission.ReviewRoundId == x.ReviewRoundId &&
                            submission.LecturerId == x.LecturerId && submission.SubmittedAt != null))
            .Select(x => x.LecturerId).Distinct().CountAsync(cancellationToken);

    public Task<int> CountGroupsWithSelectedAvailabilityAsync(
        int roundId,
        IReadOnlyCollection<int> groupIds,
        int dayOfWeek,
        int slot,
        int requiredSlotCount,
        CancellationToken cancellationToken) =>
        dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == roundId && groupIds.Contains(x.GroupId))
            .GroupBy(x => x.GroupId)
            .CountAsync(group => group.Count() == requiredSlotCount &&
                                 group.Any(x => x.DayOfWeek == dayOfWeek && x.Slot == slot),
                cancellationToken);

    public Task<bool> IsGroupScheduledAsync(
        int groupId,
        ReviewType reviewType,
        DateTime weekStart,
        DateTime weekEnd,
        CancellationToken cancellationToken) =>
        dbContext.GroupReviewSlots.AnyAsync(slot =>
            slot.GroupId == groupId && dbContext.ReviewSessions.Any(session =>
                session.Id == slot.SessionId && session.Type == reviewType &&
                session.SessionDate >= weekStart && session.SessionDate < weekEnd &&
                session.Status != ReviewSessionStatus.Cancelled), cancellationToken);

    public Task<bool> HasReviewerConflictAsync(
        int? excludedSessionId,
        DateTime sessionDate,
        int slot,
        IReadOnlyCollection<int> reviewerIds,
        CancellationToken cancellationToken) =>
        dbContext.ReviewSessions.AnyAsync(session =>
            (!excludedSessionId.HasValue || session.Id != excludedSessionId.Value) &&
            session.SessionDate.Date == sessionDate.Date && session.Slot == slot &&
            session.Status != ReviewSessionStatus.Cancelled &&
            ((session.Reviewer1Id.HasValue && reviewerIds.Contains(session.Reviewer1Id.Value)) ||
             (session.Reviewer2Id.HasValue && reviewerIds.Contains(session.Reviewer2Id.Value))),
            cancellationToken);

    public async Task<ReviewSessionResult> CreateSessionAsync(
        PersistReviewSession input, CancellationToken cancellationToken)
    {
        var session = new ReviewSession
        {
            Code = input.Code,
            SemesterId = input.SemesterId,
            DayOfWeek = input.DayOfWeek,
            Slot = input.Slot,
            Room = input.Room,
            Type = input.Type,
            Reviewer1Id = input.ReviewerIds.ElementAtOrDefault(0),
            Reviewer2Id = input.ReviewerIds.Count > 1 ? input.ReviewerIds[1] : null,
            SessionDate = input.SessionDate,
            Status = ReviewSessionStatus.Draft
        };
        dbContext.ReviewSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.GroupReviewSlots.Add(new GroupReviewSlot
        {
            SessionId = session.Id,
            GroupId = input.GroupId,
            GroupPosition = 1,
            ConflictFlag = false
        });
        foreach (var reviewerId in input.ReviewerIds)
        {
            dbContext.ReviewChecklistSubmissions.Add(NewSubmission(
                session.Id, input.GroupId, reviewerId, input.Type, input.ChangedAt));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapSessionAsync(session.Id, cancellationToken);
    }

    public async Task<ReviewSessionResult> UpdateSessionAsync(
        int sessionId, PersistReviewSessionUpdate update, CancellationToken cancellationToken)
    {
        var session = await dbContext.ReviewSessions.SingleAsync(x => x.Id == sessionId, cancellationToken);
        session.Code = update.Code;
        session.Room = update.Room;
        session.SessionDate = update.SessionDate;
        session.DayOfWeek = update.DayOfWeek;
        session.Slot = update.Slot;
        session.Reviewer1Id = update.ReviewerIds.ElementAtOrDefault(0);
        session.Reviewer2Id = update.ReviewerIds.Count > 1 ? update.ReviewerIds[1] : null;
        session.Status = update.Status;
        var groupIds = await dbContext.GroupReviewSlots.Where(x => x.SessionId == sessionId)
            .Select(x => x.GroupId).ToListAsync(cancellationToken);
        var submissions = await dbContext.ReviewChecklistSubmissions.Where(x => x.SessionId == sessionId)
            .ToListAsync(cancellationToken);
        dbContext.ReviewChecklistSubmissions.RemoveRange(
            submissions.Where(x => !update.ReviewerIds.Contains(x.ReviewerId)));
        foreach (var groupId in groupIds)
        {
            foreach (var reviewerId in update.ReviewerIds.Where(id =>
                         submissions.All(x => x.GroupId != groupId || x.ReviewerId != id)))
            {
                dbContext.ReviewChecklistSubmissions.Add(NewSubmission(
                    sessionId, groupId, reviewerId, session.Type, update.ChangedAt));
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapSessionAsync(sessionId, cancellationToken);
    }

    public Task<int?> FindLecturerIdByUserIdAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Lecturers.Where(x => x.UserId == userId).Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MyReviewSessionResult>> GetLecturerSessionsAsync(
        int lecturerId, CancellationToken cancellationToken) =>
        await dbContext.ReviewChecklistSubmissions
            .Where(submission => submission.ReviewerId == lecturerId &&
                                 dbContext.ReviewSessions.Any(session =>
                                     session.Id == submission.SessionId &&
                                     session.Status == ReviewSessionStatus.Published))
            .OrderBy(submission => dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId)
                .Select(session => session.SessionDate).Single())
            .ThenBy(submission => dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId)
                .Select(session => session.Slot).Single())
            .Select(submission => new MyReviewSessionResult(
                submission.SessionId,
                submission.Id,
                dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId)
                    .Select(session => session.Code).Single(),
                submission.Type,
                dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId)
                    .Select(session => session.Status).Single(),
                dbContext.CapstoneGroups.Where(group => group.Id == submission.GroupId)
                    .Select(group => group.Code).Single(),
                dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId)
                    .Select(session => session.SessionDate).Single(),
                dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId)
                    .Select(session => session.Slot).Single(),
                dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId)
                    .Select(session => session.Room).Single(),
                submission.Status,
                submission.LastSavedAt))
            .ToListAsync(cancellationToken);

    private async Task<ReviewSessionResult> MapSessionAsync(int sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.ReviewSessions.SingleAsync(x => x.Id == sessionId, cancellationToken);
        var groups = await dbContext.GroupReviewSlots.Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.GroupPosition)
            .Select(x => new ReviewSessionGroupResult(
                x.GroupId,
                dbContext.CapstoneGroups.Where(group => group.Id == x.GroupId).Select(group => group.Code).Single(),
                x.GroupPosition, x.Status, x.CompletedAt))
            .ToListAsync(cancellationToken);
        var reviewers = new[] { session.Reviewer1Id, session.Reviewer2Id }
            .Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return new ReviewSessionResult(
            session.Id, session.Code, session.SemesterId, groups, session.Type, session.Status,
            reviewers, session.SessionDate, session.Slot, session.Room);
    }

    private static ReviewChecklistSubmission NewSubmission(
        int sessionId, int groupId, int reviewerId, ReviewType type, DateTime changedAt) =>
        new()
        {
            SessionId = sessionId,
            GroupId = groupId,
            ReviewerId = reviewerId,
            Type = type,
            Status = ReviewSubmissionStatus.Draft,
            LastSavedAt = changedAt
        };
}

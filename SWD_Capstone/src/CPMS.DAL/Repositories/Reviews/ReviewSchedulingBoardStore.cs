using CPMS.BLL.Reviews.Scheduling;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories.Reviews;

public sealed class ReviewSchedulingBoardStore(CpmsDbContext dbContext) : IReviewSchedulingBoardStore
{
    public Task<bool> SemesterExistsAsync(int semesterId, CancellationToken cancellationToken) =>
        dbContext.Semesters.AnyAsync(x => x.Id == semesterId, cancellationToken);

    public Task<int?> FindRoundIdAsync(
        int semesterId, ReviewType reviewType, DateOnly weekStart, CancellationToken cancellationToken) =>
        dbContext.ReviewRounds
            .Where(x => x.SemesterId == semesterId && x.Type == reviewType && x.WeekStartDate == weekStart)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ReviewSchedulingBoardResult> LoadBoardAsync(
        int semesterId,
        ReviewType reviewType,
        DateOnly weekStart,
        int roundId,
        CancellationToken cancellationToken)
    {
        var weekStartDate = DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        DateTime weekEndDate = weekStartDate.AddDays(7);
        List<ReviewSchedulingLecturer> lecturers = await dbContext.Lecturers.OrderBy(x => x.Code)
            .Select(x => new ReviewSchedulingLecturer(
                x.Id, x.Code, x.FullName, x.Department,
                dbContext.Users.Where(user => user.Id == x.UserId).Select(user => user.Email)
                    .FirstOrDefault() ?? "unknown@fpt.edu.vn"))
            .ToListAsync(cancellationToken);
        List<ReviewSchedulingAvailability> availability = await dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == roundId &&
                        dbContext.ReviewAvailabilitySubmissions.Any(submission =>
                            submission.ReviewRoundId == x.ReviewRoundId &&
                            submission.LecturerId == x.LecturerId && submission.SubmittedAt != null))
            .OrderBy(x => x.LecturerId).ThenBy(x => x.DayOfWeek).ThenBy(x => x.Slot)
            .Select(x => new ReviewSchedulingAvailability(x.LecturerId, x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);
        List<ReviewSchedulingAvailabilitySubmission> submissions = await dbContext.ReviewAvailabilitySubmissions
            .Where(x => x.ReviewRoundId == roundId).OrderBy(x => x.LecturerId)
            .Select(x => new ReviewSchedulingAvailabilitySubmission(
                x.LecturerId, x.SubmittedAt != null, x.SubmittedAt,
                dbContext.ReviewAvailabilities.Count(slot =>
                    slot.ReviewRoundId == x.ReviewRoundId && slot.LecturerId == x.LecturerId)))
            .ToListAsync(cancellationToken);
        List<ReviewSchedulingGroup> groups = await dbContext.CapstoneGroups.Where(x => x.SemesterId == semesterId).OrderBy(x => x.Code)
            .Select(x => new ReviewSchedulingGroup(
                x.Id, x.Code,
                dbContext.Topics.Where(topic => topic.Id == x.TopicId).Select(topic => topic.NameEn)
                    .FirstOrDefault() ?? "Chưa đăng ký đề tài",
                x.LecturerId ?? 0,
                dbContext.Lecturers.Where(lecturer => x.LecturerId != null && lecturer.Id == x.LecturerId).Select(lecturer => lecturer.Code)
                    .FirstOrDefault() ?? "Chưa có GVHD",
                dbContext.Students.Count(student => student.GroupId == x.Id)))
            .ToListAsync(cancellationToken);
        var sessionRows = await dbContext.GroupReviewSlots
            .Join(
                dbContext.ReviewSessions.Where(x => x.SemesterId == semesterId && x.Type == reviewType &&
                    x.SessionDate >= weekStartDate && x.SessionDate < weekEndDate),
                groupSlot => groupSlot.SessionId,
                session => session.Id,
                (groupSlot, session) => new
                {
                    session.Id,
                    session.Code,
                    groupSlot.GroupId,
                    GroupCode = dbContext.CapstoneGroups.Where(group => group.Id == groupSlot.GroupId)
                        .Select(group => group.Code).Single(),
                    session.Type,
                    session.Status,
                    session.SessionDate,
                    session.DayOfWeek,
                    session.Slot,
                    session.Room,
                    GroupStatus = groupSlot.Status,
                    groupSlot.GroupPosition
                })
            .OrderBy(x => x.SessionDate).ThenBy(x => x.Slot).ThenBy(x => x.GroupPosition)
            .ToListAsync(cancellationToken);

        int[] sessionIds = sessionRows.Select(x => x.Id).Distinct().ToArray();
        var reviewerRowsBySession = await dbContext.ReviewSessionReviewers
            .Where(x => sessionIds.Contains(x.SessionId))
            .OrderBy(x => x.Position)
            .Select(x => new { x.SessionId, x.LecturerId })
            .ToListAsync(cancellationToken);
        int[] groupIds = sessionRows.Select(x => x.GroupId).Distinct().ToArray();
        var feedbackRows = await dbContext.ReviewChecklistSubmissions
            .Where(x => sessionIds.Contains(x.SessionId) && groupIds.Contains(x.GroupId) &&
                        x.Status == ReviewSubmissionStatus.Submitted)
            .Select(x => new
            {
                x.SessionId,
                x.GroupId,
                x.ReviewerId,
                ReviewerCode = dbContext.Lecturers.Where(lecturer => lecturer.Id == x.ReviewerId)
                    .Select(lecturer => lecturer.Code).Single(),
                ReviewerName = dbContext.Lecturers.Where(lecturer => lecturer.Id == x.ReviewerId)
                    .Select(lecturer => lecturer.FullName).Single(),
                Comment = x.ReviewerComment ?? x.Suggestion ?? string.Empty,
                SubmittedAt = x.SubmittedAt ?? x.LastSavedAt
            })
            .ToListAsync(cancellationToken);

        Dictionary<(int SessionId, int GroupId), ReviewSchedulingReviewerFeedback[]> feedbackBySessionGroup =
            feedbackRows
                .GroupBy(x => (x.SessionId, x.GroupId))
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(x => x.ReviewerId)
                        .Select(x => new ReviewSchedulingReviewerFeedback(
                            x.ReviewerId, x.ReviewerCode, x.ReviewerName, x.Comment, x.SubmittedAt))
                        .ToArray());

        ReviewSchedulingSession[] sessions = sessionRows.Select(x => new ReviewSchedulingSession(
            x.Id, x.Code, x.GroupId, x.GroupCode, x.Type, x.Status,
            reviewerRowsBySession.Where(reviewer => reviewer.SessionId == x.Id)
                .Select(reviewer => reviewer.LecturerId).ToArray(),
            x.SessionDate, x.DayOfWeek, x.Slot, x.Room ?? string.Empty, x.GroupStatus,
            feedbackBySessionGroup.GetValueOrDefault((x.Id, x.GroupId), []))).ToArray();

        return new ReviewSchedulingBoardResult(
            semesterId, reviewType, weekStart, lecturers, availability, submissions, groups, sessions);
    }

    public async Task<IReadOnlyList<StudentSlotRegistrationResult>> GetStudentRegistrationsAsync(
        int roundId, CancellationToken cancellationToken) =>
        await dbContext.StudentSlotRegistrations.Where(x => x.ReviewRoundId == roundId)
            .OrderBy(x => x.DayOfWeek).ThenBy(x => x.Slot)
            .Select(x => new StudentSlotRegistrationResult(
                x.Id, x.GroupId,
                dbContext.CapstoneGroups.Where(group => group.Id == x.GroupId).Select(group => group.Code).Single(),
                x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);
}

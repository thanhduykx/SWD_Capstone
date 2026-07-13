using CPMS.BLL.Reviews.Scheduling;
using CPMS.Core.Enums;
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
        var weekEndDate = weekStartDate.AddDays(7);
        var lecturers = await dbContext.Lecturers.OrderBy(x => x.Code)
            .Select(x => new ReviewSchedulingLecturer(
                x.Id, x.Code, x.FullName, x.Department,
                dbContext.Users.Where(user => user.Id == x.UserId).Select(user => user.Email)
                    .FirstOrDefault() ?? "unknown@fpt.edu.vn"))
            .ToListAsync(cancellationToken);
        var availability = await dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == roundId &&
                        dbContext.ReviewAvailabilitySubmissions.Any(submission =>
                            submission.ReviewRoundId == x.ReviewRoundId &&
                            submission.LecturerId == x.LecturerId && submission.SubmittedAt != null))
            .OrderBy(x => x.LecturerId).ThenBy(x => x.DayOfWeek).ThenBy(x => x.Slot)
            .Select(x => new ReviewSchedulingAvailability(x.LecturerId, x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);
        var submissions = await dbContext.ReviewAvailabilitySubmissions
            .Where(x => x.ReviewRoundId == roundId).OrderBy(x => x.LecturerId)
            .Select(x => new ReviewSchedulingAvailabilitySubmission(
                x.LecturerId, x.SubmittedAt != null, x.SubmittedAt,
                dbContext.ReviewAvailabilities.Count(slot =>
                    slot.ReviewRoundId == x.ReviewRoundId && slot.LecturerId == x.LecturerId)))
            .ToListAsync(cancellationToken);
        var groups = await dbContext.CapstoneGroups.Where(x => x.SemesterId == semesterId).OrderBy(x => x.Code)
            .Select(x => new ReviewSchedulingGroup(
                x.Id, x.Code,
                dbContext.Topics.Where(topic => topic.Id == x.TopicId).Select(topic => topic.NameEn)
                    .FirstOrDefault() ?? "Chưa đăng ký đề tài",
                x.LecturerId,
                dbContext.Lecturers.Where(lecturer => lecturer.Id == x.LecturerId).Select(lecturer => lecturer.Code)
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
                    session.Reviewer1Id,
                    session.Reviewer2Id,
                    session.SessionDate,
                    session.DayOfWeek,
                    session.Slot,
                    session.Room,
                    groupSlot.GroupPosition
                })
            .OrderBy(x => x.SessionDate).ThenBy(x => x.Slot).ThenBy(x => x.GroupPosition)
            .ToListAsync(cancellationToken);
        var sessions = sessionRows.Select(x => new ReviewSchedulingSession(
            x.Id, x.Code, x.GroupId, x.GroupCode, x.Type, x.Status,
            new[] { x.Reviewer1Id, x.Reviewer2Id }.Where(id => id.HasValue).Select(id => id!.Value).ToArray(),
            x.SessionDate, x.DayOfWeek, x.Slot, x.Room ?? string.Empty)).ToArray();

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

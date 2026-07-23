using CPMS.BLL.Reviews.Registration;
using CPMS.BLL.Enums;
using CPMS.DAL.Entities;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CPMS.DAL.Repositories.Reviews;

public sealed class ReviewRegistrationStore(CpmsDbContext dbContext) : IReviewRegistrationStore
{
    public async Task<TResult> ExecuteLockedAsync<TResult>(
        int roundId,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = $"review-registration:{roundId}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        TResult result = await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task<int?> FindLecturerIdByUserIdAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Lecturers.Where(x => x.UserId == userId).Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<ReviewRoundData?> FindRoundAsync(int roundId, CancellationToken cancellationToken) =>
        dbContext.ReviewRounds.Where(x => x.Id == roundId)
            .Select(x => new ReviewRoundData(
                x.Id, x.SemesterId, x.Type, x.Status, x.WeekStartDate, x.WeekEndDate))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<LecturerAvailabilityData> GetLecturerAvailabilityAsync(
        int roundId, int lecturerId, CancellationToken cancellationToken)
    {
        List<ReviewAvailabilitySlot> slots = await dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == roundId && x.LecturerId == lecturerId)
            .OrderBy(x => x.DayOfWeek).ThenBy(x => x.Slot)
            .Select(x => new ReviewAvailabilitySlot(x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);
        DateTime? submittedAt = await dbContext.ReviewAvailabilitySubmissions
            .Where(x => x.ReviewRoundId == roundId && x.LecturerId == lecturerId)
            .Select(x => x.SubmittedAt)
            .SingleOrDefaultAsync(cancellationToken);
        return new LecturerAvailabilityData(submittedAt, slots);
    }

    public async Task<IReadOnlyList<SlotRegistrationCount>> GetLecturerSlotRegistrationCountsAsync(
        int roundId, CancellationToken cancellationToken) =>
        await dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == roundId)
            .GroupBy(x => new { x.DayOfWeek, x.Slot })
            .Select(group => new SlotRegistrationCount(
                group.Key.DayOfWeek, group.Key.Slot, group.Count()))
            .OrderBy(x => x.DayOfWeek).ThenBy(x => x.Slot)
            .ToListAsync(cancellationToken);

    public async Task ReplaceLecturerAvailabilityAsync(
        ReviewRoundData round,
        int lecturerId,
        IReadOnlyCollection<ReviewAvailabilitySlot> slots,
        DateTime changedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(round);
        List<ReviewAvailability> existing = await dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == round.Id && x.LecturerId == lecturerId)
            .ToListAsync(cancellationToken);
        dbContext.ReviewAvailabilities.RemoveRange(existing);

        ReviewAvailabilitySubmission? submission = await dbContext.ReviewAvailabilitySubmissions.SingleOrDefaultAsync(
            x => x.ReviewRoundId == round.Id && x.LecturerId == lecturerId, cancellationToken);
        if (submission is null)
        {
            dbContext.ReviewAvailabilitySubmissions.Add(new ReviewAvailabilitySubmission
            {
                ReviewRoundId = round.Id,
                SemesterId = round.SemesterId,
                LecturerId = lecturerId,
                WeekStartDate = round.WeekStartDate,
                UpdatedAt = changedAt
            });
        }
        else
        {
            submission.UpdatedAt = changedAt;
            submission.SubmittedAt = null;
        }

        dbContext.ReviewAvailabilities.AddRange(slots.Select(slot => new ReviewAvailability
        {
            ReviewRoundId = round.Id,
            SemesterId = round.SemesterId,
            LecturerId = lecturerId,
            WeekStartDate = round.WeekStartDate,
            DayOfWeek = slot.DayOfWeek,
            Slot = slot.Slot
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SubmitLecturerAvailabilityAsync(
        ReviewRoundData round, int lecturerId, DateTime submittedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(round);
        ReviewAvailabilitySubmission? submission = await dbContext.ReviewAvailabilitySubmissions.SingleOrDefaultAsync(
            x => x.ReviewRoundId == round.Id && x.LecturerId == lecturerId, cancellationToken);
        if (submission is null)
        {
            dbContext.ReviewAvailabilitySubmissions.Add(new ReviewAvailabilitySubmission
            {
                ReviewRoundId = round.Id,
                SemesterId = round.SemesterId,
                LecturerId = lecturerId,
                WeekStartDate = round.WeekStartDate,
                UpdatedAt = submittedAt,
                SubmittedAt = submittedAt
            });
        }
        else
        {
            submission.UpdatedAt = submittedAt;
            submission.SubmittedAt = submittedAt;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<StudentProfileData?> FindStudentByUserIdAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Students.Where(x => x.UserId == userId)
            .Select(x => new StudentProfileData(
                x.Id,
                x.GroupId,
                x.IsLeader,
                x.GroupId.HasValue
                    ? dbContext.CapstoneGroups.Where(group => group.Id == x.GroupId.Value)
                        .Select(group => (int?)group.SemesterId).SingleOrDefault()
                    : null,
                x.GroupId.HasValue
                    ? dbContext.CapstoneGroups.Where(group => group.Id == x.GroupId.Value)
                        .Select(group => group.Code).SingleOrDefault()
                    : null))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> GetActiveSemesterIdsAsync(CancellationToken cancellationToken) =>
        await dbContext.Semesters.Where(x => x.IsActive).Select(x => x.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentRoundData>> GetStudentRoundsAsync(
        IReadOnlyCollection<int> semesterIds, int? groupId, CancellationToken cancellationToken) =>
        await dbContext.ReviewRounds.Where(x => semesterIds.Contains(x.SemesterId))
            .OrderBy(x => x.Type).ThenByDescending(x => x.WeekStartDate)
            .Select(x => new StudentRoundData(
                new ReviewRoundData(x.Id, x.SemesterId, x.Type, x.Status, x.WeekStartDate, x.WeekEndDate),
                groupId.HasValue
                    ? dbContext.StudentSlotRegistrations.Count(reg =>
                        reg.ReviewRoundId == x.Id && reg.GroupId == groupId.Value)
                    : 0))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentRegistrationData>> GetStudentRegistrationsAsync(
        int roundId, int groupId, CancellationToken cancellationToken) =>
        await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == roundId && x.GroupId == groupId)
            .OrderBy(x => x.DayOfWeek).ThenBy(x => x.Slot)
            .Select(x => new StudentRegistrationData(
                x.Id, x.GroupId,
                dbContext.CapstoneGroups.Where(group => group.Id == x.GroupId).Select(group => group.Code).Single(),
                x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SlotRegistrationCount>> GetStudentSlotRegistrationCountsAsync(
        int roundId, CancellationToken cancellationToken) =>
        await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == roundId)
            .GroupBy(x => new { x.DayOfWeek, x.Slot })
            .Select(group => new SlotRegistrationCount(
                group.Key.DayOfWeek, group.Key.Slot, group.Count()))
            .OrderBy(x => x.DayOfWeek).ThenBy(x => x.Slot)
            .ToListAsync(cancellationToken);

    public async Task ReplaceStudentAvailabilityAsync(
        int roundId,
        int groupId,
        int studentId,
        IReadOnlyCollection<ReviewAvailabilitySlot> slots,
        CancellationToken cancellationToken)
    {
        List<StudentSlotRegistration> existing = await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == roundId && x.GroupId == groupId)
            .ToListAsync(cancellationToken);
        dbContext.StudentSlotRegistrations.RemoveRange(existing);
        dbContext.StudentSlotRegistrations.AddRange(slots.Select(slot => new StudentSlotRegistration
        {
            ReviewRoundId = roundId,
            GroupId = groupId,
            RegisteredByStudentId = studentId,
            DayOfWeek = slot.DayOfWeek,
            Slot = slot.Slot
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ClearStudentAvailabilityAsync(int roundId, int groupId, CancellationToken cancellationToken)
    {
        List<StudentSlotRegistration> registrations = await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == roundId && x.GroupId == groupId)
            .ToListAsync(cancellationToken);
        if (registrations.Count == 0) return false;
        dbContext.StudentSlotRegistrations.RemoveRange(registrations);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<StudentScheduleData>> GetStudentScheduleAsync(
        int groupId, CancellationToken cancellationToken)
    {
        string groupCode = await dbContext.CapstoneGroups.Where(x => x.Id == groupId)
            .Select(x => x.Code).SingleAsync(cancellationToken);
        return await dbContext.GroupReviewSlots.Where(x => x.GroupId == groupId)
            .Join(dbContext.ReviewSessions, slot => slot.SessionId, session => session.Id,
                (slot, session) => new StudentScheduleData(
                    session.Id, session.Code, groupId, groupCode,
                    session.Type, session.SessionDate, session.Slot, session.Room,
                    session.Status, slot.Status, slot.CompletedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StudentCheckInData>> GetStudentCheckInsAsync(
        int studentId, int groupId, CancellationToken cancellationToken) =>
        await dbContext.GroupReviewSlots.Where(slot => slot.GroupId == groupId)
            .Join(dbContext.ReviewSessions, slot => slot.SessionId, session => session.Id,
                (slot, session) => new StudentCheckInData(
                    session.Id, session.Code, session.Type, session.SessionDate, session.Slot, session.Room,
                    session.Status, slot.Status,
                    dbContext.ReviewAttendances
                        .Where(attendance => attendance.SessionId == session.Id && attendance.StudentId == studentId)
                        .Select(attendance => attendance.StudentConfirmedAt)
                        .SingleOrDefault()))
            .ToListAsync(cancellationToken);

    public async Task<DateTime> ConfirmStudentCheckInAsync(
        int userId, int studentId, int groupId, int sessionId, DateTime confirmedAt,
        CancellationToken cancellationToken)
    {
        var assigned = await dbContext.GroupReviewSlots.AnyAsync(slot =>
            slot.SessionId == sessionId && slot.GroupId == groupId &&
            dbContext.ReviewSessions.Any(session => session.Id == slot.SessionId &&
                session.Status == ReviewSessionStatus.Published), cancellationToken);
        if (!assigned) throw new KeyNotFoundException("The review session is not assigned to your group.");
        ReviewAttendance? attendance = await dbContext.ReviewAttendances.SingleOrDefaultAsync(
            x => x.SessionId == sessionId && x.StudentId == studentId, cancellationToken);
        if (attendance is null)
        {
            attendance = new ReviewAttendance
            {
                SessionId = sessionId,
                StudentId = studentId,
                IsPresent = true,
                MarkedByLecturerId = null,
                MarkedAt = confirmedAt,
                StudentConfirmedAt = confirmedAt
            };
            dbContext.ReviewAttendances.Add(attendance);
        }
        else if (attendance.StudentConfirmedAt is null)
        {
            attendance.StudentConfirmedAt = confirmedAt;
            attendance.IsPresent = true;
        }
        else
        {
            return attendance.StudentConfirmedAt.Value;
        }
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "STUDENT_CONFIRM_REVIEW_ATTENDANCE",
            EntityType = nameof(ReviewAttendance),
            EntityId = sessionId,
            NewValue = $"studentId={studentId};confirmedAt={confirmedAt:O}",
            CreatedAt = confirmedAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return attendance.StudentConfirmedAt.Value;
    }

    public async Task<IReadOnlyList<StudentAttendanceData>> GetStudentAttendanceAsync(
        int studentId, CancellationToken cancellationToken)
    {
        var groupId = await dbContext.Students.Where(x => x.Id == studentId)
            .Select(x => x.GroupId).SingleOrDefaultAsync(cancellationToken);
        if (!groupId.HasValue) return [];

        return await dbContext.ReviewAttendances.Where(x => x.StudentId == studentId)
            .Join(
                dbContext.GroupReviewSlots.Where(slot => slot.GroupId == groupId.Value),
                attendance => attendance.SessionId,
                slot => slot.SessionId,
                (attendance, slot) => new StudentAttendanceData(
                    attendance.SessionId,
                    dbContext.ReviewSessions.Where(session => session.Id == attendance.SessionId)
                        .Select(session => session.Code).Single(),
                    attendance.IsPresent,
                    attendance.Note,
                    attendance.MarkedAt,
                    slot.Status))
            .ToListAsync(cancellationToken);
    }

    public Task<StudentCommentAccessData?> GetStudentCommentAccessAsync(
        int sessionId, int groupId, CancellationToken cancellationToken) =>
        dbContext.GroupReviewSlots
            .Where(slot => slot.SessionId == sessionId && slot.GroupId == groupId)
            .Select(slot => new StudentCommentAccessData(
                dbContext.ReviewSessions.Where(session => session.Id == slot.SessionId)
                    .Select(session => session.Status).Single(),
                slot.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<StudentCommentData>> GetStudentCommentsAsync(
        int sessionId, int groupId, CancellationToken cancellationToken) =>
        await dbContext.ReviewComments.Where(x => x.SessionId == sessionId && x.GroupId == groupId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new StudentCommentData(
                x.Id, x.AuthorUserId,
                dbContext.Users.Where(user => user.Id == x.AuthorUserId).Select(user => user.Username).Single(),
                x.Content, x.CreatedAt, x.UpdatedAt))
            .ToListAsync(cancellationToken);
}

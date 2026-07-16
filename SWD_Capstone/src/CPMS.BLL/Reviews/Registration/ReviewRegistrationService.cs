using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Services;

namespace CPMS.BLL.Reviews.Registration;

public sealed class ReviewRegistrationService(
    IReviewRegistrationStore store,
    AssignmentRules rules) : IReviewRegistrationService
{
    public async Task<ReviewAvailabilityWeekResult> GetLecturerAvailabilityAsync(
        int userId, int roundId, CancellationToken cancellationToken)
    {
        var lecturerId = await RequireLecturerIdAsync(userId, cancellationToken);
        ReviewRoundData round = await RequireRoundAsync(roundId, cancellationToken);
        LecturerAvailabilityData state = await store.GetLecturerAvailabilityAsync(roundId, lecturerId, cancellationToken);
        return MapLecturerAvailability(round, lecturerId, state);
    }

    public async Task<ReviewAvailabilityWeekResult> SaveLecturerAvailabilityAsync(
        int userId,
        int roundId,
        IReadOnlyCollection<ReviewAvailabilitySlot>? slots,
        CancellationToken cancellationToken)
    {
        if (slots is null)
        {
            throw new BusinessRuleException("Slots is required.");
        }
        var lecturerId = await RequireLecturerIdAsync(userId, cancellationToken);
        ReviewRoundData round = await RequireRoundAsync(roundId, cancellationToken);
        EnsureOpen(round, "Lecturer availability can only be changed while registration is Open.");

        ReviewAvailabilitySlot[] distinctSlots = slots.Distinct().OrderBy(x => x.DayOfWeek).ThenBy(x => x.Slot).ToArray();
        foreach (ReviewAvailabilitySlot? slot in distinctSlots)
        {
            if (slot.DayOfWeek is < 1 or > 6)
            {
                throw new BusinessRuleException("Review availability dayOfWeek must be between 1 and 6.");
            }
            rules.ValidateReviewSlot(slot.Slot);
        }

        await store.ReplaceLecturerAvailabilityAsync(round, lecturerId, distinctSlots, DateTime.UtcNow, cancellationToken);
        return new ReviewAvailabilityWeekResult(
            round.Id, round.SemesterId, lecturerId, round.WeekStartDate, false, null, distinctSlots);
    }

    public async Task<ReviewAvailabilityWeekResult> SubmitLecturerAvailabilityAsync(
        int userId, int roundId, CancellationToken cancellationToken)
    {
        var lecturerId = await RequireLecturerIdAsync(userId, cancellationToken);
        ReviewRoundData round = await RequireRoundAsync(roundId, cancellationToken);
        EnsureOpen(round, "Lecturer availability can only be submitted while registration is Open.");
        LecturerAvailabilityData state = await store.GetLecturerAvailabilityAsync(roundId, lecturerId, cancellationToken);
        if (state.Slots.Count == 0)
        {
            throw new BusinessRuleException("At least one availability slot is required before submitting to moderator.");
        }

        DateTime submittedAt = DateTime.UtcNow;
        await store.SubmitLecturerAvailabilityAsync(round, lecturerId, submittedAt, cancellationToken);
        return new ReviewAvailabilityWeekResult(
            round.Id, round.SemesterId, lecturerId, round.WeekStartDate, true, submittedAt, state.Slots);
    }

    public async Task<IReadOnlyList<StudentReviewRoundResult>> GetStudentRoundsAsync(
        int userId, int? semesterId, CancellationToken cancellationToken)
    {
        StudentProfileData? student = await store.FindStudentByUserIdAsync(userId, cancellationToken);
        IReadOnlyCollection<int> semesterIds = semesterId.HasValue
            ? [semesterId.Value]
            : await store.GetActiveSemesterIdsAsync(cancellationToken);
        IReadOnlyList<StudentRoundData> rows = await store.GetStudentRoundsAsync(semesterIds, student?.GroupId, cancellationToken);
        return rows.Select(x => new StudentReviewRoundResult(
            x.Round.Id, x.Round.SemesterId, x.Round.Type, x.Round.Status,
            x.Round.WeekStartDate, x.Round.WeekEndDate,
            x.RegistrationCount == AssignmentRules.RequiredStudentAvailabilityCount)).ToArray();
    }

    public async Task<StudentSlotGridResult> GetStudentSlotGridAsync(
        int userId, int roundId, CancellationToken cancellationToken)
    {
        StudentProfileData? student = await store.FindStudentByUserIdAsync(userId, cancellationToken);
        ReviewRoundData round = await RequireRoundAsync(roundId, cancellationToken);
        IReadOnlyList<StudentRegistrationData> registrations = student?.GroupId is int groupId
            ? await store.GetStudentRegistrationsAsync(roundId, groupId, cancellationToken)
            : [];
        return MapGrid(round, registrations);
    }

    public async Task<StudentSlotGridResult> SaveStudentAvailabilityAsync(
        int userId,
        int roundId,
        IReadOnlyCollection<ReviewAvailabilitySlot>? slots,
        CancellationToken cancellationToken)
    {
        if (slots is null)
        {
            throw new BusinessRuleException("Slots are required.");
        }
        StudentProfileData student = await RequireGroupedStudentAsync(userId, cancellationToken);
        ReviewRoundData round = await RequireRoundAsync(roundId, cancellationToken);
        EnsureOpen(round, "Registration is not open for this review round.");
        rules.ValidateStudentAvailability(slots.Select(x => (x.DayOfWeek, x.Slot)).ToArray());
        if (!student.IsLeader)
        {
            throw new BusinessRuleException("Only the group leader can register a review slot.");
        }
        if (student.GroupSemesterId != round.SemesterId)
        {
            throw new BusinessRuleException("Your capstone group does not belong to this review round's semester.");
        }

        await store.ReplaceStudentAvailabilityAsync(
            roundId, student.GroupId!.Value, student.Id, slots.Distinct().ToArray(), cancellationToken);
        IReadOnlyList<StudentRegistrationData> registrations = await store.GetStudentRegistrationsAsync(roundId, student.GroupId.Value, cancellationToken);
        return MapGrid(round, registrations);
    }

    public async Task ClearStudentAvailabilityAsync(int userId, int roundId, CancellationToken cancellationToken)
    {
        StudentProfileData student = await RequireGroupedStudentAsync(userId, cancellationToken);
        ReviewRoundData round = await RequireRoundAsync(roundId, cancellationToken);
        EnsureOpen(round, "Registration is not open for this review round.");
        if (!student.IsLeader)
        {
            throw new BusinessRuleException("Only the group leader can unregister.");
        }
        if (!await store.ClearStudentAvailabilityAsync(roundId, student.GroupId!.Value, cancellationToken))
        {
            throw new KeyNotFoundException("No registration found for your group in this round.");
        }
    }

    public async Task<IReadOnlyList<StudentScheduleResult>> GetStudentScheduleAsync(int userId, CancellationToken cancellationToken)
    {
        StudentProfileData? student = await store.FindStudentByUserIdAsync(userId, cancellationToken);
        if (student?.GroupId is not int groupId) return [];
        IReadOnlyList<StudentScheduleData> rows = await store.GetStudentScheduleAsync(groupId, cancellationToken);
        return rows.Where(x => x.SessionStatus == ReviewSessionStatus.Published)
            .OrderBy(x => x.SessionDate).ThenBy(x => x.Slot)
            .Select(x => new StudentScheduleResult(
                x.SessionId, x.SessionCode, x.Type, x.SessionDate, x.Slot, x.Room,
                x.SessionStatus, x.GroupStatus, x.CompletedAt)).ToArray();
    }

    public async Task<IReadOnlyList<StudentAttendanceResult>> GetStudentAttendanceAsync(int userId, CancellationToken cancellationToken)
    {
        StudentProfileData? student = await store.FindStudentByUserIdAsync(userId, cancellationToken);
        if (student is null) return [];
        IReadOnlyList<StudentAttendanceData> rows = await store.GetStudentAttendanceAsync(student.Id, cancellationToken);
        return rows.Where(x => x.GroupStatus == ReviewGroupStatus.Completed)
            .OrderByDescending(x => x.MarkedAt)
            .Select(x => new StudentAttendanceResult(x.SessionId, x.SessionCode, x.IsPresent, x.Note, x.MarkedAt))
            .ToArray();
    }

    public async Task<IReadOnlyList<StudentReviewCommentResult>> GetStudentCommentsAsync(
        int userId, int sessionId, CancellationToken cancellationToken)
    {
        StudentProfileData? student = await store.FindStudentByUserIdAsync(userId, cancellationToken);
        if (student?.GroupId is not int groupId) return [];
        StudentCommentAccessData? access = await store.GetStudentCommentAccessAsync(sessionId, groupId, cancellationToken);
        if (access is null || access.SessionStatus != ReviewSessionStatus.Published || access.GroupStatus != ReviewGroupStatus.Completed)
        {
            return [];
        }
        IReadOnlyList<StudentCommentData> comments = await store.GetStudentCommentsAsync(sessionId, groupId, cancellationToken);
        return comments.Select(x => new StudentReviewCommentResult(
            x.Id, x.AuthorUserId, x.AuthorName, x.Content, x.CreatedAt, x.UpdatedAt)).ToArray();
    }

    private async Task<int> RequireLecturerIdAsync(int userId, CancellationToken cancellationToken) =>
        await store.FindLecturerIdByUserIdAsync(userId, cancellationToken)
        ?? throw new UnauthorizedAccessException("A lecturer profile is required.");

    private async Task<ReviewRoundData> RequireRoundAsync(int roundId, CancellationToken cancellationToken) =>
        await store.FindRoundAsync(roundId, cancellationToken)
        ?? throw new KeyNotFoundException("Review round not found.");

    private async Task<StudentProfileData> RequireGroupedStudentAsync(int userId, CancellationToken cancellationToken)
    {
        StudentProfileData? student = await store.FindStudentByUserIdAsync(userId, cancellationToken);
        return student?.GroupId is not null
            ? student
            : throw new BusinessRuleException("You must be a member of a capstone group to register.");
    }

    private static void EnsureOpen(ReviewRoundData round, string message)
    {
        if (round.Status != ReviewRoundStatus.Open && round.Status != ReviewRoundStatus.Draft)
            throw new BusinessRuleException(message);
    }

    private static ReviewAvailabilityWeekResult MapLecturerAvailability(
        ReviewRoundData round, int lecturerId, LecturerAvailabilityData state) =>
        new(round.Id, round.SemesterId, lecturerId, round.WeekStartDate,
            state.SubmittedAt is not null, state.SubmittedAt, state.Slots);

    private static StudentSlotGridResult MapGrid(
        ReviewRoundData round, IReadOnlyCollection<StudentRegistrationData> registrations) =>
        new(round.Id, round.Type, round.Status, round.WeekStartDate, round.WeekEndDate,
            AssignmentRules.RequiredStudentAvailabilityCount,
            registrations.Select(x => new StudentSlotRegistrationResult(
                x.Id, x.GroupId, x.GroupCode, x.DayOfWeek, x.Slot)).ToArray());
}

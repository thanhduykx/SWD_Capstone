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
        IReadOnlyList<SlotRegistrationCount> counts = await store.GetLecturerSlotRegistrationCountsAsync(roundId, cancellationToken);
        return MapLecturerAvailability(round, lecturerId, state, counts);
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
        ReviewAvailabilitySlot[] distinctSlots = slots.Distinct().OrderBy(x => x.DayOfWeek).ThenBy(x => x.Slot).ToArray();
        foreach (ReviewAvailabilitySlot? slot in distinctSlots)
        {
            if (slot.DayOfWeek is < 1 or > 6)
            {
                throw new BusinessRuleException("Review availability dayOfWeek must be between 1 and 6.");
            }
            rules.ValidateReviewSlot(slot.Slot);
        }

        ReviewRoundData round = await RequireRoundAsync(roundId, cancellationToken);
        EnsureOpen(round, "Lecturer availability can only be changed while registration is Open.");
        await store.ReplaceLecturerAvailabilityAsync(
            round, lecturerId, distinctSlots, DateTime.UtcNow, cancellationToken);
        IReadOnlyList<SlotRegistrationCount> counts =
            await store.GetLecturerSlotRegistrationCountsAsync(roundId, cancellationToken);
        return new ReviewAvailabilityWeekResult(
            round.Id, round.SemesterId, lecturerId, round.WeekStartDate, false, null, distinctSlots,
            AssignmentRules.MaxLecturersPerAvailabilitySlot, counts);
    }

    public async Task<ReviewAvailabilityWeekResult> SubmitLecturerAvailabilityAsync(
        int userId, int roundId, CancellationToken cancellationToken)
    {
        var lecturerId = await RequireLecturerIdAsync(userId, cancellationToken);
        return await store.ExecuteLockedAsync(roundId, async token =>
        {
            ReviewRoundData round = await RequireRoundAsync(roundId, token);
            EnsureOpen(round, "Lecturer availability can only be submitted while registration is Open.");
            LecturerAvailabilityData state = await store.GetLecturerAvailabilityAsync(roundId, lecturerId, token);
            if (state.Slots.Count == 0)
            {
                throw new BusinessRuleException("At least one availability slot is required before submitting to moderator.");
            }
            IReadOnlyList<SlotRegistrationCount> counts =
                await store.GetLecturerSlotRegistrationCountsAsync(roundId, token);
            IReadOnlyCollection<ReviewAvailabilitySlot> currentOfficialSlots =
                state.SubmittedAt is null ? [] : state.Slots;
            rules.EnsureSlotRegistrationCapacity(
                state.Slots,
                currentOfficialSlots,
                counts,
                AssignmentRules.MaxLecturersPerAvailabilitySlot,
                "lecturer");
            DateTime submittedAt = DateTime.UtcNow;
            await store.SubmitLecturerAvailabilityAsync(round, lecturerId, submittedAt, token);
            counts = await store.GetLecturerSlotRegistrationCountsAsync(roundId, token);
            return new ReviewAvailabilityWeekResult(
                round.Id, round.SemesterId, lecturerId, round.WeekStartDate, true, submittedAt, state.Slots,
                AssignmentRules.MaxLecturersPerAvailabilitySlot, counts);
        }, cancellationToken);
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
            x.RegistrationCount == AssignmentRules.RequiredStudentAvailabilityCount,
            student?.IsLeader ?? false,
            student?.GroupId,
            student?.GroupCode)).ToArray();
    }

    public async Task<StudentSlotGridResult> GetStudentSlotGridAsync(
        int userId, int roundId, CancellationToken cancellationToken)
    {
        StudentProfileData? student = await store.FindStudentByUserIdAsync(userId, cancellationToken);
        ReviewRoundData round = await RequireRoundAsync(roundId, cancellationToken);
        IReadOnlyList<StudentRegistrationData> registrations = student?.GroupId is int groupId
            ? await store.GetStudentRegistrationsAsync(roundId, groupId, cancellationToken)
            : [];
        IReadOnlyList<SlotRegistrationCount> counts = await store.GetStudentSlotRegistrationCountsAsync(roundId, cancellationToken);
        return MapGrid(round, registrations, counts, student?.IsLeader ?? false, student?.GroupId, student?.GroupCode);
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
        rules.ValidateStudentAvailability(slots.Select(x => (x.DayOfWeek, x.Slot)).ToArray());
        if (!student.IsLeader)
        {
            throw new BusinessRuleException("Only the group leader can register a review slot.");
        }
        ReviewAvailabilitySlot[] distinctSlots = slots.Distinct().OrderBy(x => x.DayOfWeek).ThenBy(x => x.Slot).ToArray();
        return await store.ExecuteLockedAsync(roundId, async token =>
        {
            ReviewRoundData round = await RequireRoundAsync(roundId, token);
            EnsureOpen(round, "Registration is not open for this review round.");
            if (student.GroupSemesterId != round.SemesterId)
            {
                throw new BusinessRuleException("Your capstone group does not belong to this review round's semester.");
            }
            IReadOnlyList<StudentRegistrationData> current = await store.GetStudentRegistrationsAsync(
                roundId, student.GroupId!.Value, token);
            IReadOnlyList<SlotRegistrationCount> counts = await store.GetStudentSlotRegistrationCountsAsync(roundId, token);
            rules.EnsureSlotRegistrationCapacity(
                distinctSlots,
                current.Select(x => new ReviewAvailabilitySlot(x.DayOfWeek, x.Slot)).ToArray(),
                counts,
                AssignmentRules.MaxGroupsPerAvailabilitySlot,
                "group");
            await store.ReplaceStudentAvailabilityAsync(
                roundId, student.GroupId.Value, student.Id, distinctSlots, token);
            IReadOnlyList<StudentRegistrationData> registrations = await store.GetStudentRegistrationsAsync(
                roundId, student.GroupId.Value, token);
            counts = await store.GetStudentSlotRegistrationCountsAsync(roundId, token);
            return MapGrid(round, registrations, counts, student.IsLeader, student.GroupId, student.GroupCode);
        }, cancellationToken);
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
                x.SessionId, x.SessionCode, x.GroupId, x.GroupCode,
                x.Type, x.SessionDate, x.Slot, x.Room,
                x.SessionStatus, x.GroupStatus, x.CompletedAt)).ToArray();
    }

    public async Task<IReadOnlyList<StudentCheckInResult>> GetStudentCheckInsAsync(
        int userId, CancellationToken cancellationToken)
    {
        StudentProfileData? student = await store.FindStudentByUserIdAsync(userId, cancellationToken);
        if (student?.GroupId is not int groupId) return [];
        DateTime now = DateTime.UtcNow;
        IReadOnlyList<StudentCheckInData> rows = await store.GetStudentCheckInsAsync(student.Id, groupId, cancellationToken);
        return rows.Where(x => x.SessionStatus == ReviewSessionStatus.Published)
            .OrderBy(x => x.SessionDate).ThenBy(x => x.Slot)
            .Select(x => MapCheckIn(x, now)).ToArray();
    }

    public async Task<StudentCheckInResult> ConfirmStudentCheckInAsync(
        int userId, int sessionId, CancellationToken cancellationToken)
    {
        StudentProfileData? student = await store.FindStudentByUserIdAsync(userId, cancellationToken);
        if (student?.GroupId is not int groupId)
            throw new BusinessRuleException("You must belong to a capstone group to confirm attendance.");
        IReadOnlyList<StudentCheckInData> rows = await store.GetStudentCheckInsAsync(student.Id, groupId, cancellationToken);
        StudentCheckInData row = rows.FirstOrDefault(x => x.SessionId == sessionId)
            ?? throw new KeyNotFoundException("The review session is not assigned to your group.");
        if (row.SessionStatus != ReviewSessionStatus.Published)
            throw new BusinessRuleException("Only a published review session can be confirmed.");
        if (row.GroupStatus == ReviewGroupStatus.Completed)
            throw new BusinessRuleException("This review has ended. Missing signatures are recorded as absent.");
        DateTime now = DateTime.UtcNow;
        if (!ReviewSessionTiming.HasStarted(row.SessionDate, row.Slot, now))
            throw new BusinessRuleException("You can sign attendance when the review session starts.");
        DateTime confirmedAt = row.ConfirmedAt ?? await store.ConfirmStudentCheckInAsync(
            userId, student.Id, groupId, sessionId, now, cancellationToken);
        return MapCheckIn(row with { ConfirmedAt = confirmedAt }, now);
    }

    private static StudentCheckInResult MapCheckIn(StudentCheckInData row, DateTime now) =>
        new(row.SessionId, row.SessionCode, row.Type, row.SessionDate, row.Slot, row.Room,
            row.GroupStatus, row.ConfirmedAt,
            row.GroupStatus != ReviewGroupStatus.Completed && row.ConfirmedAt is null &&
            ReviewSessionTiming.HasStarted(row.SessionDate, row.Slot, now));

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
        ReviewRoundData round, int lecturerId, LecturerAvailabilityData state,
        IReadOnlyCollection<SlotRegistrationCount> counts) =>
        new(round.Id, round.SemesterId, lecturerId, round.WeekStartDate,
            state.SubmittedAt is not null, state.SubmittedAt, state.Slots,
            AssignmentRules.MaxLecturersPerAvailabilitySlot, counts);

    private static StudentSlotGridResult MapGrid(
        ReviewRoundData round, IReadOnlyCollection<StudentRegistrationData> registrations,
        IReadOnlyCollection<SlotRegistrationCount> counts,
        bool isLeader = false, int? groupId = null, string? groupCode = null) =>
        new(round.Id, round.Type, round.Status, round.WeekStartDate, round.WeekEndDate,
            AssignmentRules.RequiredStudentAvailabilityCount,
            registrations.Select(x => new StudentSlotRegistrationResult(
                x.Id, x.GroupId, x.GroupCode, x.DayOfWeek, x.Slot)).ToArray(),
            AssignmentRules.MaxGroupsPerAvailabilitySlot,
            counts,
            isLeader, groupId, groupCode);
}

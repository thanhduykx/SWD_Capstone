using System.Globalization;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Services;

namespace CPMS.BLL.Reviews.Scheduling;

public sealed class ReviewAssignmentService(IReviewAssignmentStore store, AssignmentRules rules)
    : IReviewAssignmentService
{
    public Task<GenerateReviewAssignmentsResult> GenerateAsync(
        GenerateReviewAssignmentsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var minimumReviewers = command.ReviewersPerSession == 0 ? 2 : command.ReviewersPerSession;
        if (minimumReviewers is < 2 or > AssignmentRules.MaxLecturersPerAvailabilitySlot)
        {
            throw new BusinessRuleException(
                $"Minimum reviewers per session must be between 2 and {AssignmentRules.MaxLecturersPerAvailabilitySlot}.");
        }

        var scope = new ReviewAssignmentScope(
            command.SemesterId, command.ReviewType, ReviewWeek.NormalizeStart(command.WeekStart));
        return store.ExecuteLockedAsync(
            scope,
            token => GenerateLockedAsync(scope, minimumReviewers, command.RoomPrefix, command.Seed, token),
            cancellationToken);
    }

    private async Task<GenerateReviewAssignmentsResult> GenerateLockedAsync(
        ReviewAssignmentScope scope,
        int minimumReviewers,
        string? requestedRoomPrefix,
        int? seed,
        CancellationToken cancellationToken)
    {
        ReviewAssignmentSnapshot snapshot = await store.LoadSnapshotAsync(scope, cancellationToken);
        ValidateSnapshot(snapshot);
        if (snapshot.Groups.Count == 0)
        {
            return new GenerateReviewAssignmentsResult(0, 0, [], []);
        }

#pragma warning disable S2245 // Scheduling randomization is non-security-sensitive; seeds enable deterministic tests.
        Random random = seed.HasValue ? new Random(seed.Value) : new Random();
#pragma warning restore S2245
        var usedCodes = snapshot.UsedSessionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var occupiedSlots = snapshot.OccupiedReviewerSlots
            .Select(x => OccupiedKey(x.LecturerId, x.SessionDate, x.Slot))
            .ToHashSet(StringComparer.Ordinal);
        var panels = snapshot.ReusablePanels.Select(x => new MutablePanel(x)).ToList();
        var assignedSessions = new List<ReviewSchedulingSession>();
        var unassignedGroups = new List<UnassignedReviewGroup>();
        var roomPrefix = string.IsNullOrWhiteSpace(requestedRoomPrefix) ? "AUTO" : requestedRoomPrefix.Trim();

        foreach (AssignmentGroup? group in Shuffle(snapshot.Groups, random))
        {
            if (!TryGetStudentAvailability(snapshot, group.Id, out IReadOnlySet<string> groupAvailability))
            {
                unassignedGroups.Add(new UnassignedReviewGroup(
                    group.Id, group.Code, "The group leader did not submit exactly five available slots."));
                continue;
            }

            IReadOnlySet<int> previousReviewerIds = snapshot.PreviousReviewerIdsByGroup.TryGetValue(group.Id, out IReadOnlySet<int>? previous)
                ? previous
                : new HashSet<int>();
            MutablePanel? selectedPanel = FindReusablePanel(
                panels, group, groupAvailability, previousReviewerIds, minimumReviewers);
            selectedPanel ??= await CreatePanelAsync(
                scope, snapshot, group, groupAvailability, previousReviewerIds,
                minimumReviewers, roomPrefix, panels, occupiedSlots, usedCodes, random,
                cancellationToken);
            if (selectedPanel is null)
            {
                unassignedGroups.Add(new UnassignedReviewGroup(
                    group.Id, group.Code, "No shared student/lecturer slot satisfies reviewer conflict rules."));
                continue;
            }

            rules.EnsureReviewSessionCapacity(selectedPanel.GroupCount);
            await store.AssignGroupAsync(
                selectedPanel.SessionId,
                group.Id,
                selectedPanel.GroupCount + 1,
                selectedPanel.ReviewerIds,
                scope.ReviewType,
                DateTime.UtcNow,
                cancellationToken);
            selectedPanel.GroupCount++;
            assignedSessions.Add(selectedPanel.ToResult(group));
        }

        return new GenerateReviewAssignmentsResult(
            snapshot.Groups.Count, assignedSessions.Count, unassignedGroups, assignedSessions);
    }

    private static void ValidateSnapshot(ReviewAssignmentSnapshot snapshot)
    {
        if (!snapshot.SemesterExists) throw new BusinessRuleException("Semester does not exist.");
        if (snapshot.RoundId is null)
            throw new BusinessRuleException("The selected review round does not exist.");
        if (snapshot.RoundStatus != ReviewRoundStatus.Closed)
            throw new ConflictException("The review round must be Closed before assignments can be generated.");
        if (snapshot.SubmittedAvailability.Count == 0)
            throw new BusinessRuleException("No submitted lecturer availability exists for this week.");
    }

    private static bool TryGetStudentAvailability(
        ReviewAssignmentSnapshot snapshot,
        int groupId,
        out IReadOnlySet<string> availability)
    {
        if (snapshot.StudentAvailabilityByGroup.TryGetValue(groupId, out IReadOnlySet<string>? selected) &&
            selected.Count == AssignmentRules.RequiredStudentAvailabilityCount)
        {
            availability = selected;
            return true;
        }
        availability = new HashSet<string>();
        return false;
    }

    private static MutablePanel? FindReusablePanel(
        IEnumerable<MutablePanel> panels,
        AssignmentGroup group,
        IReadOnlySet<string> groupAvailability,
        IReadOnlySet<int> previousReviewerIds,
        int minimumReviewers) => panels
        .Where(panel => panel.GroupCount < AssignmentRules.MaxGroupsPerReviewSession &&
                        panel.ReviewerIds.Count >= minimumReviewers &&
                        panel.ReviewerIds.Count <= AssignmentRules.MaxLecturersPerAvailabilitySlot &&
                        groupAvailability.Contains(SlotKey(panel.DayOfWeek, panel.Slot)) &&
                        !panel.ReviewerIds.Contains(group.SupervisorId) &&
                        !panel.ReviewerIds.Any(previousReviewerIds.Contains))
        .OrderByDescending(panel => panel.GroupCount)
        .FirstOrDefault();

    private async Task<MutablePanel?> CreatePanelAsync(
        ReviewAssignmentScope scope,
        ReviewAssignmentSnapshot snapshot,
        AssignmentGroup group,
        IReadOnlySet<string> groupAvailability,
        IReadOnlySet<int> previousReviewerIds,
        int minimumReviewers,
        string roomPrefix,
        List<MutablePanel> panels,
        HashSet<string> occupiedSlots,
        HashSet<string> usedCodes,
        Random random,
        CancellationToken cancellationToken)
    {
        var eligibleSlots = snapshot.SubmittedAvailability
            .Where(slot => slot.LecturerId != group.SupervisorId &&
                           groupAvailability.Contains(SlotKey(slot.DayOfWeek, slot.Slot)) &&
                           !previousReviewerIds.Contains(slot.LecturerId) &&
                           !occupiedSlots.Contains(OccupiedKey(
                               slot.LecturerId, SessionDate(scope.WeekStart, slot.DayOfWeek), slot.Slot)))
            .GroupBy(slot => new { slot.DayOfWeek, slot.Slot })
            .Where(groupedSlots =>
            {
                var count = groupedSlots.Select(slot => slot.LecturerId).Distinct().Count();
                return count >= minimumReviewers && count <= AssignmentRules.MaxLecturersPerAvailabilitySlot;
            })
            .ToArray();
        if (eligibleSlots.Length == 0) return null;

        var selectedSlot = Shuffle(eligibleSlots, random)[0];
        int[] reviewerIds = selectedSlot.Select(x => x.LecturerId).Distinct().Order().ToArray();
        rules.ValidateReviewAssignment(group.SupervisorId, reviewerIds, previousReviewerIds);
        DateTime sessionDate = SessionDate(scope.WeekStart, selectedSlot.Key.DayOfWeek);
        AssignmentPanel created = await store.CreatePanelAsync(
            new CreateAssignmentPanel(
                NextSessionCode(snapshot.SemesterCode!, scope.ReviewType, group.Code, sessionDate,
                    selectedSlot.Key.Slot, usedCodes),
                scope.SemesterId, selectedSlot.Key.DayOfWeek, selectedSlot.Key.Slot,
                $"{roomPrefix}-{selectedSlot.Key.DayOfWeek}{selectedSlot.Key.Slot}-{panels.Count + 1:D2}",
                scope.ReviewType, reviewerIds, sessionDate),
            cancellationToken);
        var panel = new MutablePanel(created);
        panels.Add(panel);
        foreach (var reviewerId in reviewerIds)
            occupiedSlots.Add(OccupiedKey(reviewerId, sessionDate, panel.Slot));
        return panel;
    }

    private static DateTime SessionDate(DateOnly weekStart, int dayOfWeek) =>
        DateTime.SpecifyKind(weekStart.AddDays(dayOfWeek - 1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    private static string OccupiedKey(int lecturerId, DateTime sessionDate, int slot) =>
        $"{lecturerId}:{sessionDate.Date:yyyy-MM-dd}:{slot}";
    private static string SlotKey(int dayOfWeek, int slot) => $"{dayOfWeek}:{slot}";

    private static string NextSessionCode(
        string semesterCode,
        ReviewType reviewType,
        string groupCode,
        DateTime sessionDate,
        int slot,
        HashSet<string> usedCodes)
    {
        var baseCode = $"{reviewType}-{semesterCode}-{string.Concat(groupCode.Where(char.IsLetterOrDigit))}-{sessionDate:yyyyMMdd}-S{slot}";
        var code = baseCode;
        var index = 2;
        while (!usedCodes.Add(code))
        {
            code = $"{baseCode}-{index.ToString(CultureInfo.InvariantCulture)}";
            index++;
        }
        return code;
    }

    private static T[] Shuffle<T>(IEnumerable<T> source, Random random)
    {
        T[] items = source.ToArray();
        for (var index = items.Length - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
        return items;
    }

    private sealed class MutablePanel(AssignmentPanel panel)
    {
        public int SessionId { get; } = panel.SessionId;
        public string SessionCode { get; } = panel.Code;
        public ReviewType Type { get; } = panel.Type;
        public ReviewSessionStatus Status { get; } = panel.Status;
        public IReadOnlyList<int> ReviewerIds { get; } = panel.ReviewerIds;
        public DateTime ScheduledAt { get; } = panel.SessionDate;
        public int DayOfWeek { get; } = panel.DayOfWeek;
        public int Slot { get; } = panel.Slot;
        public string Room { get; } = panel.Room;
        public int GroupCount { get; set; } = panel.GroupCount;

        public ReviewSchedulingSession ToResult(AssignmentGroup group) =>
            new(SessionId, SessionCode, group.Id, group.Code, Type, Status, ReviewerIds,
                ScheduledAt, DayOfWeek, Slot, Room);
    }
}

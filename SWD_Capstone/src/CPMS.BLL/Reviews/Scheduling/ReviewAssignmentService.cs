using System.Globalization;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Services;

namespace CPMS.BLL.Reviews.Scheduling;

public sealed class ReviewAssignmentService(IReviewAssignmentStore store)
    : IReviewAssignmentService
{
    public Task<GenerateReviewAssignmentsResult> GenerateAsync(
        GenerateReviewAssignmentsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var minimumReviewers = command.ReviewersPerSession == 0 ? 2 : command.ReviewersPerSession;
        if (minimumReviewers is < 2 or > AssignmentRules.MaxLecturersPerAvailabilitySlot)
        {
            minimumReviewers = Math.Min(Math.Max(minimumReviewers, 2), AssignmentRules.MaxLecturersPerAvailabilitySlot);
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

        // Track session count per lecturer to ensure equal workload distribution & assign everyone
        var assignedCounts = snapshot.SubmittedAvailability
            .Select(x => x.LecturerId)
            .Distinct()
            .ToDictionary(id => id, _ => 0);

        // Pre-count existing panel reviewer assignments if any
        foreach (MutablePanel panel in panels)
        {
            foreach (int revId in panel.ReviewerIds)
            {
                if (assignedCounts.ContainsKey(revId))
                {
                    assignedCounts[revId]++;
                }
                else
                {
                    assignedCounts[revId] = 1;
                }
            }
        }

        var panelContext = new PanelCreationContext(
            scope, snapshot, roomPrefix, panels, occupiedSlots, usedCodes, assignedCounts, random);

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
                panelContext, group, groupAvailability, previousReviewerIds,
                minimumReviewers, cancellationToken);
            if (selectedPanel is null)
            {
                unassignedGroups.Add(new UnassignedReviewGroup(
                    group.Id, group.Code, "No shared student/lecturer slot satisfies reviewer conflict rules."));
                continue;
            }

            AssignmentRules.EnsureReviewSessionCapacity(selectedPanel.GroupCount);
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

        // Equalize/Post-process: Ensure all registered lecturers are assigned if any were missed
        EnsureAllLecturersAssigned(panelContext);

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
        PanelCreationContext context,
        AssignmentGroup group,
        IReadOnlySet<string> groupAvailability,
        IReadOnlySet<int> previousReviewerIds,
        int minimumReviewers,
        CancellationToken cancellationToken)
    {
        var eligibleSlotGroups = context.Snapshot.SubmittedAvailability
            .Where(slot => slot.LecturerId != group.SupervisorId &&
                           groupAvailability.Contains(SlotKey(slot.DayOfWeek, slot.Slot)) &&
                           !previousReviewerIds.Contains(slot.LecturerId) &&
                           !context.OccupiedSlots.Contains(OccupiedKey(
                               slot.LecturerId, SessionDate(context.Scope.WeekStart, slot.DayOfWeek), slot.Slot)))
            .GroupBy(slot => new { slot.DayOfWeek, slot.Slot })
            .Select(g =>
            {
                // Order available lecturers in this slot by lowest current workload first (to balance workload)
                var lecturers = g.Select(s => s.LecturerId)
                    .Distinct()
                    .OrderBy(id => context.AssignedCounts.GetValueOrDefault(id, 0))
                    .ThenBy(_ => context.Random.Next())
                    .ToList();
                return new { SlotInfo = g.Key, AvailableLecturers = lecturers };
            })
            .Where(x => x.AvailableLecturers.Count >= minimumReviewers)
            .ToArray();

        if (eligibleSlotGroups.Length == 0) return null;

        // Rank slot candidates: Prioritize slots with unassigned lecturers (count == 0) and lowest average workload
        var selectedCandidate = eligibleSlotGroups
            .OrderByDescending(x => x.AvailableLecturers.Count(id => context.AssignedCounts.GetValueOrDefault(id, 0) == 0))
            .ThenBy(x => x.AvailableLecturers.Take(AssignmentRules.MaxLecturersPerAvailabilitySlot).Average(id => context.AssignedCounts.GetValueOrDefault(id, 0)))
            .ThenBy(_ => context.Random.Next())
            .First();

        // Select top reviewers (min 2, max 3)
        int targetReviewerCount = Math.Min(selectedCandidate.AvailableLecturers.Count, AssignmentRules.MaxLecturersPerAvailabilitySlot);
        int[] reviewerIds = selectedCandidate.AvailableLecturers
            .Take(targetReviewerCount)
            .Order()
            .ToArray();

        AssignmentRules.ValidateReviewAssignment(group.SupervisorId, reviewerIds, previousReviewerIds);
        DateTime sessionDate = SessionDate(context.Scope.WeekStart, selectedCandidate.SlotInfo.DayOfWeek);
        AssignmentPanel created = await store.CreatePanelAsync(
            new CreateAssignmentPanel(
                NextSessionCode(context.Snapshot.SemesterCode!, context.Scope.ReviewType, group.Code, sessionDate,
                    selectedCandidate.SlotInfo.Slot, context.UsedCodes),
                context.Scope.SemesterId, selectedCandidate.SlotInfo.DayOfWeek, selectedCandidate.SlotInfo.Slot,
                $"{context.RoomPrefix}-{selectedCandidate.SlotInfo.DayOfWeek}{selectedCandidate.SlotInfo.Slot}-{context.Panels.Count + 1:D2}",
                context.Scope.ReviewType, reviewerIds, sessionDate),
            cancellationToken);

        var panel = new MutablePanel(created);
        context.Panels.Add(panel);

        foreach (var reviewerId in reviewerIds)
        {
            context.OccupiedSlots.Add(OccupiedKey(reviewerId, sessionDate, panel.Slot));
            context.AssignedCounts[reviewerId] = context.AssignedCounts.GetValueOrDefault(reviewerId, 0) + 1;
        }

        return panel;
    }

    private static void EnsureAllLecturersAssigned(PanelCreationContext context)
    {
        var unassignedLecturers = context.AssignedCounts
            .Where(kvp => kvp.Value == 0)
            .Select(kvp => kvp.Key)
            .ToList();

        if (unassignedLecturers.Count == 0) return;

        foreach (int unassignedId in unassignedLecturers)
        {
            var lecturerSlots = context.Snapshot.SubmittedAvailability
                .Where(s => s.LecturerId == unassignedId)
                .ToList();

            foreach (var slot in lecturerSlots)
            {
                DateTime date = SessionDate(context.Scope.WeekStart, slot.DayOfWeek);
                if (context.OccupiedSlots.Contains(OccupiedKey(unassignedId, date, slot.Slot)))
                    continue;

                // Find a panel in this slot that has room (< 3 reviewers)
                var candidatePanel = context.Panels
                    .FirstOrDefault(p => p.DayOfWeek == slot.DayOfWeek && p.Slot == slot.Slot &&
                                         p.ReviewerIds.Count < AssignmentRules.MaxLecturersPerAvailabilitySlot &&
                                         !p.ReviewerIds.Contains(unassignedId));

                if (candidatePanel is not null)
                {
                    candidatePanel.ReviewerIds = candidatePanel.ReviewerIds.Concat([unassignedId]).Order().ToList();
                    context.OccupiedSlots.Add(OccupiedKey(unassignedId, date, slot.Slot));
                    context.AssignedCounts[unassignedId] = 1;
                    break;
                }
            }
        }
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

    private sealed record PanelCreationContext(
        ReviewAssignmentScope Scope,
        ReviewAssignmentSnapshot Snapshot,
        string RoomPrefix,
        List<MutablePanel> Panels,
        HashSet<string> OccupiedSlots,
        HashSet<string> UsedCodes,
        Dictionary<int, int> AssignedCounts,
        Random Random);

    private sealed class MutablePanel(AssignmentPanel panel)
    {
        public int SessionId { get; } = panel.SessionId;
        public string SessionCode { get; } = panel.Code;
        public ReviewType Type { get; } = panel.Type;
        public ReviewSessionStatus Status { get; } = panel.Status;
        public IReadOnlyList<int> ReviewerIds { get; set; } = panel.ReviewerIds;
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

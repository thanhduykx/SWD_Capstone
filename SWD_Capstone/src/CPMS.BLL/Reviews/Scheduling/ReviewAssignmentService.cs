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
        var reviewersPerSession = command.ReviewersPerSession == 0 ? 2 : command.ReviewersPerSession;
        if (reviewersPerSession is < 1 or > 2)
        {
            throw new BusinessRuleException("Reviewers per session must be 1 or 2.");
        }

        var scope = new ReviewAssignmentScope(
            command.SemesterId, command.ReviewType, ReviewWeek.NormalizeStart(command.WeekStart));
        return store.ExecuteLockedAsync(
            scope,
            token => GenerateLockedAsync(scope, reviewersPerSession, command.RoomPrefix, command.Seed, token),
            cancellationToken);
    }

    private async Task<GenerateReviewAssignmentsResult> GenerateLockedAsync(
        ReviewAssignmentScope scope,
        int reviewersPerSession,
        string? requestedRoomPrefix,
        int? seed,
        CancellationToken cancellationToken)
    {
        var snapshot = await store.LoadSnapshotAsync(scope, cancellationToken);
        if (!snapshot.SemesterExists)
        {
            throw new BusinessRuleException("Semester does not exist.");
        }
        if (snapshot.RoundId is null)
        {
            throw new BusinessRuleException("The selected review round does not exist.");
        }
        if (snapshot.RoundStatus != ReviewRoundStatus.Closed)
        {
            throw new ConflictException("The review round must be Closed before assignments can be generated.");
        }
        if (snapshot.SubmittedAvailability.Count == 0)
        {
            throw new BusinessRuleException("No submitted lecturer availability exists for this week.");
        }
        if (snapshot.Groups.Count == 0)
        {
            return new GenerateReviewAssignmentsResult(0, 0, [], []);
        }

        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var usedCodes = snapshot.UsedSessionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var occupiedSlots = snapshot.OccupiedReviewerSlots
            .Select(x => OccupiedKey(x.LecturerId, x.SessionDate, x.Slot))
            .ToHashSet(StringComparer.Ordinal);
        var panels = snapshot.ReusablePanels.Select(x => new MutablePanel(x)).ToList();
        var assignedSessions = new List<ReviewSchedulingSession>();
        var unassignedGroups = new List<UnassignedReviewGroup>();
        var roomPrefix = string.IsNullOrWhiteSpace(requestedRoomPrefix) ? "AUTO" : requestedRoomPrefix.Trim();

        foreach (var group in Shuffle(snapshot.Groups, random))
        {
            var groupAvailability = snapshot.StudentAvailabilityByGroup.TryGetValue(group.Id, out var ga)
                ? ga
                : new HashSet<string>();

            var previousReviewerIds = snapshot.PreviousReviewerIdsByGroup.TryGetValue(group.Id, out var previous)
                ? previous
                : new HashSet<int>();
            var selectedPanel = panels
                .Where(panel => panel.GroupCount < 3 &&
                                panel.ReviewerIds.Count == reviewersPerSession &&
                                (groupAvailability.Count == 0 || groupAvailability.Contains(SlotKey(panel.DayOfWeek, panel.Slot))) &&
                                !panel.ReviewerIds.Contains(group.SupervisorId) &&
                                !panel.ReviewerIds.Any(previousReviewerIds.Contains))
                .OrderByDescending(panel => panel.GroupCount)
                .FirstOrDefault();

            if (selectedPanel is null)
            {
                var eligibleSlots = snapshot.SubmittedAvailability
                    .Where(slot => slot.LecturerId != group.SupervisorId &&
                                   (groupAvailability.Count == 0 || groupAvailability.Contains(SlotKey(slot.DayOfWeek, slot.Slot))) &&
                                   !previousReviewerIds.Contains(slot.LecturerId) &&
                                   !occupiedSlots.Contains(OccupiedKey(
                                       slot.LecturerId,
                                       SessionDate(scope.WeekStart, slot.DayOfWeek),
                                       slot.Slot)))
                    .GroupBy(slot => new { slot.DayOfWeek, slot.Slot })
                    .Where(slotGroup => slotGroup.Select(slot => slot.LecturerId).Distinct().Count() >= reviewersPerSession)
                    .ToArray();

                if (eligibleSlots.Length == 0 && groupAvailability.Count > 0)
                {
                    // Fallback: try any slot where enough reviewers are available, ignoring student preference if needed
                    eligibleSlots = snapshot.SubmittedAvailability
                        .Where(slot => slot.LecturerId != group.SupervisorId &&
                                       !previousReviewerIds.Contains(slot.LecturerId) &&
                                       !occupiedSlots.Contains(OccupiedKey(
                                           slot.LecturerId,
                                           SessionDate(scope.WeekStart, slot.DayOfWeek),
                                           slot.Slot)))
                        .GroupBy(slot => new { slot.DayOfWeek, slot.Slot })
                        .Where(slotGroup => slotGroup.Select(slot => slot.LecturerId).Distinct().Count() >= reviewersPerSession)
                        .ToArray();
                }

                if (eligibleSlots.Length == 0)
                {
                    var availableLecturersCount = snapshot.SubmittedAvailability.Select(s => s.LecturerId).Distinct().Count();
                    unassignedGroups.Add(new UnassignedReviewGroup(
                        group.Id, group.Code, $"Chưa đủ giảng viên rảnh trong cùng 1 slot (cần tối thiểu {reviewersPerSession} giảng viên rảnh khác GVHD để lập hội đồng, hiện mới có {availableLecturersCount} giảng viên khai báo rảnh)."));
                    continue;
                }

                var selectedSlot = Shuffle(eligibleSlots, random)[0];
                var reviewerIds = Shuffle(selectedSlot.Select(x => x.LecturerId).Distinct(), random)
                    .Take(reviewersPerSession).ToArray();
                rules.ValidateReviewAssignment(group.SupervisorId, reviewerIds, previousReviewerIds);
                var sessionDate = SessionDate(scope.WeekStart, selectedSlot.Key.DayOfWeek);
                var createdPanel = await store.CreatePanelAsync(
                    new CreateAssignmentPanel(
                        NextSessionCode(snapshot.SemesterCode!, scope.ReviewType, group.Code, sessionDate,
                            selectedSlot.Key.Slot, usedCodes),
                        scope.SemesterId,
                        selectedSlot.Key.DayOfWeek,
                        selectedSlot.Key.Slot,
                        $"{roomPrefix}-{selectedSlot.Key.DayOfWeek}{selectedSlot.Key.Slot}-{panels.Count + 1:D2}",
                        scope.ReviewType,
                        reviewerIds,
                        sessionDate),
                    cancellationToken);
                selectedPanel = new MutablePanel(createdPanel);
                panels.Add(selectedPanel);
                foreach (var reviewerId in reviewerIds)
                {
                    occupiedSlots.Add(OccupiedKey(reviewerId, sessionDate, selectedPanel.Slot));
                }
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
        var items = source.ToArray();
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
        public string Code { get; } = panel.Code;
        public ReviewType Type { get; } = panel.Type;
        public ReviewSessionStatus Status { get; } = panel.Status;
        public IReadOnlyList<int> ReviewerIds { get; } = panel.ReviewerIds;
        public DateTime SessionDate { get; } = panel.SessionDate;
        public int DayOfWeek { get; } = panel.DayOfWeek;
        public int Slot { get; } = panel.Slot;
        public string Room { get; } = panel.Room;
        public int GroupCount { get; set; } = panel.GroupCount;

        public ReviewSchedulingSession ToResult(AssignmentGroup group) =>
            new(SessionId, Code, group.Id, group.Code, Type, Status, ReviewerIds,
                SessionDate, DayOfWeek, Slot, Room);
    }
}

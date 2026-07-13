using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Reviews.Scheduling;
using CPMS.Core.Enums;
using CPMS.Core.Services;

namespace CPMS.UnitTests.Services;

public sealed class ReviewAssignmentServiceTests
{
    [Fact]
    public async Task Generate_RejectsRoundThatIsNotClosed()
    {
        var store = new FakeStore { Snapshot = Snapshot(roundStatus: ReviewRoundStatus.Open) };
        var service = new ReviewAssignmentService(store, new AssignmentRules());

        await Assert.ThrowsAsync<ConflictException>(() => service.GenerateAsync(Command(), CancellationToken.None));

        Assert.Empty(store.AssignedGroups);
    }

    [Fact]
    public async Task Generate_LeavesGroupUnassignedWhenLeaderHasNotSubmittedFiveSlots()
    {
        var store = new FakeStore { Snapshot = Snapshot(studentSlotCount: 4) };
        var service = new ReviewAssignmentService(store, new AssignmentRules());

        var result = await service.GenerateAsync(Command(), CancellationToken.None);

        Assert.Equal(0, result.AssignedCount);
        Assert.Single(result.UnassignedGroups);
        Assert.Contains("exactly five", result.UnassignedGroups[0].Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_ExcludesSupervisorAndCreatesAssignment()
    {
        var store = new FakeStore { Snapshot = Snapshot() };
        var service = new ReviewAssignmentService(store, new AssignmentRules());

        var result = await service.GenerateAsync(Command(), CancellationToken.None);

        Assert.Equal(1, result.AssignedCount);
        Assert.Single(store.AssignedGroups);
        Assert.DoesNotContain(9, store.AssignedGroups[0].ReviewerIds);
        Assert.Equal(2, store.AssignedGroups[0].ReviewerIds.Count);
    }

    [Fact]
    public async Task Generate_ReusesPanelOnlyUntilThreeGroups()
    {
        var panel = new AssignmentPanel(
            44, "PANEL", ReviewType.Review1, ReviewSessionStatus.Draft, [1, 2],
            new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc), 1, 1, "A", 2);
        var snapshot = Snapshot() with
        {
            Groups = [new(10, "G1", 9), new(11, "G2", 9)],
            StudentAvailabilityByGroup = new Dictionary<int, IReadOnlySet<string>>
            {
                [10] = FiveSlots(),
                [11] = FiveSlots()
            },
            ReusablePanels = [panel]
        };
        var store = new FakeStore { Snapshot = snapshot };
        var service = new ReviewAssignmentService(store, new AssignmentRules());

        var result = await service.GenerateAsync(Command(), CancellationToken.None);

        Assert.Equal(2, result.AssignedCount);
        Assert.Equal(44, store.AssignedGroups[0].SessionId);
        Assert.NotEqual(44, store.AssignedGroups[1].SessionId);
    }

    private static GenerateReviewAssignmentsCommand Command() =>
        new(3, ReviewType.Review1, new DateOnly(2026, 7, 16), 2, "R", 123);

    private static ReviewAssignmentSnapshot Snapshot(
        ReviewRoundStatus roundStatus = ReviewRoundStatus.Closed, int studentSlotCount = 5)
    {
        var slots = FiveSlots().Take(studentSlotCount).ToHashSet(StringComparer.Ordinal);
        return new ReviewAssignmentSnapshot(
            true,
            "SU26",
            5,
            roundStatus,
            [new(1, 1, 1), new(2, 1, 1), new(9, 1, 1)],
            [new(10, "G1", 9)],
            new Dictionary<int, IReadOnlySet<int>> { [10] = new HashSet<int>() },
            [],
            new HashSet<string>(),
            new Dictionary<int, IReadOnlySet<string>> { [10] = slots },
            []);
    }

    private static HashSet<string> FiveSlots() => ["1:1", "1:2", "2:1", "2:2", "3:1"];

    private sealed class FakeStore : IReviewAssignmentStore
    {
        private int _nextSessionId = 100;
        public required ReviewAssignmentSnapshot Snapshot { get; init; }
        public List<AssignedGroup> AssignedGroups { get; } = [];

        public Task<TResult> ExecuteLockedAsync<TResult>(
            ReviewAssignmentScope scope,
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken) => operation(cancellationToken);

        public Task<ReviewAssignmentSnapshot> LoadSnapshotAsync(
            ReviewAssignmentScope scope, CancellationToken cancellationToken) => Task.FromResult(Snapshot);

        public Task<AssignmentPanel> CreatePanelAsync(
            CreateAssignmentPanel panel, CancellationToken cancellationToken) =>
            Task.FromResult(new AssignmentPanel(
                _nextSessionId++, panel.Code, panel.Type, ReviewSessionStatus.Draft, panel.ReviewerIds,
                panel.SessionDate, panel.DayOfWeek, panel.Slot, panel.Room, 0));

        public Task AssignGroupAsync(
            int sessionId,
            int groupId,
            int groupPosition,
            IReadOnlyList<int> reviewerIds,
            ReviewType reviewType,
            DateTime changedAt,
            CancellationToken cancellationToken)
        {
            AssignedGroups.Add(new AssignedGroup(sessionId, groupId, groupPosition, reviewerIds));
            return Task.CompletedTask;
        }
    }

    private sealed record AssignedGroup(
        int SessionId, int GroupId, int GroupPosition, IReadOnlyList<int> ReviewerIds);
}

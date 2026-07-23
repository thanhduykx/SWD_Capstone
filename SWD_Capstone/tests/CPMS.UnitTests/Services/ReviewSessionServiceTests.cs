using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Reviews.Sessions;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Services;

namespace CPMS.UnitTests.Services;

public sealed class ReviewSessionServiceTests
{
    [Fact]
    public async Task Create_RejectsGroupWithoutFiveSlotsIncludingSelectedSlot()
    {
        var store = new FakeStore { GroupsWithAvailability = 0 };
        var service = new ReviewSessionService(store);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(Command(), CancellationToken.None));

        Assert.False(store.Created);
    }

    [Fact]
    public async Task Create_RejectsSupervisorAsReviewer()
    {
        var store = new FakeStore();
        var service = new ReviewSessionService(store);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(Command(reviewers: [9]), CancellationToken.None));

        Assert.False(store.Created);
    }

    [Fact]
    public async Task Create_PersistsValidDraftAssignment()
    {
        var store = new FakeStore();
        var service = new ReviewSessionService(store);

        ReviewSessionResult result = await service.CreateAsync(Command(), CancellationToken.None);

        Assert.True(store.Created);
        Assert.Equal(ReviewSessionStatus.Draft, result.Status);
    }

    [Fact]
    public async Task Update_RejectsNonDraftSession()
    {
        var store = new FakeStore
        {
            EditSession = new ReviewSessionEditState(
                1, 3, ReviewType.Review1, ReviewSessionStatus.Published, [10], [9])
        };
        var service = new ReviewSessionService(store);

        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(
            1,
            new UpdateReviewSessionCommand(
                "RV", [1, 2], [], 1, "A", DateTime.UtcNow, ReviewSessionStatus.Draft),
            CancellationToken.None));
    }

    private static AssignReviewSessionCommand Command(IReadOnlyCollection<int>? reviewers = null) =>
        new("RV", 10, ReviewType.Review1, reviewers ?? [1, 2], [], 1, "A",
            new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc));

    private sealed class FakeStore : IReviewSessionStore
    {
        public int GroupsWithAvailability { get; init; } = 1;
        public ReviewSessionEditState? EditSession { get; init; }
        public bool Created { get; private set; }

        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);
        public Task<ReviewGroupState?> FindGroupAsync(int groupId, CancellationToken cancellationToken) =>
            Task.FromResult<ReviewGroupState?>(new(groupId, 3, 9));
        public Task<ReviewSessionEditState?> FindSessionForEditAsync(int sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(EditSession);
        public Task<ReviewRoundEditState?> AcquireRoundAsync(
            int semesterId, ReviewType reviewType, DateOnly weekStart, CancellationToken cancellationToken) =>
            Task.FromResult<ReviewRoundEditState?>(new(4, ReviewRoundStatus.Closed));
        public Task<int> CountExistingLecturersAsync(
            IReadOnlyCollection<int> reviewerIds, CancellationToken cancellationToken) =>
            Task.FromResult(reviewerIds.Count);
        public Task<int> CountAvailableReviewersAsync(
            int roundId, int dayOfWeek, int slot, IReadOnlyCollection<int> reviewerIds,
            CancellationToken cancellationToken) => Task.FromResult(reviewerIds.Count);
        public Task<int> CountGroupsWithSelectedAvailabilityAsync(
            int roundId, IReadOnlyCollection<int> groupIds, int dayOfWeek, int slot,
            int requiredSlotCount, CancellationToken cancellationToken) => Task.FromResult(GroupsWithAvailability);
        public Task<bool> IsGroupScheduledAsync(
            int groupId, ReviewType reviewType, DateTime weekStart, DateTime weekEnd,
            CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> HasReviewerConflictAsync(
            int? excludedSessionId, DateTime sessionDate, int slot, IReadOnlyCollection<int> reviewerIds,
            CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<ReviewSessionResult> CreateSessionAsync(
            PersistReviewSession input, CancellationToken cancellationToken)
        {
            Created = true;
            return Task.FromResult(new ReviewSessionResult(
                1, input.Code, input.SemesterId, [], input.Type, ReviewSessionStatus.Draft,
                input.ReviewerIds, input.SessionDate, input.Slot, input.Room));
        }
        public Task<ReviewSessionResult> UpdateSessionAsync(
            int sessionId, PersistReviewSessionUpdate update, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<int?> FindLecturerIdByUserIdAsync(int userId, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(null);
        public Task<IReadOnlyList<MyReviewSessionResult>> GetLecturerSessionsAsync(
            int lecturerId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MyReviewSessionResult>>([]);
    }
}

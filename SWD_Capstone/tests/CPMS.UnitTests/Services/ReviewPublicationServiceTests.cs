using CPMS.BLL.Reviews.Publication;
using CPMS.Core.Enums;

namespace CPMS.UnitTests.Services;

public sealed class ReviewPublicationServiceTests
{
    [Fact]
    public async Task Publish_RejectsIncompleteScheduleWithoutPersisting()
    {
        var store = new FakeStore { Snapshot = Snapshot(active: 3, registered: 3, scheduled: 2) };
        var service = new ReviewPublicationService(store);

        var exception = await Assert.ThrowsAsync<IncompleteReviewScheduleException>(() =>
            service.PublishAsync(Command(), CancellationToken.None));

        Assert.Equal(3, exception.ActiveGroups);
        Assert.Equal(2, exception.ScheduledGroups);
        Assert.False(store.Persisted);
    }

    [Fact]
    public async Task Publish_PersistsThenDispatchesNotifications()
    {
        var store = new FakeStore { Snapshot = Snapshot(active: 1, registered: 1, scheduled: 1) };
        var service = new ReviewPublicationService(store);

        var result = await service.PublishAsync(Command(), CancellationToken.None);

        Assert.True(store.Persisted);
        Assert.Single(store.PersistedPublication!.Emails);
        Assert.Equal(8, result.PublicationId);
        Assert.Equal(1, result.SentEmailCount);
    }

    private static PublishReviewScheduleCommand Command() =>
        new(10, false, 3, ReviewType.Review1, new DateOnly(2026, 7, 16), null, null, null, null);

    private static ReviewPublicationSnapshot Snapshot(int active, int registered, int scheduled)
    {
        var session = new PublicationSession(
            2, "RV", ReviewType.Review1, new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc),
            1, "A", [5]);
        return new ReviewPublicationSnapshot(
            1,
            ReviewRoundStatus.Closed,
            false,
            [session],
            active,
            registered,
            scheduled,
            [new PublicationRecipient(20, "reviewer@example.com", "Reviewer", null, null, [2])],
            []);
    }

    private sealed class FakeStore : IReviewPublicationStore
    {
        public required ReviewPublicationSnapshot Snapshot { get; init; }
        public bool Persisted { get; private set; }
        public PersistReviewPublication? PersistedPublication { get; private set; }

        public Task<int?> FindModeratorIdAsync(int userId, bool allowFallback, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(4);

        public Task<TResult> ExecuteLockedAsync<TResult>(
            ReviewPublicationScope scope,
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken) => operation(cancellationToken);

        public Task<ReviewPublicationSnapshot> LoadSnapshotAsync(
            ReviewPublicationScope scope, int requiredSlotCount, CancellationToken cancellationToken) =>
            Task.FromResult(Snapshot);

        public Task<int> PersistAsync(
            ReviewPublicationScope scope,
            PersistReviewPublication publication,
            CancellationToken cancellationToken)
        {
            Persisted = true;
            PersistedPublication = publication;
            return Task.FromResult(8);
        }

        public Task<(int Sent, int Failed)> DispatchEmailsAsync(
            int publicationId, CancellationToken cancellationToken) => Task.FromResult((1, 0));
    }
}

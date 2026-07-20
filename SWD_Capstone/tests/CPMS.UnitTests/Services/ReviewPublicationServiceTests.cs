using CPMS.BLL.Reviews.Publication;
using CPMS.BLL.Enums;

namespace CPMS.UnitTests.Services;

public sealed class ReviewPublicationServiceTests
{
    [Fact]
    public async Task Publish_RejectsIncompleteScheduleWithoutPersisting()
    {
        var store = new FakeStore { PublicationSnapshot = Snapshot(active: 3, registered: 3, scheduled: 2) };
        var service = new ReviewPublicationService(store, new FakeDispatchQueue());

        IncompleteReviewScheduleException exception = await Assert.ThrowsAsync<IncompleteReviewScheduleException>(() =>
            service.PublishAsync(Command(), CancellationToken.None));

        Assert.Equal(3, exception.ActiveGroups);
        Assert.Equal(2, exception.ScheduledGroups);
        Assert.False(store.Persisted);
    }

    [Fact]
    public async Task Publish_PersistsThenQueuesNotificationsWithoutWaitingForSmtp()
    {
        var store = new FakeStore { PublicationSnapshot = Snapshot(active: 1, registered: 1, scheduled: 1) };
        var queue = new FakeDispatchQueue();
        var service = new ReviewPublicationService(store, queue);

        ReviewSchedulePublicationResult result = await service.PublishAsync(Command(), CancellationToken.None);

        Assert.True(store.Persisted);
        Assert.Equal(3, store.PersistedPublication!.Emails.Count);
        Assert.Contains(store.PersistedPublication.Emails,
            email => email.RecipientEmail == "reviewer@example.com" &&
                     email.Body.Contains("Slot 1 (07:30-09:00)", StringComparison.Ordinal));
        Assert.Contains(store.PersistedPublication.Emails,
            email => email.RecipientEmail == "leader@example.com" &&
                     email.Body.Contains("Group G01", StringComparison.Ordinal));
        Assert.Contains(store.PersistedPublication.Emails,
            email => email.RecipientEmail == "member@example.com" &&
                     email.Body.Contains("Group G01", StringComparison.Ordinal));
        Assert.Equal(8, result.PublicationId);
        Assert.Equal(3, result.QueuedEmailCount);
        Assert.Equal(0, result.SentEmailCount);
        Assert.Equal(0, result.FailedEmailCount);
        Assert.Equal(8, queue.PublicationId);
        Assert.False(store.DispatchCalled);
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
            [
                new PublicationRecipient(30, "leader@example.com", "Leader", "G01", "Project", [2]),
                new PublicationRecipient(31, "member@example.com", "Member", "G01", "Project", [2])
            ]);
    }

    private sealed class FakeStore : IReviewPublicationStore
    {
        public required ReviewPublicationSnapshot PublicationSnapshot { get; init; }
        public bool Persisted { get; private set; }
        public bool DispatchCalled { get; private set; }
        public PersistReviewPublication? PersistedPublication { get; private set; }

        public Task<int?> FindModeratorIdAsync(int userId, bool allowFallback, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(4);

        public Task<TResult> ExecuteLockedAsync<TResult>(
            ReviewPublicationScope scope,
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken) => operation(cancellationToken);

        public Task<ReviewPublicationSnapshot> LoadSnapshotAsync(
            ReviewPublicationScope scope, int requiredSlotCount, CancellationToken cancellationToken) =>
            Task.FromResult(PublicationSnapshot);

        public Task<int> PersistAsync(
            ReviewPublicationScope scope,
            PersistReviewPublication input,
            CancellationToken cancellationToken)
        {
            Persisted = true;
            PersistedPublication = input;
            return Task.FromResult(8);
        }

        public Task<(int Sent, int Failed)> DispatchEmailsAsync(
            int publicationId, CancellationToken cancellationToken)
        {
            DispatchCalled = true;
            return Task.FromResult((PersistedPublication?.Emails.Count ?? 0, 0));
        }

        public Task<IReadOnlyList<int>> GetPendingPublicationIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<int>>([]);
    }

    private sealed class FakeDispatchQueue : IReviewPublicationDispatchScheduler
    {
        public int? PublicationId { get; private set; }

        public ValueTask EnqueueAsync(int publicationId, CancellationToken cancellationToken)
        {
            PublicationId = publicationId;
            return ValueTask.CompletedTask;
        }
    }
}

using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Reviews.Scheduling;
using CPMS.BLL.Enums;
using CPMS.BLL.Services;

namespace CPMS.UnitTests.Services;

public sealed class ReviewRoundServiceTests
{
    [Fact]
    public async Task CreateRound_NormalizesWeekToMonday()
    {
        var store = new FakeStore
        {
            SemesterExists = true,
            ModeratorId = 12
        };
        var service = new ReviewRoundService(store, new AssignmentRules());

        var result = await service.CreateRoundAsync(
            7, false, 3, ReviewType.Review1, new DateOnly(2026, 7, 16), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 13), result.WeekStartDate);
        Assert.Equal(new DateOnly(2026, 7, 18), result.WeekEndDate);
        Assert.Equal(12, store.CreatedByModeratorId);
    }

    [Fact]
    public async Task UpdateStatus_RejectsInvalidTransitionWithoutWriting()
    {
        var store = new FakeStore { Round = Round(status: ReviewRoundStatus.Draft) };
        var service = new ReviewRoundService(store, new AssignmentRules());

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateStatusAsync(1, ReviewRoundStatus.Closed, CancellationToken.None));

        Assert.False(store.StatusUpdated);
    }

    [Fact]
    public async Task CloseRound_RejectsIncompleteGroupRegistrationsWithoutWriting()
    {
        var store = new FakeStore
        {
            Round = Round(status: ReviewRoundStatus.Open),
            RegistrationCompletion = (4, 3)
        };
        var service = new ReviewRoundService(store, new AssignmentRules());

        var exception = await Assert.ThrowsAsync<ReviewRoundClosureException>(() =>
            service.UpdateStatusAsync(1, ReviewRoundStatus.Closed, CancellationToken.None));

        Assert.Equal(4, exception.ActiveGroupCount);
        Assert.Equal(3, exception.CompletedRegistrationCount);
        Assert.False(store.StatusUpdated);
    }

    [Fact]
    public async Task CloseRound_SetsClosedTimestampWhenAllGroupsRegistered()
    {
        var store = new FakeStore
        {
            Round = Round(status: ReviewRoundStatus.Open),
            RegistrationCompletion = (4, 4)
        };
        var service = new ReviewRoundService(store, new AssignmentRules());

        var result = await service.UpdateStatusAsync(1, ReviewRoundStatus.Closed, CancellationToken.None);

        Assert.True(store.StatusUpdated);
        Assert.Equal(ReviewRoundStatus.Closed, result.Status);
        Assert.NotNull(result.ClosedAt);
    }

    private static ReviewRoundState Round(int id = 1, ReviewRoundStatus status = ReviewRoundStatus.Draft) =>
        new(id, 3, ReviewType.Review1, status, new DateOnly(2026, 7, 13), new DateOnly(2026, 7, 18),
            DateTime.UtcNow.AddDays(-1), null, null, 0);

    private sealed class FakeStore : IReviewRoundStore
    {
        public bool SemesterExists { get; init; }
        public int? ModeratorId { get; init; }
        public bool ExistingRound { get; init; }
        public ReviewRoundState? Round { get; init; }
        public (int ActiveGroups, int CompletedRegistrations) RegistrationCompletion { get; init; }
        public int? CreatedByModeratorId { get; private set; }
        public bool StatusUpdated { get; private set; }

        public Task<IReadOnlyList<ReviewRoundState>> GetRoundsAsync(int semesterId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReviewRoundState>>(Round is null ? [] : [Round]);

        public Task<bool> SemesterExistsAsync(int semesterId, CancellationToken cancellationToken) =>
            Task.FromResult(SemesterExists);

        public Task<int?> FindModeratorIdAsync(int userId, bool allowFallback, CancellationToken cancellationToken) =>
            Task.FromResult(ModeratorId);

        public Task<bool> RoundExistsAsync(int semesterId, ReviewType reviewType, DateOnly weekStartDate, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingRound);

        public Task<ReviewRoundState> CreateRoundAsync(
            int semesterId,
            ReviewType reviewType,
            DateOnly weekStartDate,
            DateOnly weekEndDate,
            int moderatorId,
            CancellationToken cancellationToken)
        {
            CreatedByModeratorId = moderatorId;
            return Task.FromResult(new ReviewRoundState(
                1, semesterId, reviewType, ReviewRoundStatus.Draft, weekStartDate, weekEndDate,
                DateTime.UtcNow, null, null, 0));
        }

        public Task<ReviewRoundState?> FindRoundAsync(int roundId, CancellationToken cancellationToken) =>
            Task.FromResult(Round);

        public Task<(int ActiveGroups, int CompletedRegistrations)> GetRegistrationCompletionAsync(
            int roundId, int semesterId, int requiredSlotCount, CancellationToken cancellationToken) =>
            Task.FromResult(RegistrationCompletion);

        public Task<ReviewRoundState> UpdateRoundStatusAsync(
            int roundId,
            ReviewRoundStatus status,
            DateTime? openedAt,
            DateTime? closedAt,
            CancellationToken cancellationToken)
        {
            StatusUpdated = true;
            var current = Round ?? throw new InvalidOperationException("Round is required for this test.");
            return Task.FromResult(current with { Status = status, OpenedAt = openedAt, ClosedAt = closedAt });
        }
    }
}

using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Reviews.Scheduling;
using CPMS.BLL.Enums;
using CPMS.BLL.Services;

namespace CPMS.UnitTests.Services;

public sealed class ReviewRoundServiceTests
{
    [Fact]
    public async Task CreateRound_StartsReviewFiveDaysAfterRegistrationEnds()
    {
        var store = new FakeStore
        {
            SemesterExists = true,
            ModeratorId = 12
        };
        var service = new ReviewRoundService(store);

        ReviewRoundManagementResult result = await service.CreateRoundAsync(
            7, false, 3, ReviewType.Review1,
            new DateOnly(2026, 7, 8), new DateOnly(2026, 7, 13), CancellationToken.None);

        Assert.Equal(new DateOnly(2026, 7, 8), result.RegistrationEndDate);
        Assert.Equal(new DateOnly(2026, 7, 13), result.WeekStartDate);
        Assert.Equal(new DateOnly(2026, 7, 18), result.WeekEndDate);
        Assert.Equal(12, store.CreatedByModeratorId);
    }

    [Fact]
    public async Task CreateRound_RejectsDuplicateTypeWithinSemesterEvenForAnotherWeek()
    {
        var store = new FakeStore
        {
            SemesterExists = true,
            ModeratorId = 12,
            RoundExists = true
        };
        var service = new ReviewRoundService(store);

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateRoundAsync(
                7, false, 3, ReviewType.Review1,
                new DateOnly(2026, 7, 29), new DateOnly(2026, 8, 3), CancellationToken.None));

        Assert.Contains("already has a review round", exception.Message);
        Assert.Null(store.CreatedByModeratorId);
    }

    [Fact]
    public async Task CreateRound_RejectsReviewDateThatIsNotFiveDaysAfterRegistrationEnds()
    {
        var store = new FakeStore { SemesterExists = true, ModeratorId = 12 };
        var service = new ReviewRoundService(store);

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateRoundAsync(
                7, false, 3, ReviewType.Review1,
                new DateOnly(2026, 7, 8), new DateOnly(2026, 7, 14), CancellationToken.None));

        Assert.Contains("exactly five days", exception.Message);
        Assert.Null(store.CreatedByModeratorId);
    }

    [Fact]
    public async Task CreateRound_RejectsRegistrationDeadlineThatCannotStartMondayReviewWeek()
    {
        var store = new FakeStore { SemesterExists = true, ModeratorId = 12 };
        var service = new ReviewRoundService(store);

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateRoundAsync(
                7, false, 3, ReviewType.Review1,
                new DateOnly(2026, 7, 9), null, CancellationToken.None));

        Assert.Contains("Wednesday", exception.Message);
        Assert.Null(store.CreatedByModeratorId);
    }

    [Fact]
    public async Task CreateRound_RejectsReviewWeeksLessThanFourWeeksApart()
    {
        var store = new FakeStore
        {
            SemesterExists = true,
            ModeratorId = 12,
            CurrentRound = Round(
                status: ReviewRoundStatus.Published,
                type: ReviewType.Review1,
                weekStartDate: new DateOnly(2026, 7, 6))
        };
        var service = new ReviewRoundService(store);

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateRoundAsync(
                7, false, 3, ReviewType.Review2,
                new DateOnly(2026, 7, 15), new DateOnly(2026, 7, 20), CancellationToken.None));

        Assert.Contains("at least four weeks apart", exception.Message);
        Assert.Null(store.CreatedByModeratorId);
    }

    [Fact]
    public async Task CreateRound_RejectsRegistrationOrReviewOutsideSemester()
    {
        var store = new FakeStore
        {
            SemesterExists = true,
            ModeratorId = 12,
            SemesterPeriod = new SemesterReviewPeriod(
                new DateOnly(2026, 5, 1),
                new DateOnly(2026, 8, 31))
        };
        var service = new ReviewRoundService(store);

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateRoundAsync(
                7, false, 3, ReviewType.Review1,
                new DateOnly(2026, 4, 29), new DateOnly(2026, 5, 4), CancellationToken.None));

        Assert.Contains("fully within the semester", exception.Message);
        Assert.Null(store.CreatedByModeratorId);
    }

    [Fact]
    public async Task UpdateStatus_RejectsInvalidTransitionWithoutWriting()
    {
        var store = new FakeStore { CurrentRound = Round(status: ReviewRoundStatus.Draft) };
        var service = new ReviewRoundService(store);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateStatusAsync(1, ReviewRoundStatus.Published, CancellationToken.None));

        Assert.False(store.StatusUpdated);
    }

    [Fact]
    public async Task CloseRound_RejectsIncompleteGroupRegistrationsWithoutWriting()
    {
        var store = new FakeStore
        {
            CurrentRound = Round(status: ReviewRoundStatus.Open),
            RegistrationCompletion = (4, 3)
        };
        var service = new ReviewRoundService(store);

        ReviewRoundClosureException exception = await Assert.ThrowsAsync<ReviewRoundClosureException>(() =>
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
            CurrentRound = Round(status: ReviewRoundStatus.Open),
            RegistrationCompletion = (4, 4)
        };
        var service = new ReviewRoundService(store);

        ReviewRoundManagementResult result = await service.UpdateStatusAsync(1, ReviewRoundStatus.Closed, CancellationToken.None);

        Assert.True(store.StatusUpdated);
        Assert.Equal(ReviewRoundStatus.Closed, result.Status);
        Assert.NotNull(result.ClosedAt);
    }

    private static ReviewRoundState Round(
        int id = 1,
        ReviewRoundStatus status = ReviewRoundStatus.Draft,
        ReviewType type = ReviewType.Review1,
        DateOnly? weekStartDate = null)
    {
        DateOnly start = weekStartDate ?? new DateOnly(2026, 7, 13);
        return new(id, 3, type, status, start, start.AddDays(5),
            DateTime.UtcNow.AddDays(-1), null, null, 0);
    }

    private sealed class FakeStore : IReviewRoundStore
    {
        public bool SemesterExists { get; init; }
        public SemesterReviewPeriod? SemesterPeriod { get; init; }
        public bool RoundExists { get; init; }
        public int? ModeratorId { get; init; }
        public ReviewRoundState? CurrentRound { get; init; }
        public (int ActiveGroups, int CompletedRegistrations) RegistrationCompletion { get; init; }
        public int? CreatedByModeratorId { get; private set; }
        public bool StatusUpdated { get; private set; }

        public Task<IReadOnlyList<ReviewRoundState>> GetRoundsAsync(int semesterId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReviewRoundState>>(CurrentRound is null ? [] : [CurrentRound]);

        public Task<SemesterReviewPeriod?> FindSemesterPeriodAsync(
            int semesterId,
            CancellationToken cancellationToken) =>
            Task.FromResult(SemesterExists
                ? SemesterPeriod ?? new SemesterReviewPeriod(
                    new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 12, 31))
                : null);

        public Task<int?> FindModeratorIdAsync(int userId, bool allowFallback, CancellationToken cancellationToken) =>
            Task.FromResult(ModeratorId);

        public Task<bool> RoundExistsAsync(int semesterId, ReviewType reviewType, CancellationToken cancellationToken) =>
            Task.FromResult(RoundExists);

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
            Task.FromResult(CurrentRound);

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
            ReviewRoundState current = CurrentRound ?? throw new InvalidOperationException("Round is required for this test.");
            return Task.FromResult(current with { Status = status, OpenedAt = openedAt, ClosedAt = closedAt });
        }
    }
}

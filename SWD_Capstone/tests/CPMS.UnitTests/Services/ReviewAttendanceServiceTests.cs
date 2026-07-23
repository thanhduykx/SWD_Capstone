using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Reviews.Assessment;

namespace CPMS.UnitTests.Services;

public sealed class ReviewAttendanceServiceTests
{
    [Fact]
    public async Task GetAttendance_RejectsAssignedReviewerBeforeSlotStarts()
    {
        var store = new FakeStore(FutureContext());
        var service = new ReviewAttendanceService(store);

        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.GetAttendanceAsync(
                10, 20, new ReviewActor(7, UserRole.Lecturer), CancellationToken.None));

        Assert.Contains("opens when slot 3 starts", exception.Message);
        Assert.False(store.StudentsLoaded);
    }

    [Fact]
    public async Task SubmitAttendance_RejectsWriteBeforeSlotStarts()
    {
        var store = new FakeStore(FutureContext());
        var service = new ReviewAttendanceService(store);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.SubmitAttendanceAsync(
                10, 20, [new AttendanceEntry(30, true, null)],
                new ReviewActor(7, UserRole.Lecturer), CancellationToken.None));

        Assert.False(store.AttendanceSaved);
    }

    private static ReviewAttendanceContext FutureContext() =>
        new(10, "FUTURE", ReviewSessionStatus.Published, [3],
            DateTime.UtcNow.AddDays(1), 3, "B206", 20, "TEAM-20",
            ReviewGroupStatus.Scheduled, null, 3, null);

    private sealed class FakeStore(ReviewAttendanceContext context) : IReviewAttendanceStore
    {
        public bool StudentsLoaded { get; private set; }
        public bool AttendanceSaved { get; private set; }

        public Task<ReviewAttendanceContextLookup> LoadContextAsync(
            int sessionId, int? groupId, int userId, CancellationToken cancellationToken) =>
            Task.FromResult(new ReviewAttendanceContextLookup(true, context));

        public Task<IReadOnlyList<AttendanceStudent>> GetStudentsAsync(
            int sessionId, int groupId, CancellationToken cancellationToken)
        {
            StudentsLoaded = true;
            return Task.FromResult<IReadOnlyList<AttendanceStudent>>([]);
        }

        public Task<IReadOnlySet<int>> GetStudentIdsAsync(int groupId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<int>>(new HashSet<int> { 30 });

        public Task SaveAttendanceAsync(
            int sessionId, int lecturerId, IReadOnlyCollection<AttendanceEntry> entries,
            DateTime markedAt, CancellationToken cancellationToken)
        {
            AttendanceSaved = true;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ReviewCommentResult>> GetCommentsAsync(
            int sessionId, int groupId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReviewCommentResult>>([]);

        public Task<ReviewCommentResult> AddCommentAsync(
            int sessionId, int groupId, int authorUserId, string content,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task FinalizeUnsignedCheckInsAsync(
            int sessionId, int groupId, int lecturerId, DateTime finalizedAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(int StudentCount, int AttendanceCount, int SubmittedCount)> GetCompletionCountsAsync(
            int sessionId, int groupId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CompleteReviewResult> CompleteAsync(
            int sessionId, int groupId, int lecturerId, DateTime completedAt,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

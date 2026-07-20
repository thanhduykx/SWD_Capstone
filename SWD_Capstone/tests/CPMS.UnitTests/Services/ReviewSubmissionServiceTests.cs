using CPMS.BLL.Enums;
using CPMS.BLL.Reviews.Assessment;

namespace CPMS.UnitTests.Services;

public sealed class ReviewSubmissionServiceTests
{
    [Fact]
    public async Task Submit_RequiresReviewerCommentButNotNumericScoreOrVerdict()
    {
        var store = new FakeStore();
        var service = new ReviewSubmissionService(store, new TemplateProvider());

        ReviewSubmissionResult result = await service.SubmitAsync(
            10, new ReviewActor(7, UserRole.Lecturer), CancellationToken.None);

        Assert.True(store.Submitted);
        Assert.Equal(ReviewSubmissionStatus.Submitted, result.Status);
    }

    private sealed class TemplateProvider : IReviewChecklistTemplateProvider
    {
        public IReadOnlyList<ReviewChecklistTemplateItem> GetItems(ReviewType type) =>
        [new("architecture", "Architecture", null, null, 1, false, null)];
    }

    private sealed class FakeStore : IReviewSubmissionStore
    {
        public bool Submitted { get; private set; }

        public Task<int?> FindLecturerIdAsync(int userId, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(3);
        public Task<int?> FindStudentGroupIdAsync(int userId, CancellationToken cancellationToken) =>
            Task.FromResult<int?>(null);
        public Task<IReadOnlyList<ReviewSubmissionSummary>> GetLecturerSummariesAsync(
            int lecturerId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReviewSubmissionSummary>>([]);
        public Task<IReadOnlyList<ReviewSubmissionSummary>> GetStudentSummariesAsync(
            int groupId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReviewSubmissionSummary>>([]);
        public Task<ReviewSubmissionState?> FindStateAsync(int submissionId, CancellationToken cancellationToken) =>
            Task.FromResult<ReviewSubmissionState?>(new(
                submissionId, 20, 30, 3, ReviewType.Review1, ReviewSubmissionStatus.Draft,
                ReviewSessionStatus.Published, false, "Kiến trúc cần tách lớp rõ hơn."));
        public Task<ReviewSubmissionData> GetDataAsync(int submissionId, CancellationToken cancellationToken) =>
            Task.FromResult(new ReviewSubmissionData(
                submissionId, 20, 30, "SE2601", "CPMS", ReviewType.Review1,
                Submitted ? ReviewSubmissionStatus.Submitted : ReviewSubmissionStatus.Draft,
                ReviewSessionStatus.Published, new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc), 1, "P101", 3,
                "GV001", "Nguyễn Văn An", null, null, null,
                "Kiến trúc cần tách lớp rõ hơn.", null, null, DateTime.UtcNow,
                Submitted ? DateTime.UtcNow : null,
                new Dictionary<string, (ReviewChecklistAnswer?, string?)>()));
        public Task SaveDraftAsync(
            int submissionId, PersistReviewDraft draft, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task SubmitAsync(int submissionId, DateTime submittedAt, CancellationToken cancellationToken)
        {
            Submitted = true;
            return Task.CompletedTask;
        }
        public Task<ReviewChecklistExportData> GetExportDataAsync(
            int sessionId, int groupId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ScheduledReviewExport>> GetScheduledExportsAsync(
            int semesterId, ReviewType reviewType, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

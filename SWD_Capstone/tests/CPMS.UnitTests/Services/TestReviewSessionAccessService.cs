using CPMS.BLL.Reviews.Access;

namespace CPMS.UnitTests.Services;

internal sealed class TestReviewSessionAccessService : IReviewSessionAccessService
{
    public Task<GeneratedReviewSessionAccessCode> GenerateAsync(
        int sessionId,
        int groupId,
        int generatedByUserId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<VerifiedReviewSessionAccess> VerifyAsync(
        int sessionId,
        int groupId,
        int lecturerUserId,
        string accessCode,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task EnsureLecturerAccessAsync(
        int sessionId,
        int groupId,
        int lecturerUserId,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<GeneratedReviewSessionAccessCode> GenerateForSessionAsync(
        int sessionId,
        int generatedByUserId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<VerifiedReviewSessionAccess> VerifyForSessionAsync(
        int sessionId,
        int lecturerUserId,
        string accessCode,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

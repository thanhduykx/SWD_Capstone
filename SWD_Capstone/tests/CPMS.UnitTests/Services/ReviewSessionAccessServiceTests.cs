using CPMS.BLL.Abstractions.Security;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Reviews.Access;

namespace CPMS.UnitTests.Services;

public sealed class ReviewSessionAccessServiceTests
{
    [Fact]
    public async Task Generate_CreatesEightCharacterCodeAndStoresOnlyHash()
    {
        var store = new FakeStore
        {
            Session = new ReviewSessionAccessState(58, null, 2),
            SlotSessions =
            [
                new ReviewSessionAccessState(56, null, 4),
                new ReviewSessionAccessState(58, null, 2)
            ]
        };
        var service = new ReviewSessionAccessService(store, new FakeHasher());

        GeneratedReviewSessionAccessCode result = await service.GenerateAsync(
            58,
            7,
            CancellationToken.None);

        Assert.Equal(8, result.AccessCode.Length);
        Assert.Equal(5, result.CodeVersion);
        Assert.Equal([56, 58], result.AffectedSessionIds);
        Assert.Equal([56, 58], store.StoredSessionIds);
        Assert.Equal($"hashed:{new string(result.AccessCode.Reverse().ToArray())}", store.StoredHash);
        Assert.DoesNotContain(result.AccessCode, store.StoredHash);
        Assert.Equal(7, store.GeneratedByUserId);
    }

    [Fact]
    public async Task Verify_GrantsCurrentVersionOnlyToAssignedLecturer()
    {
        var store = new FakeStore
        {
            Session = new ReviewSessionAccessState(58, "hashed:5432DCBA", 4),
            AssignedLecturerId = 12
        };
        var service = new ReviewSessionAccessService(store, new FakeHasher());

        VerifiedReviewSessionAccess result = await service.VerifyAsync(
            58,
            21,
            "abcd2345",
            CancellationToken.None);

        Assert.True(result.IsVerified);
        Assert.Equal((58, 12, 4), store.SavedGrant);
    }

    [Fact]
    public async Task Verify_RejectsWrongCodeWithoutSavingGrant()
    {
        var store = new FakeStore
        {
            Session = new ReviewSessionAccessState(58, "hashed:5432DCBA", 4),
            AssignedLecturerId = 12
        };
        var service = new ReviewSessionAccessService(store, new FakeHasher());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.VerifyAsync(58, 21, "WXYZ6789", CancellationToken.None));

        Assert.Null(store.SavedGrant);
    }

    [Fact]
    public async Task EnsureAccess_RejectsGrantFromPreviousCodeVersion()
    {
        var store = new FakeStore
        {
            Session = new ReviewSessionAccessState(58, "hashed:5432DCBA", 5),
            AssignedLecturerId = 12,
            HasCurrentGrant = false
        };
        var service = new ReviewSessionAccessService(store, new FakeHasher());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.EnsureLecturerAccessAsync(58, 21, CancellationToken.None));
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) =>
            $"hashed:{new string(password.Reverse().ToArray())}";

        public bool Verify(string passwordHash, string password) =>
            passwordHash == Hash(password);
    }

    private sealed class FakeStore : IReviewSessionAccessStore
    {
        public ReviewSessionAccessState? Session { get; init; }
        public IReadOnlyList<ReviewSessionAccessState>? SlotSessions { get; init; }
        public int? AssignedLecturerId { get; init; }
        public bool HasCurrentGrant { get; init; }
        public string? StoredHash { get; private set; }
        public IReadOnlyCollection<int>? StoredSessionIds { get; private set; }
        public int? GeneratedByUserId { get; private set; }
        public (int SessionId, int LecturerId, int Version)? SavedGrant { get; private set; }

        public Task<ReviewSessionAccessState?> FindSessionAsync(
            int sessionId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Session?.SessionId == sessionId ? Session : null);

        public Task<IReadOnlyList<ReviewSessionAccessState>> FindSlotSessionsAsync(
            int sessionId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ReviewSessionAccessState> sessions = SlotSessions
                ?? (Session is { } session && session.SessionId == sessionId ? [session] : []);
            return Task.FromResult(sessions);
        }

        public Task<int?> FindAssignedLecturerAsync(
            int sessionId,
            int lecturerUserId,
            CancellationToken cancellationToken) =>
            Task.FromResult(AssignedLecturerId);

        public Task SaveAccessCodeAsync(
            IReadOnlyCollection<int> sessionIds,
            string accessCodeHash,
            int codeVersion,
            int generatedByUserId,
            DateTime generatedAt,
            CancellationToken cancellationToken)
        {
            StoredSessionIds = sessionIds;
            StoredHash = accessCodeHash;
            GeneratedByUserId = generatedByUserId;
            return Task.CompletedTask;
        }

        public Task SaveGrantAsync(
            int sessionId,
            int lecturerId,
            int codeVersion,
            DateTime verifiedAt,
            CancellationToken cancellationToken)
        {
            SavedGrant = (sessionId, lecturerId, codeVersion);
            return Task.CompletedTask;
        }

        public Task<bool> HasCurrentGrantAsync(
            int sessionId,
            int lecturerId,
            int codeVersion,
            CancellationToken cancellationToken) =>
            Task.FromResult(HasCurrentGrant);
    }
}

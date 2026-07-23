using CPMS.BLL.Abstractions.Security;
using CPMS.BLL.Accounts;
using CPMS.BLL.Enums;

namespace CPMS.UnitTests.Services;

public sealed class AuthenticationServiceTests
{
    [Fact]
    public async Task Login_RejectsLockedAccountWithoutCheckingPassword()
    {
        var store = new AuthenticationStoreFake
        {
            CurrentUser = User(lockedUntil: DateTime.UtcNow.AddMinutes(1))
        };
        var hasher = new PasswordHasherFake { Matches = true };
        AuthenticationService service = CreateService(store, hasher);

        AuthenticationResult? result = await service.LoginAsync("lecturer", "secret", null, CancellationToken.None);

        Assert.Null(result);
        Assert.False(hasher.VerifyCalled);
        Assert.Null(store.SavedRefreshToken);
    }

    [Fact]
    public async Task Login_TenthFailedAttemptTemporarilyLocksAccount()
    {
        var store = new AuthenticationStoreFake { CurrentUser = User(failedAttempts: 9) };
        AuthenticationService service = CreateService(store, new PasswordHasherFake { Matches = false });

        AuthenticationResult? result = await service.LoginAsync("lecturer", "wrong", null, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(10, store.RecordedFailedAttempts);
        Assert.True(store.RecordedLockedUntil > DateTime.UtcNow.AddMinutes(4));
    }

    [Fact]
    public async Task Login_WithValidPasswordPersistsRefreshToken()
    {
        var store = new AuthenticationStoreFake { CurrentUser = User() };
        AuthenticationService service = CreateService(store, new PasswordHasherFake { Matches = true });

        AuthenticationResult? result = await service.LoginAsync(" lecturer ", "secret", "browser", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("access", result.AccessToken);
        Assert.Equal("refresh", result.RefreshToken);
        Assert.Equal(7, store.SuccessfulLoginUserId);
        Assert.Equal("refresh-hash", store.SavedRefreshToken?.TokenHash);
        Assert.Equal("browser", store.SavedRefreshToken?.DeviceInfo);
    }

    [Fact]
    public async Task GoogleLogin_WithMatchingActiveAccountIssuesApplicationTokens()
    {
        var store = new AuthenticationStoreFake { CurrentUser = User() };
        var verifier = new GoogleVerifierFake { Email = "lecturer@fpt.edu.vn" };
        AuthenticationService service = CreateService(store, new PasswordHasherFake(), verifier);

        AuthenticationResult? result = await service.GoogleLoginAsync(
            "valid-google-id-token", "chrome", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("access", result.AccessToken);
        Assert.Equal(7, store.SuccessfulLoginUserId);
        Assert.Equal("refresh-hash", store.SavedRefreshToken?.TokenHash);
    }

    [Fact]
    public async Task Refresh_RejectsLockedAccountWithoutRotatingToken()
    {
        var store = new AuthenticationStoreFake
        {
            CurrentUser = User(lockedUntil: DateTime.UtcNow.AddMinutes(1)),
            RefreshToken = new RefreshTokenState(3, 7, false, DateTime.UtcNow.AddHours(1))
        };
        AuthenticationService service = CreateService(store, new PasswordHasherFake());

        AuthenticationResult? result = await service.RefreshAsync("refresh", null, CancellationToken.None);

        Assert.Null(result);
        Assert.Null(store.RotatedToken);
    }

    private static AuthenticationService CreateService(
        AuthenticationStoreFake store, PasswordHasherFake hasher, GoogleVerifierFake? verifier = null) =>
        new(store, hasher, new TokenIssuerFake(), verifier ?? new GoogleVerifierFake());

    private static AuthenticationUser User(int failedAttempts = 0, DateTime? lockedUntil = null) =>
        new(7, "lecturer", "lecturer@fpt.edu.vn", "hash", UserRole.Lecturer,
            true, failedAttempts, lockedUntil);

    private sealed class PasswordHasherFake : IPasswordHasher
    {
        public bool Matches { get; init; }
        public bool VerifyCalled { get; private set; }
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string passwordHash, string password)
        {
            VerifyCalled = true;
            return Matches;
        }
    }

    private sealed class TokenIssuerFake : IAuthenticationTokenIssuer
    {
        public IssuedToken Issue(AuthIdentity identity, string? deviceInfo) =>
            new("access", "refresh", "refresh-hash", DateTime.UtcNow.AddDays(7));
        public string HashRefreshToken(string rawToken) => $"hash:{rawToken}";
    }

    private sealed class GoogleVerifierFake : IGoogleIdentityVerifier
    {
        public string? Email { get; init; }
        public Task<string?> VerifyEmailAsync(string idToken, CancellationToken cancellationToken) =>
            Task.FromResult(Email);
    }

    private sealed class AuthenticationStoreFake : IAuthenticationStore
    {
        public AuthenticationUser? CurrentUser { get; init; }
        public RefreshTokenState? RefreshToken { get; init; }
        public int? RecordedFailedAttempts { get; private set; }
        public DateTime? RecordedLockedUntil { get; private set; }
        public int? SuccessfulLoginUserId { get; private set; }
        public PersistRefreshToken? SavedRefreshToken { get; private set; }
        public PersistRefreshToken? RotatedToken { get; private set; }

        public Task<AuthenticationUser?> FindByLoginAsync(string login, CancellationToken cancellationToken) =>
            Task.FromResult(CurrentUser);
        public Task<AuthenticationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(CurrentUser);
        public Task RecordFailedLoginAsync(
            int userId, int failedAttempts, DateTime? lockedUntil, CancellationToken cancellationToken)
        {
            RecordedFailedAttempts = failedAttempts;
            RecordedLockedUntil = lockedUntil;
            return Task.CompletedTask;
        }
        public Task CompleteSuccessfulLoginAsync(
            int userId,
            DateTime loginAt,
            PersistRefreshToken token,
            CancellationToken cancellationToken)
        {
            SuccessfulLoginUserId = userId;
            SavedRefreshToken = token;
            return Task.CompletedTask;
        }
        public Task<RefreshTokenState?> FindRefreshTokenAsync(
            string tokenHash, CancellationToken cancellationToken) => Task.FromResult(RefreshToken);
        public Task<AuthenticationUser?> FindUserAsync(int userId, CancellationToken cancellationToken) =>
            Task.FromResult(CurrentUser);
        public Task RotateRefreshTokenAsync(
            int existingTokenId, PersistRefreshToken replacement, CancellationToken cancellationToken)
        {
            RotatedToken = replacement;
            return Task.CompletedTask;
        }
    }
}

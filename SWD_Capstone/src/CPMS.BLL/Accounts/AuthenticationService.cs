using CPMS.BLL.Abstractions.Security;
using CPMS.BLL.Common.Exceptions;
using CPMS.Core.Exceptions;

namespace CPMS.BLL.Accounts;

public sealed class AuthenticationService(
    IAuthenticationStore store,
    IPasswordHasher passwordHasher,
    IAuthenticationTokenIssuer tokenIssuer,
    IGoogleIdentityVerifier googleVerifier) : IAuthenticationService
{
    public async Task<AuthenticationResult?> LoginAsync(
        string? username, string? password, string? deviceInfo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new BusinessRuleException("Username is required.");
        if (string.IsNullOrEmpty(password)) return null;
        var user = await store.FindByLoginAsync(username.Trim(), cancellationToken);
        if (user is null || !user.IsActive || user.LockedUntil > DateTime.UtcNow) return null;
        var matches = false;
        try { matches = passwordHasher.Verify(user.PasswordHash, password); }
        catch (FormatException) { matches = false; }
        if (!matches)
        {
            var attempts = user.FailedLoginAttempts + 1;
            await store.RecordFailedLoginAsync(
                user.Id, attempts, attempts >= 10 ? DateTime.UtcNow.AddMinutes(5) : null, cancellationToken);
            return null;
        }
        await store.RecordSuccessfulLoginAsync(user.Id, DateTime.UtcNow, cancellationToken);
        return await IssueAsync(user, deviceInfo, cancellationToken);
    }

    public async Task<AuthenticationResult?> GoogleLoginAsync(
        string idToken, string? deviceInfo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idToken)) return null;
        var email = await googleVerifier.VerifyEmailAsync(idToken, cancellationToken);
        if (string.IsNullOrWhiteSpace(email)) return null;
        var user = await store.FindByEmailAsync(email.Trim(), cancellationToken);
        if (user is null || !user.IsActive || user.LockedUntil > DateTime.UtcNow) return null;
        await store.RecordSuccessfulLoginAsync(user.Id, DateTime.UtcNow, cancellationToken);
        return await IssueAsync(user, deviceInfo, cancellationToken);
    }

    public async Task<AuthenticationResult?> RefreshAsync(
        string refreshToken, string? deviceInfo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;
        var state = await store.FindRefreshTokenAsync(tokenIssuer.HashRefreshToken(refreshToken), cancellationToken);
        if (state is null || state.IsRevoked || state.ExpiresAt <= DateTime.UtcNow) return null;
        var user = await store.FindUserAsync(state.UserId, cancellationToken);
        if (user is null || !user.IsActive || user.LockedUntil > DateTime.UtcNow) return null;
        var issued = tokenIssuer.Issue(new AuthIdentity(user.Id, user.Username, user.Role), deviceInfo);
        await store.RotateRefreshTokenAsync(state.Id, ToPersistence(user.Id, issued, deviceInfo), cancellationToken);
        return Map(issued);
    }

    public async Task<BootstrapAdminResult> BootstrapAdminAsync(
        string username,
        string email,
        string password,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        if (await store.AnyUsersAsync(cancellationToken))
            throw new ConflictException("Bootstrap is only allowed before the first account exists.");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new BusinessRuleException("Username, email and password are required.");
        return await store.CreateBootstrapAdminAsync(
            username.Trim(), email.Trim(), passwordHasher.Hash(password), ipAddress, userAgent, cancellationToken);
    }

    private async Task<AuthenticationResult> IssueAsync(
        AuthenticationUser user, string? deviceInfo, CancellationToken cancellationToken)
    {
        var issued = tokenIssuer.Issue(new AuthIdentity(user.Id, user.Username, user.Role), deviceInfo);
        await store.SaveRefreshTokenAsync(ToPersistence(user.Id, issued, deviceInfo), cancellationToken);
        return Map(issued);
    }

    private static PersistRefreshToken ToPersistence(int userId, IssuedToken token, string? deviceInfo) =>
        new(userId, token.RefreshTokenHash, token.ExpiresAt, deviceInfo);
    private static AuthenticationResult Map(IssuedToken token) =>
        new(token.AccessToken, token.RawRefreshToken, token.ExpiresAt);
}

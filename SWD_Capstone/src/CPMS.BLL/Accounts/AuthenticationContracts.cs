using CPMS.Core.Enums;

namespace CPMS.BLL.Accounts;

public sealed record AuthIdentity(int Id, string Username, UserRole Role);
public sealed record IssuedToken(string AccessToken, string RawRefreshToken, string RefreshTokenHash, DateTime ExpiresAt);
public sealed record AuthenticationResult(string AccessToken, string RefreshToken, DateTime RefreshTokenExpiresAt);
public sealed record BootstrapAdminResult(int Id, string Username, UserRole Role);
public interface IAuthenticationTokenIssuer
{
    IssuedToken Issue(AuthIdentity identity, string? deviceInfo);
    string HashRefreshToken(string rawToken);
}
public interface IGoogleIdentityVerifier
{
    Task<string?> VerifyEmailAsync(string idToken, CancellationToken cancellationToken);
}
public interface IAuthenticationService
{
    Task<AuthenticationResult?> LoginAsync(
        string? username, string? password, string? deviceInfo, CancellationToken cancellationToken);
    Task<AuthenticationResult?> GoogleLoginAsync(
        string idToken, string? deviceInfo, CancellationToken cancellationToken);
    Task<AuthenticationResult?> RefreshAsync(
        string refreshToken, string? deviceInfo, CancellationToken cancellationToken);
    Task<BootstrapAdminResult> BootstrapAdminAsync(
        string username,
        string email,
        string password,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);
}

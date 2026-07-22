using CPMS.BLL.Enums;

namespace CPMS.BLL.Accounts;

public sealed record AuthenticationUser(
    int Id,
    string Username,
    string Email,
    string PasswordHash,
    UserRole Role,
    bool IsActive,
    int FailedLoginAttempts,
    DateTime? LockedUntil);
public sealed record RefreshTokenState(int Id, int UserId, bool IsRevoked, DateTime ExpiresAt);
public sealed record PersistRefreshToken(
    int UserId, string TokenHash, DateTime ExpiresAt, string? DeviceInfo);
public interface IAuthenticationStore
{
    Task<AuthenticationUser?> FindByLoginAsync(string login, CancellationToken cancellationToken);
    Task<AuthenticationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task RecordFailedLoginAsync(int userId, int failedAttempts, DateTime? lockedUntil, CancellationToken cancellationToken);
    Task CompleteSuccessfulLoginAsync(
        int userId,
        DateTime loginAt,
        PersistRefreshToken token,
        CancellationToken cancellationToken);
    Task<RefreshTokenState?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task<AuthenticationUser?> FindUserAsync(int userId, CancellationToken cancellationToken);
    Task RotateRefreshTokenAsync(int existingTokenId, PersistRefreshToken replacement, CancellationToken cancellationToken);
    Task<bool> AnyUsersAsync(CancellationToken cancellationToken);
    Task<BootstrapAdminResult> CreateBootstrapAdminAsync(
        string username,
        string email,
        string passwordHash,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken);
}

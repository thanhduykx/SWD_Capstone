using CPMS.BLL.Accounts;
using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CPMS.DAL.Repositories.Accounts;

public sealed class AuthenticationStore(CpmsDbContext dbContext) : IAuthenticationStore
{
    public Task<AuthenticationUser?> FindByLoginAsync(string login, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        var normalized = login.Trim().ToLowerInvariant();
        return dbContext.Users.Where(x => EF.Functions.ILike(x.Username, normalized) ||
                EF.Functions.ILike(x.Email, normalized) ||
                (normalized == "admin" && (EF.Functions.ILike(x.Username, "admin@gmail.com") ||
                                             EF.Functions.ILike(x.Email, "admin@gmail.com"))))
            .Select(MapProjection()).FirstOrDefaultAsync(cancellationToken);
    }
    public Task<AuthenticationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Users.Where(x => x.Email == email).Select(MapProjection()).FirstOrDefaultAsync(cancellationToken);
    public Task<AuthenticationUser?> FindUserAsync(int userId, CancellationToken cancellationToken) =>
        dbContext.Users.Where(x => x.Id == userId).Select(MapProjection()).SingleOrDefaultAsync(cancellationToken);

    public async Task RecordFailedLoginAsync(
        int userId, int failedAttempts, DateTime? lockedUntil, CancellationToken cancellationToken)
    {
        User user = await dbContext.Users.SingleAsync(x => x.Id == userId, cancellationToken);
        user.FailedLoginAttempts = failedAttempts;
        user.LockedUntil = lockedUntil;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public async Task RecordSuccessfulLoginAsync(int userId, DateTime loginAt, CancellationToken cancellationToken)
    {
        User user = await dbContext.Users.SingleAsync(x => x.Id == userId, cancellationToken);
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = loginAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public async Task SaveRefreshTokenAsync(PersistRefreshToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        dbContext.RefreshTokens.Add(NewToken(token));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    public Task<RefreshTokenState?> FindRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.Where(x => x.TokenHash == tokenHash)
            .Select(x => new RefreshTokenState(x.Id, x.UserId, x.IsRevoked, x.ExpiresAt))
            .SingleOrDefaultAsync(cancellationToken);
    public async Task RotateRefreshTokenAsync(
        int existingTokenId, PersistRefreshToken replacement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        RefreshToken existing = await dbContext.RefreshTokens.SingleAsync(x => x.Id == existingTokenId, cancellationToken);
        existing.IsRevoked = true;
        dbContext.RefreshTokens.Add(NewToken(replacement));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
    public Task<bool> AnyUsersAsync(CancellationToken cancellationToken) => dbContext.Users.AnyAsync(cancellationToken);
    public async Task<BootstrapAdminResult> CreateBootstrapAdminAsync(
        string username, string email, string passwordHash, string? ipAddress, string? userAgent,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Username = username, Email = email, PasswordHash = passwordHash,
            Role = UserRole.SystemAdministrator, IsActive = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.SystemAdministrators.Add(new SystemAdministrator
        {
            UserId = user.Id, AdminLevel = "Root", PermissionScope = "DevelopmentBootstrap"
        });
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id, Action = "BOOTSTRAP_ADMIN", EntityType = nameof(User), EntityId = user.Id,
            NewValue = user.Username, IpAddress = ipAddress, UserAgent = userAgent
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new BootstrapAdminResult(user.Id, user.Username, user.Role);
    }

    private static RefreshToken NewToken(PersistRefreshToken x) => new()
    {
        UserId = x.UserId, TokenHash = x.TokenHash, ExpiresAt = x.ExpiresAt, DeviceInfo = x.DeviceInfo
    };
    private static System.Linq.Expressions.Expression<Func<User, AuthenticationUser>> MapProjection() =>
        x => new AuthenticationUser(
            x.Id, x.Username, x.Email, x.PasswordHash, x.Role, x.IsActive,
            x.FailedLoginAttempts, x.LockedUntil);
}

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
                EF.Functions.ILike(x.Email, normalized))
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
    public async Task CompleteSuccessfulLoginAsync(
        int userId,
        DateTime loginAt,
        PersistRefreshToken token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        var user = new User
        {
            Id = userId,
            Username = string.Empty,
            Email = string.Empty,
            PasswordHash = string.Empty
        };
        dbContext.Users.Attach(user);
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = loginAt;
        dbContext.Entry(user).Property(x => x.FailedLoginAttempts).IsModified = true;
        dbContext.Entry(user).Property(x => x.LockedUntil).IsModified = true;
        dbContext.Entry(user).Property(x => x.LastLoginAt).IsModified = true;
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
    private static RefreshToken NewToken(PersistRefreshToken x) => new()
    {
        UserId = x.UserId, TokenHash = x.TokenHash, ExpiresAt = x.ExpiresAt, DeviceInfo = x.DeviceInfo
    };
    private static System.Linq.Expressions.Expression<Func<User, AuthenticationUser>> MapProjection() =>
        x => new AuthenticationUser(
            x.Id, x.Username, x.Email, x.PasswordHash, x.Role, x.IsActive,
            x.FailedLoginAttempts, x.LockedUntil);
}

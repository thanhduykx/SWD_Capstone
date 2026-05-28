using CPMS.Core.Entities;
using CPMS.Core.Enums;

namespace CPMS.Infrastructure.Data.Seeding;

public static class DefaultDataSeeder
{
    public static async Task SeedDefaultAccountAsync(
        CpmsDbContext dbContext,
        string username,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                dbContext.Users,
                x => x.Username == username,
                cancellationToken))
        {
            return;
        }

        dbContext.Users.Add(new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            Role = UserRole.SystemAdministrator,
            IsActive = true
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

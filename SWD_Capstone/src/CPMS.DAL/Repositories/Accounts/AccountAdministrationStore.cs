using CPMS.BLL.Accounts;
using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using CPMS.BLL.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CPMS.DAL.Repositories.Accounts;

public sealed class AccountAdministrationStore(CpmsDbContext dbContext) : IAccountAdministrationStore
{
    public async Task<PagedResult<AccountResult>> GetAllAsync(
        PagedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        IQueryable<User> query = dbContext.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchPattern = PostgreSqlSearchPattern.Contains(request.Search);
            query = query.Where(x =>
                EF.Functions.ILike(x.Username, searchPattern, PostgreSqlSearchPattern.EscapeCharacter) ||
                EF.Functions.ILike(x.Email, searchPattern, PostgreSqlSearchPattern.EscapeCharacter));
        }
        if (!string.IsNullOrWhiteSpace(request.FilterBy) && !string.IsNullOrWhiteSpace(request.FilterValue))
        {
            var value = request.FilterValue.Trim();
            query = request.FilterBy.Trim().ToLowerInvariant() switch
            {
                "role" when Enum.TryParse<UserRole>(value, true, out UserRole role) => query.Where(x => x.Role == role),
                "isactive" when bool.TryParse(value, out var active) => query.Where(x => x.IsActive == active),
                _ => query
            };
        }
        query = request.SortBy?.ToLowerInvariant() switch
        {
            "email" => request.Desc ? query.OrderByDescending(x => x.Email) : query.OrderBy(x => x.Email),
            "role" => request.Desc ? query.OrderByDescending(x => x.Role) : query.OrderBy(x => x.Role),
            "isactive" => request.Desc ? query.OrderByDescending(x => x.IsActive) : query.OrderBy(x => x.IsActive),
            _ => request.Desc ? query.OrderByDescending(x => x.Username) : query.OrderBy(x => x.Username)
        };
        var total = await query.CountAsync(cancellationToken);
        List<AccountResult> items = await query.Skip((request.SafePage - 1) * request.SafePageSize).Take(request.SafePageSize)
            .Select(x => new AccountResult(
                x.Id, x.Username, x.Email, x.Role, x.IsActive, x.LastLoginAt, x.LockedUntil))
            .ToListAsync(cancellationToken);
        return new PagedResult<AccountResult>(items, request.SafePage, request.SafePageSize, total);
    }

    public Task<bool> UsernameOrEmailExistsAsync(
        string username, string email, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(x => x.Username == username || x.Email == email, cancellationToken);

    public async Task<AccountResult> CreateAsync(PersistAccount input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = new User
        {
            Username = input.Username,
            Email = input.Email,
            PasswordHash = input.PasswordHash,
            Role = input.Role,
            IsActive = true,
            CreatedById = input.CreatedByUserId
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.Add(CreateProfile(user.Id, input));
        dbContext.AuditLogs.Add(NewAudit(
            input.CreatedByUserId, "CREATE_ACCOUNT", user.Id, null, user.Username,
            input.IpAddress, input.UserAgent));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(user);
    }

    public async Task<AccountResult?> UpdateStatusAsync(
        int userId,
        bool isActive,
        bool unlock,
        AccountAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        User? user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return null;
        var oldValue = user.IsActive;
        user.IsActive = isActive;
        if (unlock) user.LockedUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        dbContext.AuditLogs.Add(NewAudit(
            audit.ActorUserId, isActive ? "ACTIVATE_ACCOUNT" : "DEACTIVATE_ACCOUNT", userId,
            oldValue.ToString(), isActive.ToString(), audit.IpAddress, audit.UserAgent));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task<(AccountResult Account, AccountProfileSeed Seed)?> FindForRoleUpdateAsync(
        int userId, CancellationToken cancellationToken)
    {
        User? user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return null;
        var student = await dbContext.Students.Where(x => x.UserId == userId)
            .Select(x => new { x.FullName, IdentityCode = x.Code }).FirstOrDefaultAsync(cancellationToken);
        var lecturer = student is null
            ? await dbContext.Lecturers.Where(x => x.UserId == userId)
                .Select(x => new { x.FullName, IdentityCode = x.Code }).FirstOrDefaultAsync(cancellationToken)
            : null;
        return (Map(user), new AccountProfileSeed(
            student?.FullName ?? lecturer?.FullName ?? user.Username,
            student?.IdentityCode ?? lecturer?.IdentityCode ?? user.Username));
    }

    public async Task<AccountResult> UpdateRoleAsync(
        int userId,
        UserRole newRole,
        AccountProfileSeed seed,
        AccountAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seed);
        ArgumentNullException.ThrowIfNull(audit);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        User user = await dbContext.Users.SingleAsync(x => x.Id == userId, cancellationToken);
        UserRole oldRole = user.Role;
        user.Role = newRole;
        user.UpdatedAt = DateTime.UtcNow;
        if (!await RoleProfileExistsAsync(userId, newRole, cancellationToken))
            dbContext.Add(CreateDefaultProfile(userId, newRole, seed));
        dbContext.AuditLogs.Add(NewAudit(
            audit.ActorUserId, "UPDATE_ACCOUNT_ROLE", userId, oldRole.ToString(), newRole.ToString(),
            audit.IpAddress, audit.UserAgent));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(user);
    }

    public Task<bool> UsernameExistsForOtherUserAsync(
        string username, int userId, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(x => x.Username == username && x.Id != userId, cancellationToken);

    public async Task<AccountResult?> UpdateIdentityAsync(
        int userId,
        string username,
        string identityCode,
        AccountAuditContext audit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        User? user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return null;
        string oldValue = user.Username;
        user.Username = username;
        user.UpdatedAt = DateTime.UtcNow;
        switch (user.Role)
        {
            case UserRole.Student:
                Student student = await dbContext.Students.SingleAsync(x => x.UserId == userId, cancellationToken);
                student.Code = identityCode;
                break;
            case UserRole.Lecturer:
                Lecturer lecturer = await dbContext.Lecturers.SingleAsync(x => x.UserId == userId, cancellationToken);
                lecturer.Code = identityCode;
                break;
            case UserRole.TrainingDepartment:
                TrainingDepartment department = await dbContext.TrainingDepartments.SingleAsync(x => x.UserId == userId, cancellationToken);
                department.StaffCode = identityCode;
                break;
        }
        dbContext.AuditLogs.Add(NewAudit(
            audit.ActorUserId, "UPDATE_ACCOUNT_IDENTITY", userId, oldValue,
            $"{username}:{identityCode}", audit.IpAddress, audit.UserAgent));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(user);
    }

    private Task<bool> RoleProfileExistsAsync(int userId, UserRole role, CancellationToken cancellationToken) =>
        role switch
        {
            UserRole.Student => dbContext.Students.AnyAsync(x => x.UserId == userId, cancellationToken),
            UserRole.Lecturer => dbContext.Lecturers.AnyAsync(x => x.UserId == userId, cancellationToken),
            UserRole.TrainingDepartment => dbContext.TrainingDepartments.AnyAsync(x => x.UserId == userId, cancellationToken),
            UserRole.SystemAdministrator => dbContext.SystemAdministrators.AnyAsync(x => x.UserId == userId, cancellationToken),
            _ => Task.FromResult(false)
        };

    private static object CreateProfile(int userId, PersistAccount x) => x.Role switch
    {
        UserRole.Lecturer => new Lecturer
        {
            UserId = userId, Code = x.IdentityCode, FullName = x.FullName,
            Department = x.Department, IsPartTime = x.IsPartTime
        },
        UserRole.TrainingDepartment => new TrainingDepartment
        {
            UserId = userId, DepartmentName = x.Department,
            StaffCode = x.IdentityCode, Position = x.Position
        },
        UserRole.SystemAdministrator => new SystemAdministrator
        {
            UserId = userId, AdminLevel = x.Position, PermissionScope = x.PermissionScope
        },
        UserRole.Student => new Student
        {
            UserId = userId, Code = x.IdentityCode, FullName = x.FullName,
            ClassCode = x.ClassCode, Batch = x.Batch, Major = x.Major
        },
        _ => throw new InvalidOperationException("Unsupported account role.")
    };

    private static object CreateDefaultProfile(int userId, UserRole role, AccountProfileSeed seed) => role switch
    {
        UserRole.Student => new Student
        {
            UserId = userId, Code = seed.IdentityCode, FullName = seed.FullName, ClassCode = "SE", Major = "SE"
        },
        UserRole.Lecturer => new Lecturer
        {
            UserId = userId, Code = seed.IdentityCode, FullName = seed.FullName,
            Department = "General", IsPartTime = false
        },
        UserRole.TrainingDepartment => new TrainingDepartment
        {
            UserId = userId, DepartmentName = "Training Department",
            StaffCode = seed.IdentityCode, Position = "Moderator"
        },
        UserRole.SystemAdministrator => new SystemAdministrator
        {
            UserId = userId, AdminLevel = "Admin", PermissionScope = "System"
        },
        _ => throw new InvalidOperationException("Unsupported account role.")
    };

    private static AuditLog NewAudit(
        int actorId, string action, int entityId, string? oldValue, string? newValue,
        string? ipAddress, string? userAgent) => new()
        {
            UserId = actorId,
            Action = action,
            EntityType = nameof(User),
            EntityId = entityId,
            OldValue = oldValue,
            NewValue = newValue,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
    private static AccountResult Map(User user) =>
        new(user.Id, user.Username, user.Email, user.Role, user.IsActive, user.LastLoginAt, user.LockedUntil);
}

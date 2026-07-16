using CPMS.BLL.Abstractions.Identity;
using CPMS.BLL.DTOs.Accounts;
using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using CPMS.BLL.Models;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Repositories;

public sealed class AccountRepository(CpmsDbContext dbContext) : IAccountRepository
{
    public async Task<PagedResult<AccountResponse>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken)
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
            var filterBy = request.FilterBy.Trim().ToLowerInvariant();
            var filterValue = request.FilterValue.Trim();
            query = filterBy switch
            {
                "role" when Enum.TryParse<UserRole>(filterValue, true, out UserRole role) => query.Where(x => x.Role == role),
                "isactive" when bool.TryParse(filterValue, out var isActive) => query.Where(x => x.IsActive == isActive),
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
        List<AccountResponse> items = await query
            .Skip((request.SafePage - 1) * request.SafePageSize)
            .Take(request.SafePageSize)
            .Select(x => new AccountResponse(
                x.Id,
                x.Username,
                x.Email,
                x.Role,
                x.IsActive,
                x.LastLoginAt,
                x.LockedUntil))
            .ToListAsync(cancellationToken);

        return new PagedResult<AccountResponse>(items, request.SafePage, request.SafePageSize, total);
    }

    public async Task<bool> UsernameOrEmailExistsAsync(string username, string email, CancellationToken cancellationToken) =>
        await dbContext.Users.AnyAsync(x => x.Username == username || x.Email == email, cancellationToken);

    public async Task<CreatedAccountState> AddUserWithProfileAsync(
        CreateAccountPersistenceData account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        var user = new User
        {
            Username = account.Username,
            Email = account.Email,
            PasswordHash = account.PasswordHash,
            Role = account.Role,
            IsActive = true,
            CreatedById = account.CreatedByUserId
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var profile = CreateRoleProfile(user.Id, account);
        dbContext.Add(profile);
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = account.CreatedByUserId,
            Action = "CREATE_ACCOUNT",
            EntityType = nameof(User),
            EntityId = user.Id,
            NewValue = account.Username,
            IpAddress = account.IpAddress,
            UserAgent = account.UserAgent
        });
        return new CreatedAccountState(
            user.Id, user.Username, user.Email, user.Role, user.IsActive,
            user.LastLoginAt, user.LockedUntil);
    }

    private static object CreateRoleProfile(int userId, CreateAccountPersistenceData account) =>
        account.Role switch
        {
            UserRole.Lecturer => new Lecturer
            {
                UserId = userId,
                Code = account.IdentityCode,
                FullName = account.FullName,
                Department = account.Department,
                IsPartTime = account.IsPartTime
            },
            UserRole.TrainingDepartment => new TrainingDepartment
            {
                UserId = userId,
                DepartmentName = account.Department,
                StaffCode = account.IdentityCode,
                Position = account.Position
            },
            UserRole.SystemAdministrator => new SystemAdministrator
            {
                UserId = userId,
                AdminLevel = account.Position,
                PermissionScope = account.PermissionScope
            },
            UserRole.Student => new Student
            {
                UserId = userId,
                Code = account.IdentityCode,
                FullName = account.FullName,
                ClassCode = account.ClassCode,
                Batch = account.Batch,
                Major = account.Major
            },
            _ => throw new ArgumentOutOfRangeException(nameof(account), "Unsupported account role.")
        };
}

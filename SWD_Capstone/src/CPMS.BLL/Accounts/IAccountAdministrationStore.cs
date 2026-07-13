using CPMS.BLL.Enums;
using CPMS.BLL.Models;

namespace CPMS.BLL.Accounts;

public sealed record PersistAccount(
    string Username,
    string Email,
    string PasswordHash,
    UserRole Role,
    int CreatedByUserId,
    string IdentityCode,
    string FullName,
    string Department,
    string Position,
    string PermissionScope,
    bool IsPartTime,
    string ClassCode,
    string? Batch,
    string Major,
    string? IpAddress,
    string? UserAgent);
public sealed record AccountProfileSeed(string FullName, string IdentityCode);

public interface IAccountAdministrationStore
{
    Task<PagedResult<AccountResult>> GetAllAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<bool> UsernameOrEmailExistsAsync(string username, string email, CancellationToken cancellationToken);
    Task<AccountResult> CreateAsync(PersistAccount account, CancellationToken cancellationToken);
    Task<AccountResult?> UpdateStatusAsync(
        int userId, bool isActive, bool unlock, AccountAuditContext audit, CancellationToken cancellationToken);
    Task<(AccountResult Account, AccountProfileSeed Seed)?> FindForRoleUpdateAsync(
        int userId, CancellationToken cancellationToken);
    Task<AccountResult> UpdateRoleAsync(
        int userId,
        UserRole newRole,
        AccountProfileSeed seed,
        AccountAuditContext audit,
        CancellationToken cancellationToken);
}

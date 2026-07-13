using CPMS.BLL.DTOs.Accounts;
using CPMS.BLL.Models;
using CPMS.BLL.Enums;

namespace CPMS.BLL.Abstractions.Identity;

public interface IAccountRepository
{
    Task<PagedResult<AccountResponse>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<bool> UsernameOrEmailExistsAsync(string username, string email, CancellationToken cancellationToken);
    Task<CreatedAccountState> AddUserWithProfileAsync(
        CreateAccountPersistenceData account,
        CancellationToken cancellationToken);
}

public sealed record CreateAccountPersistenceData(
    string Username,
    string Email,
    string PasswordHash,
    UserRole Role,
    string FullName,
    string IdentityCode,
    string Department,
    string Position,
    string PermissionScope,
    bool IsPartTime,
    string ClassCode,
    string? Batch,
    string Major,
    int CreatedByUserId,
    string? IpAddress,
    string? UserAgent);

public sealed record CreatedAccountState(
    int Id,
    string Username,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime? LockedUntil);

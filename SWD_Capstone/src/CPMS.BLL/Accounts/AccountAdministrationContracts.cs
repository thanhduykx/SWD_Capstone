using CPMS.BLL.Enums;
using CPMS.BLL.Models;

namespace CPMS.BLL.Accounts;

public sealed record AccountResult(
    int Id, string Username, string Email, UserRole Role, bool IsActive,
    DateTime? LastLoginAt, DateTime? LockedUntil);
public sealed record CreateAccountData(
    string? Username,
    string? IdentityCode,
    string Email,
    string Password,
    UserRole Role,
    string? FullName,
    string? Department,
    string? Position,
    string? PermissionScope,
    bool IsPartTime,
    string? ClassCode,
    string? Batch,
    string? Major);
public sealed record AccountCreatedResult(
    int Id,
    string Username,
    string Email,
    string InitialPassword,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime? LockedUntil,
    string IdentityCode,
    string EmailDeliveryStatus,
    string? EmailDeliveryError);
public sealed record AccountAuditContext(int ActorUserId, string? IpAddress, string? UserAgent);
public sealed record UpdateAccountIdentityData(string Username, string IdentityCode);
public sealed record AccountNotificationResult(string Status, string? Error);

public interface IAccountWelcomeNotificationSender
{
    Task<AccountNotificationResult> SendAsync(
        string email,
        string fullName,
        string username,
        string initialPassword,
        CancellationToken cancellationToken);
}

public interface IAccountAdministrationService
{
    Task<PagedResult<AccountResult>> GetAllAsync(PagedRequest request, CancellationToken cancellationToken);
    Task<AccountCreatedResult> CreateAsync(
        CreateAccountData data, AccountAuditContext audit, CancellationToken cancellationToken);
    Task<AccountResult> UpdateStatusAsync(
        int userId, bool isActive, bool unlock, AccountAuditContext audit, CancellationToken cancellationToken);
    Task<AccountResult> UpdateRoleAsync(
        int userId, UserRole role, AccountAuditContext audit, CancellationToken cancellationToken);
    Task<AccountResult> UpdateIdentityAsync(
        int userId, UpdateAccountIdentityData data, AccountAuditContext audit, CancellationToken cancellationToken);
}

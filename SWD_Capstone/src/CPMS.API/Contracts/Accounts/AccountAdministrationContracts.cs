using CPMS.Core.Enums;

namespace CPMS.API.Contracts.Accounts;

public sealed record AccountResponse(
    int Id, string Username, string Email, UserRole Role, bool IsActive,
    DateTime? LastLoginAt, DateTime? LockedUntil);
public sealed record CreateAccountRequest(
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
public sealed record UpdateAccountStatusRequest(bool IsActive, bool Unlock);
public sealed record UpdateAccountRoleRequest(UserRole Role);
public sealed record AccountCreatedResponse(
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

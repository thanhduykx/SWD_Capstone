using CPMS.Core.Enums;

namespace CPMS.Application.DTOs.Accounts;

public sealed record AccountResponse(
    int Id,
    string Username,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime? LockedUntil);

public sealed record AccountCreatedResponse(
    int Id,
    string Username,
    string Email,
    string InitialPassword,
    UserRole Role,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime? LockedUntil,
    string IdentityCode);

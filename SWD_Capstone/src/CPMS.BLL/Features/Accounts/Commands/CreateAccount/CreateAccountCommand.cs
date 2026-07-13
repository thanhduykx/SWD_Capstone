using CPMS.Core.Enums;

namespace CPMS.BLL.Features.Accounts.Commands.CreateAccount;

public sealed record CreateAccountCommand(
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
    string? Major,
    int CreatedByUserId,
    string? IpAddress,
    string? UserAgent);

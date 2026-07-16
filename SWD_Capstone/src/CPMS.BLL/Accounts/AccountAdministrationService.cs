using CPMS.BLL.Abstractions.Identity;
using CPMS.BLL.Abstractions.Security;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Models;
using CPMS.BLL.Enums;

namespace CPMS.BLL.Accounts;

public sealed class AccountAdministrationService(
    IAccountAdministrationStore store,
    IAccountUsernameGenerator usernameGenerator,
    IPasswordHasher passwordHasher,
    IAccountWelcomeNotificationSender notificationSender) : IAccountAdministrationService
{
    public Task<PagedResult<AccountResult>> GetAllAsync(
        PagedRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.GetAllAsync(request, cancellationToken);
    }

    public async Task<AccountCreatedResult> CreateAsync(
        CreateAccountData data, AccountAuditContext audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(audit);
        var fullName = Required(data.FullName, "Full name is required for generated account usernames.");
        var identityCode = Required(data.IdentityCode ?? data.Username, "Identity code is required.");
        var email = Required(data.Email, "Email and password are required.");
        if (string.IsNullOrWhiteSpace(data.Password))
            throw new BusinessRuleException("Email and password are required.");
        if (data.Password.Length < 6)
            throw new BusinessRuleException("Password must contain at least 6 characters.");
        if (!Enum.IsDefined(data.Role)) throw new BusinessRuleException("Unsupported account role.");

        var username = usernameGenerator.Generate(fullName, identityCode);
        if (await store.UsernameOrEmailExistsAsync(username, email, cancellationToken))
            throw new ConflictException("Username or email already exists.");
        AccountResult persisted = await store.CreateAsync(
            new PersistAccount(
                username,
                email,
                passwordHasher.Hash(data.Password),
                data.Role,
                audit.ActorUserId,
                identityCode,
                fullName,
                Optional(data.Department, data.Role == UserRole.Lecturer ? string.Empty : "Training Department"),
                Optional(data.Position, data.Role == UserRole.SystemAdministrator ? "Admin" : "Moderator"),
                Optional(data.PermissionScope, "System"),
                data.IsPartTime,
                Optional(data.ClassCode, string.Empty),
                data.Batch,
                Optional(data.Major, "SE"),
                audit.IpAddress,
                audit.UserAgent),
            cancellationToken);
        AccountNotificationResult delivery = await notificationSender.SendAsync(
            persisted.Email, fullName, persisted.Username, data.Password, cancellationToken);
        return new AccountCreatedResult(
            persisted.Id, persisted.Username, persisted.Email, data.Password, persisted.Role,
            persisted.IsActive, persisted.LastLoginAt, persisted.LockedUntil, identityCode,
            delivery.Status, delivery.Error);
    }

    public async Task<AccountResult> UpdateStatusAsync(
        int userId,
        bool isActive,
        bool unlock,
        AccountAuditContext audit,
        CancellationToken cancellationToken) =>
        await store.UpdateStatusAsync(userId, isActive, unlock, audit, cancellationToken)
        ?? throw new KeyNotFoundException("Account not found.");

    public async Task<AccountResult> UpdateRoleAsync(
        int userId,
        UserRole role,
        AccountAuditContext audit,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(role)) throw new BusinessRuleException("Unsupported account role.");
        (AccountResult Account, AccountProfileSeed Seed) current = await store.FindForRoleUpdateAsync(userId, cancellationToken)
            ?? throw new KeyNotFoundException("Account not found.");
        return current.Account.Role == role
            ? current.Account
            : await store.UpdateRoleAsync(userId, role, current.Seed, audit, cancellationToken);
    }

    private static string Required(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new BusinessRuleException(message) : value.Trim();
    private static string Optional(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

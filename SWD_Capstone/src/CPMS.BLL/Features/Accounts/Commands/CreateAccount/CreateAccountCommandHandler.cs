using CPMS.BLL.Abstractions.Identity;
using CPMS.BLL.Abstractions.Persistence;
using CPMS.BLL.Abstractions.Security;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.DTOs.Accounts;
using CPMS.BLL.Enums;

namespace CPMS.BLL.Features.Accounts.Commands.CreateAccount;

public sealed class CreateAccountCommandHandler(
    IAccountRepository accounts,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IAccountUsernameGenerator usernameGenerator)
{
    public async Task<AccountCreatedResponse> HandleAsync(
        CreateAccountCommand command,
        CancellationToken cancellationToken)
    {
        var fullName = Required(command.FullName, "Full name is required.");
        var identityCode = Required(command.IdentityCode, "Identity code is required.");
        var email = Required(command.Email, "Email is required.");
        var username = usernameGenerator.Generate(fullName, identityCode);

        if (await accounts.UsernameOrEmailExistsAsync(username, email, cancellationToken))
        {
            throw new ConflictException("Username or email already exists.");
        }

        EnsureSupportedRole(command.Role, username);
        var account = await accounts.AddUserWithProfileAsync(
            new CreateAccountPersistenceData(
                username,
                email,
                passwordHasher.Hash(command.Password),
                command.Role,
                fullName,
                identityCode,
                OptionalOrDefault(command.Department, command.Role == UserRole.TrainingDepartment
                    ? "Training Department"
                    : string.Empty),
                OptionalOrDefault(command.Position, command.Role == UserRole.SystemAdministrator
                    ? "Admin"
                    : "Moderator"),
                OptionalOrDefault(command.PermissionScope, "System"),
                command.IsPartTime,
                OptionalOrDefault(command.ClassCode, string.Empty),
                command.Batch,
                OptionalOrDefault(command.Major, "SE"),
                command.CreatedByUserId,
                command.IpAddress,
                command.UserAgent),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccountCreatedResponse(
            account.Id,
            account.Username,
            account.Email,
            command.Password,
            account.Role,
            account.IsActive,
            account.LastLoginAt,
            account.LockedUntil,
            identityCode);
    }

    private static void EnsureSupportedRole(UserRole role, string username)
    {
        if (role is not (UserRole.Lecturer or UserRole.TrainingDepartment or
            UserRole.SystemAdministrator or UserRole.Student))
            throw new ArgumentOutOfRangeException(nameof(role), $"Unsupported role for account {username}.");
    }

    private static string Required(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();

    private static string OptionalOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

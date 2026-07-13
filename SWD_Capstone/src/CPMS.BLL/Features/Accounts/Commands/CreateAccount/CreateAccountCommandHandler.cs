using CPMS.BLL.Abstractions.Identity;
using CPMS.BLL.Abstractions.Persistence;
using CPMS.BLL.Abstractions.Security;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.DTOs.Accounts;
using CPMS.Core.Entities;
using CPMS.Core.Enums;

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

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHasher.Hash(command.Password),
            Role = command.Role,
            IsActive = true,
            CreatedById = command.CreatedByUserId
        };

        var profile = CreateRoleProfile(user.Id, username, identityCode, fullName, command);
        var auditLog = new AuditLog
        {
            UserId = command.CreatedByUserId,
            Action = "CREATE_ACCOUNT",
            EntityType = nameof(User),
            EntityId = user.Id,
            NewValue = username,
            IpAddress = command.IpAddress,
            UserAgent = command.UserAgent
        };

        await accounts.AddUserWithProfileAsync(user, profile, auditLog, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccountCreatedResponse(
            user.Id,
            user.Username,
            user.Email,
            command.Password,
            user.Role,
            user.IsActive,
            user.LastLoginAt,
            user.LockedUntil,
            identityCode);
    }

    private static object CreateRoleProfile(
        int userId,
        string username,
        string identityCode,
        string fullName,
        CreateAccountCommand command) =>
        command.Role switch
        {
            UserRole.Lecturer => new Lecturer
            {
                UserId = userId,
                Code = identityCode,
                FullName = fullName,
                Department = OptionalOrDefault(command.Department, string.Empty),
                IsPartTime = command.IsPartTime
            },
            UserRole.TrainingDepartment => new TrainingDepartment
            {
                UserId = userId,
                DepartmentName = OptionalOrDefault(command.Department, "Training Department"),
                StaffCode = identityCode,
                Position = OptionalOrDefault(command.Position, "Moderator")
            },
            UserRole.SystemAdministrator => new SystemAdministrator
            {
                UserId = userId,
                AdminLevel = OptionalOrDefault(command.Position, "Admin"),
                PermissionScope = OptionalOrDefault(command.PermissionScope, "System")
            },
            UserRole.Student => new Student
            {
                UserId = userId,
                Code = identityCode,
                FullName = fullName,
                ClassCode = OptionalOrDefault(command.ClassCode, string.Empty),
                Batch = command.Batch,
                Major = OptionalOrDefault(command.Major, "SE")
            },
            _ => throw new ArgumentOutOfRangeException(nameof(command), $"Unsupported role for account {username}.")
        };

    private static string Required(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();

    private static string OptionalOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

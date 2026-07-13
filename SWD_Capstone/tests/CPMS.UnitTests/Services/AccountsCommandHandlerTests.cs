using CPMS.BLL.Abstractions.Identity;
using CPMS.BLL.Abstractions.Persistence;
using CPMS.BLL.Abstractions.Security;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.DTOs.Accounts;
using CPMS.BLL.Features.Accounts.Commands.CreateAccount;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Core.Services;
using CPMS.Shared.Models;

namespace CPMS.UnitTests.Services;

public sealed class AccountsCommandHandlerTests
{
    [Fact]
    public async Task CreateAccount_CreatesUserWithGeneratedUsername()
    {
        var repository = new InMemoryAccountRepository();
        var handler = new CreateAccountCommandHandler(
            repository,
            new InMemoryUnitOfWork(),
            new FakePasswordHasher(),
            new DefaultAccountUsernameGenerator());

        var result = await handler.HandleAsync(NewCommand(), CancellationToken.None);

        Assert.Equal("AnNVGV001", result.Username);
        Assert.Equal(UserRole.Lecturer, result.Role);
        Assert.Contains(repository.Users, user => user.Email == "lecturer@example.com");
    }

    [Fact]
    public async Task CreateAccount_RejectsDuplicateEmail()
    {
        var repository = new InMemoryAccountRepository();
        repository.Users.Add(new User
        {
            Id = 1,
            Username = "Existing",
            Email = "lecturer@example.com",
            PasswordHash = "hash",
            Role = UserRole.Lecturer
        });
        var handler = new CreateAccountCommandHandler(
            repository,
            new InMemoryUnitOfWork(),
            new FakePasswordHasher(),
            new DefaultAccountUsernameGenerator());

        await Assert.ThrowsAsync<ConflictException>(() => handler.HandleAsync(NewCommand(), CancellationToken.None));
    }

    [Fact]
    public void CreateAccountValidator_RejectsInvalidEmail()
    {
        var validator = new CreateAccountCommandValidator();

        var result = validator.Validate(NewCommand(email: "not-an-email"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateAccountCommand.Email));
    }

    private static CreateAccountCommand NewCommand(string email = "lecturer@example.com") =>
        new(
            "GV001",
            email,
            "Test@123456",
            UserRole.Lecturer,
            "Nguyen Van An",
            "SE",
            null,
            null,
            false,
            null,
            null,
            null,
            99,
            "127.0.0.1",
            "unit-test");

    private sealed class InMemoryAccountRepository : IAccountRepository
    {
        public List<User> Users { get; } = [];

        public IQueryable<User> QueryUsers() => Users.AsQueryable();

        public Task<PagedResult<AccountResponse>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<AccountResponse>([], request.SafePage, request.SafePageSize, Users.Count));

        public Task<bool> UsernameOrEmailExistsAsync(string username, string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.Any(user => user.Username == username || user.Email == email));

        public Task AddUserWithProfileAsync(User user, object profile, AuditLog auditLog, CancellationToken cancellationToken)
        {
            user.Id = Users.Count + 1;
            Users.Add(user);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string passwordHash, string password) => passwordHash == $"hashed:{password}";
    }
}

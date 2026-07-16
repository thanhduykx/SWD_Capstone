using CPMS.BLL.Abstractions.Identity;
using CPMS.BLL.Abstractions.Persistence;
using CPMS.BLL.Abstractions.Security;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.DTOs.Accounts;
using CPMS.BLL.Features.Accounts.Commands.CreateAccount;
using CPMS.BLL.Enums;
using CPMS.BLL.Services;
using CPMS.BLL.Models;
using FluentValidation.Results;

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

        AccountCreatedResponse result = await handler.HandleAsync(NewCommand(), CancellationToken.None);

        Assert.Equal("AnNVGV001", result.Username);
        Assert.Equal(UserRole.Lecturer, result.Role);
        Assert.Contains(repository.Users, user => user.Email == "lecturer@example.com");
    }

    [Fact]
    public async Task CreateAccount_RejectsDuplicateEmail()
    {
        var repository = new InMemoryAccountRepository();
        repository.Users.Add(new CreatedAccountState(
            1, "Existing", "lecturer@example.com", UserRole.Lecturer,
            true, null, null));
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

        ValidationResult result = validator.Validate(NewCommand(email: "not-an-email"));

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
        public List<CreatedAccountState> Users { get; } = [];

        public Task<PagedResult<AccountResponse>> GetPagedAsync(PagedRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<AccountResponse>([], request.SafePage, request.SafePageSize, Users.Count));

        public Task<bool> UsernameOrEmailExistsAsync(string username, string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.Any(user => user.Username == username || user.Email == email));

        public Task<CreatedAccountState> AddUserWithProfileAsync(
            CreateAccountPersistenceData account,
            CancellationToken cancellationToken)
        {
            var user = new CreatedAccountState(
                Users.Count + 1, account.Username, account.Email, account.Role,
                true, null, null);
            Users.Add(user);
            return Task.FromResult(user);
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

using CPMS.BLL.Abstractions.Identity;
using CPMS.BLL.Abstractions.Security;
using CPMS.BLL.Academic;
using CPMS.BLL.Academic.Import;
using CPMS.BLL.Accounts;
using CPMS.BLL.Enums;
using CPMS.BLL.Models;

namespace CPMS.UnitTests.Services;

public sealed class GroupImportServiceTests
{
    [Fact]
    public async Task Import_CreatesEachStudentWithADifferentStrongInitialPassword()
    {
        var store = new FakeImportStore();
        var notifications = new FakeNotificationSender();
        var service = new GroupImportService(
            new FakeParser(),
            store,
            new FakeSemesterService(),
            new FakeUsernameGenerator(),
            new RecordingPasswordHasher(),
            notifications);

        GroupImportResult result = await service.ImportAsync(new MemoryStream([1]), 7, CancellationToken.None);

        Assert.Equal(2, result.StudentAccountsCreated);
        Assert.Equal(2, store.PasswordHashes.Count);
        Assert.Equal(2, notifications.InitialPasswords.Count);
        Assert.Equal(2, notifications.InitialPasswords.Distinct(StringComparer.Ordinal).Count());
        Assert.All(notifications.InitialPasswords, password => Assert.True(password.Length >= 20));
        Assert.Equal(
            notifications.InitialPasswords.Select(password => $"hashed:{password}"),
            store.PasswordHashes);
    }

    private sealed class FakeParser : IGroupWorkbookParser
    {
        public IReadOnlyList<ImportedGroupData> Parse(Stream stream) =>
        [
            new(
                "Secure Import",
                "Student One",
                2,
                [
                    new("Student One", "SE000001", "one@example.com"),
                    new("Student Two", "SE000002", "two@example.com")
                ])
        ];
    }

    private sealed class FakeImportStore : IGroupImportStore
    {
        private int _nextUserId = 1;

        public List<string> PasswordHashes { get; } = [];

        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken) => operation(cancellationToken);

        public Task<bool> LecturerExistsAsync(int lecturerId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<int> FindDefaultLecturerIdAsync(CancellationToken cancellationToken) =>
            Task.FromResult(1);

        public Task<(int Id, bool Created)> UpsertTopicAsync(
            int semesterId,
            string code,
            string title,
            CancellationToken cancellationToken) => Task.FromResult((1, true));

        public Task<(int Id, bool Created)> UpsertGroupAsync(
            int semesterId,
            string code,
            int topicId,
            int? lecturerId,
            CancellationToken cancellationToken) => Task.FromResult((1, true));

        public Task<ImportUserState?> FindUserAsync(
            string username,
            string email,
            CancellationToken cancellationToken) => Task.FromResult<ImportUserState?>(null);

        public Task<ImportUserState> CreateStudentUserAsync(
            string username,
            string email,
            string passwordHash,
            CancellationToken cancellationToken)
        {
            PasswordHashes.Add(passwordHash);
            return Task.FromResult(new ImportUserState(_nextUserId++, UserRole.Student));
        }

        public Task<ImportStudentState?> FindStudentAsync(
            string studentCode,
            CancellationToken cancellationToken) => Task.FromResult<ImportStudentState?>(null);

        public Task<bool> UpsertStudentAsync(
            int userId,
            string studentCode,
            string fullName,
            int groupId,
            bool isLeader,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class FakeSemesterService : ISemesterService
    {
        public Task<SemesterResult> ResolveAsync(DateOnly date, CancellationToken cancellationToken) =>
            Task.FromResult(new SemesterResult(
                1, "SU26", "Summer 2026", "2025-2026", date, date.AddMonths(3), true));

        public Task<PagedResult<SemesterResult>> GetAllAsync(
            PagedRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SemesterResult> CreateAsync(
            CreateSemesterCommand command,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SemesterResult> ToggleStatusAsync(
            int semesterId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(
            int semesterId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<SemesterGroupResult>> GetGroupsAsync(
            int semesterId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeUsernameGenerator : IAccountUsernameGenerator
    {
        public string Generate(string fullName, string identityCode) => identityCode.ToLowerInvariant();
    }

    private sealed class RecordingPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";
        public bool Verify(string passwordHash, string password) => throw new NotSupportedException();
    }

    private sealed class FakeNotificationSender : IAccountWelcomeNotificationSender
    {
        public List<string> InitialPasswords { get; } = [];

        public Task<AccountNotificationResult> SendAsync(
            string email,
            string fullName,
            string username,
            string initialPassword,
            CancellationToken cancellationToken)
        {
            InitialPasswords.Add(initialPassword);
            return Task.FromResult(new AccountNotificationResult("Sent", null));
        }
    }
}

using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CPMS.BLL.Abstractions.Identity;
using CPMS.BLL.Abstractions.Security;
using CPMS.BLL.Accounts;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;

namespace CPMS.BLL.Academic.Import;

public sealed class GroupImportService(
    IGroupWorkbookParser parser,
    IGroupImportStore store,
    ISemesterService semesterService,
    IAccountUsernameGenerator usernameGenerator,
    IPasswordHasher passwordHasher,
    IAccountWelcomeNotificationSender notificationSender) : IGroupImportService
{
    private const int InitialPasswordByteCount = 18;

    public async Task<GroupImportResult> ImportAsync(
        Stream stream, int lecturerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (lecturerId <= 0 || !await store.LecturerExistsAsync(lecturerId, cancellationToken))
            throw new BusinessRuleException($"Lecturer {lecturerId} does not exist.");
        SemesterResult semester = await semesterService.ResolveAsync(
            DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        IReadOnlyList<ImportedGroupData> groups = parser.Parse(stream);
        PersistResult persisted = await store.ExecuteInTransactionAsync(
            token => PersistAsync(groups, semester.Id, lecturerId, token), cancellationToken);
        var sent = 0;
        var failed = 0;
        foreach (PendingAccountNotification pendingNotification in persisted.Accounts)
        {
            CreatedImportAccount account = pendingNotification.Account;
            AccountNotificationResult delivery = await notificationSender.SendAsync(
                account.Email, account.FullName, account.Username,
                pendingNotification.InitialPassword, cancellationToken);
            if (delivery.Status == "Sent") sent++; else failed++;
        }
        return new GroupImportResult(
            persisted.GroupsCreated, persisted.TopicsCreated, persisted.StudentsCreated,
            persisted.Accounts.Count, sent, failed);
    }

    private async Task<PersistResult> PersistAsync(
        IReadOnlyList<ImportedGroupData> groups,
        int semesterId,
        int lecturerId,
        CancellationToken cancellationToken)
    {
        var groupsCreated = 0;
        var topicsCreated = 0;
        var studentsCreated = 0;
        var accounts = new List<PendingAccountNotification>();
        foreach (ImportedGroupData importedGroup in groups)
        {
            PersistedGroup persistedGroup = await PersistGroupAsync(
                importedGroup, semesterId, lecturerId, cancellationToken);
            groupsCreated += persistedGroup.GroupCreated ? 1 : 0;
            topicsCreated += persistedGroup.TopicCreated ? 1 : 0;
            studentsCreated += persistedGroup.StudentsCreated;
            accounts.AddRange(persistedGroup.Accounts);
        }
        return new PersistResult(groupsCreated, topicsCreated, studentsCreated, accounts);
    }

    private async Task<PersistedGroup> PersistGroupAsync(
        ImportedGroupData importedGroup,
        int semesterId,
        int lecturerId,
        CancellationToken cancellationToken)
    {
        var suffix = $"{semesterId}_{importedGroup.FirstRowNumber}";
        (int Id, bool Created) topic = await store.UpsertTopicAsync(
            semesterId, BuildCode("TOPIC", importedGroup.ProjectTitle, suffix),
            importedGroup.ProjectTitle, cancellationToken);
        (int Id, bool Created) group = await store.UpsertGroupAsync(
            semesterId, BuildCode("GROUP", importedGroup.ProjectTitle, suffix),
            topic.Id, lecturerId, cancellationToken);
        var studentsCreated = 0;
        var accounts = new List<PendingAccountNotification>();
        foreach (ImportedStudentData importedStudent in importedGroup.Students)
        {
            PersistedStudent student = await PersistStudentAsync(
                importedStudent, importedGroup.LeaderName, group.Id, cancellationToken);
            studentsCreated += student.Created ? 1 : 0;
            if (student.Account is not null) accounts.Add(student.Account);
        }
        return new PersistedGroup(topic.Created, group.Created, studentsCreated, accounts);
    }

    private async Task<PersistedStudent> PersistStudentAsync(
        ImportedStudentData importedStudent,
        string leaderName,
        int groupId,
        CancellationToken cancellationToken)
    {
        var username = usernameGenerator.Generate(importedStudent.FullName, importedStudent.StudentCode);
        ImportUserState? user = await store.FindUserAsync(username, importedStudent.Email, cancellationToken);
        PendingAccountNotification? account = null;
        if (user is null)
        {
            var initialPassword = GenerateInitialPassword();
            user = await store.CreateStudentUserAsync(
                username, importedStudent.Email, passwordHasher.Hash(initialPassword), cancellationToken);
            account = new PendingAccountNotification(
                new CreatedImportAccount(user.Id, username, importedStudent.Email, importedStudent.FullName),
                initialPassword);
        }
        else if (user.Role != UserRole.Student)
        {
            throw new BusinessRuleException(
                $"The account matching MSSV '{importedStudent.StudentCode}' or email '{importedStudent.Email}' is not a student account.");
        }

        ImportStudentState? student = await store.FindStudentAsync(importedStudent.StudentCode, cancellationToken);
        if (student is not null && student.UserId != user.Id)
            throw new BusinessRuleException(
                $"MSSV '{importedStudent.StudentCode}' belongs to a different user account.");
        var isLeader = importedStudent.FullName.Equals(leaderName, StringComparison.OrdinalIgnoreCase);
        var created = await store.UpsertStudentAsync(
            user.Id, importedStudent.StudentCode, importedStudent.FullName,
            groupId, isLeader, cancellationToken);
        return new PersistedStudent(created, account);
    }

    private static string GenerateInitialPassword()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(InitialPasswordByteCount))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string BuildCode(string prefix, string projectTitle, string stableSuffix)
    {
        var title = Regex.Replace(
            projectTitle.ToUpperInvariant(),
            "[^A-Z0-9]+",
            "_",
            RegexOptions.None,
            TimeSpan.FromSeconds(1)).Trim('_');
        if (string.IsNullOrWhiteSpace(title)) title = "PROJECT";
        var code = $"{prefix}_{stableSuffix}_{title}";
        return code.Length <= 50 ? code : code[..50].TrimEnd('_');
    }

    private sealed record PersistResult(
        int GroupsCreated, int TopicsCreated, int StudentsCreated,
        IReadOnlyList<PendingAccountNotification> Accounts);

    private sealed record PendingAccountNotification(
        CreatedImportAccount Account,
        string InitialPassword);

    private sealed record PersistedStudent(bool Created, PendingAccountNotification? Account);

    private sealed record PersistedGroup(
        bool TopicCreated,
        bool GroupCreated,
        int StudentsCreated,
        IReadOnlyList<PendingAccountNotification> Accounts);
}

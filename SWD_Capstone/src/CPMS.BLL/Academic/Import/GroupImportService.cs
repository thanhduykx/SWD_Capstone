using System.Text.RegularExpressions;
using CPMS.BLL.Abstractions.Identity;
using CPMS.BLL.Abstractions.Security;
using CPMS.BLL.Accounts;
using CPMS.Core.Enums;
using CPMS.Core.Exceptions;

namespace CPMS.BLL.Academic.Import;

public sealed class GroupImportService(
    IGroupWorkbookParser parser,
    IGroupImportStore store,
    ISemesterService semesterService,
    IAccountUsernameGenerator usernameGenerator,
    IPasswordHasher passwordHasher,
    IAccountWelcomeNotificationSender notificationSender) : IGroupImportService
{
    private const string TemporaryPassword = "Abcd@1234";

    public async Task<GroupImportResult> ImportAsync(
        Stream stream, int lecturerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (lecturerId <= 0 || !await store.LecturerExistsAsync(lecturerId, cancellationToken))
            throw new BusinessRuleException($"Lecturer {lecturerId} does not exist.");
        var semester = await semesterService.ResolveAsync(
            DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        var groups = parser.Parse(stream);
        var passwordHash = passwordHasher.Hash(TemporaryPassword);
        var persisted = await store.ExecuteInTransactionAsync(
            token => PersistAsync(groups, semester.Id, lecturerId, passwordHash, token), cancellationToken);
        var sent = 0;
        var failed = 0;
        foreach (var account in persisted.Accounts)
        {
            var delivery = await notificationSender.SendAsync(
                account.Email, account.FullName, account.Username, TemporaryPassword, cancellationToken);
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
        string passwordHash,
        CancellationToken cancellationToken)
    {
        var groupsCreated = 0;
        var topicsCreated = 0;
        var studentsCreated = 0;
        var accounts = new List<CreatedImportAccount>();
        foreach (var importedGroup in groups)
        {
            var suffix = $"{semesterId}_{importedGroup.FirstRowNumber}";
            var topic = await store.UpsertTopicAsync(
                semesterId, BuildCode("TOPIC", importedGroup.ProjectTitle, suffix),
                importedGroup.ProjectTitle, cancellationToken);
            if (topic.Created) topicsCreated++;
            var group = await store.UpsertGroupAsync(
                semesterId, BuildCode("GROUP", importedGroup.ProjectTitle, suffix),
                topic.Id, lecturerId, cancellationToken);
            if (group.Created) groupsCreated++;
            foreach (var importedStudent in importedGroup.Students)
            {
                var username = usernameGenerator.Generate(importedStudent.FullName, importedStudent.StudentCode);
                var user = await store.FindUserAsync(username, importedStudent.Email, cancellationToken);
                if (user is null)
                {
                    user = await store.CreateStudentUserAsync(
                        username, importedStudent.Email, passwordHash, cancellationToken);
                    accounts.Add(new CreatedImportAccount(
                        user.Id, username, importedStudent.Email, importedStudent.FullName));
                }
                else if (user.Role != UserRole.Student)
                {
                    throw new BusinessRuleException(
                        $"The account matching MSSV '{importedStudent.StudentCode}' or email '{importedStudent.Email}' is not a student account.");
                }
                var student = await store.FindStudentAsync(importedStudent.StudentCode, cancellationToken);
                if (student is not null && student.UserId != user.Id)
                    throw new BusinessRuleException(
                        $"MSSV '{importedStudent.StudentCode}' belongs to a different user account.");
                var isLeader = importedStudent.FullName.Equals(
                    importedGroup.LeaderName, StringComparison.OrdinalIgnoreCase);
                if (await store.UpsertStudentAsync(
                        user.Id, importedStudent.StudentCode, importedStudent.FullName,
                        group.Id, isLeader, cancellationToken))
                    studentsCreated++;
            }
        }
        return new PersistResult(groupsCreated, topicsCreated, studentsCreated, accounts);
    }

    private static string BuildCode(string prefix, string projectTitle, string stableSuffix)
    {
        var title = Regex.Replace(projectTitle.ToUpperInvariant(), "[^A-Z0-9]+", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(title)) title = "PROJECT";
        var code = $"{prefix}_{stableSuffix}_{title}";
        return code.Length <= 50 ? code : code[..50].TrimEnd('_');
    }

    private sealed record PersistResult(
        int GroupsCreated, int TopicsCreated, int StudentsCreated, IReadOnlyList<CreatedImportAccount> Accounts);
}

namespace CPMS.BLL.Academic.Import;

public static class GroupImportLimits
{
    public const long MaximumWorkbookBytes = 5 * 1024 * 1024;
}

public sealed record ImportedGroupData(
    string ProjectTitle, string LeaderName, int FirstRowNumber, IReadOnlyList<ImportedStudentData> Students);
public sealed record ImportedStudentData(string FullName, string StudentCode, string Email);
public sealed record GroupImportResult(
    int GroupsCreated, int TopicsCreated, int StudentsCreated,
    int StudentAccountsCreated, int EmailsSent, int EmailsFailed);
public interface IGroupWorkbookParser
{
    IReadOnlyList<ImportedGroupData> Parse(Stream stream);
}
public interface IGroupImportService
{
    Task<GroupImportResult> ImportAsync(
        Stream stream, int? lecturerId = null, CancellationToken cancellationToken = default);
}

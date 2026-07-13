using System.Net.Mail;
using System.Text;
using ClosedXML.Excel;
using CPMS.Core.Exceptions;

namespace CPMS.Api.Services;

internal static class ExcelGroupWorkbookParser
{
    private const string ProjectTitleHeader = "Tên dự án (có thể trùng tên nhóm)";
    private const string StudentNameHeader = "Họ & Tên";
    private const string StudentCodeHeader = "MSSV";
    private const string StudentEmailHeader = "Email (email trên FAP của SV)";
    private const string LeaderNameHeader = "Họ & Tên Leader";

    public static IReadOnlyList<ImportedGroup> Parse(IXLWorksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);

        var headerRow = worksheet.FirstRowUsed()
            ?? throw new BusinessRuleException("The uploaded Excel worksheet is empty.");
        var columns = ResolveColumns(headerRow);
        var groups = new List<ImportedGroup>();
        ImportedGroupBuilder? currentGroup = null;
        var studentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var studentEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in worksheet.RowsUsed().Where(row => row.RowNumber() > headerRow.RowNumber()))
        {
            EnsureSelectedCellsAreValues(row, columns.Values);

            var projectTitle = GetCellString(row, columns[ProjectTitleHeader]);
            var studentName = GetCellString(row, columns[StudentNameHeader]);
            var studentCode = GetCellString(row, columns[StudentCodeHeader]);
            var studentEmail = GetCellString(row, columns[StudentEmailHeader]);
            var leaderName = GetCellString(row, columns[LeaderNameHeader]);

            if (SelectedValuesAreEmpty(projectTitle, studentName, studentCode, studentEmail, leaderName))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(projectTitle))
            {
                AddCompletedGroup(groups, currentGroup);
                currentGroup = new ImportedGroupBuilder(projectTitle, leaderName, row.RowNumber());
            }
            else if (currentGroup is null)
            {
                throw RowError(row, "a student row appears before the first project title");
            }

            if (currentGroup is null)
            {
                throw RowError(row, "project title is required");
            }

            if (!string.IsNullOrWhiteSpace(leaderName))
            {
                if (!string.IsNullOrWhiteSpace(currentGroup.LeaderName) &&
                    !currentGroup.LeaderName.Equals(leaderName, StringComparison.OrdinalIgnoreCase))
                {
                    throw RowError(row, "multiple leader names were provided for the same project");
                }

                currentGroup.LeaderName = leaderName;
            }

            if (string.IsNullOrWhiteSpace(studentName) && string.IsNullOrWhiteSpace(studentCode) && string.IsNullOrWhiteSpace(studentEmail))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(studentName) || string.IsNullOrWhiteSpace(studentCode) || string.IsNullOrWhiteSpace(studentEmail))
            {
                throw RowError(row, "Họ & Tên, MSSV, and Email are all required for each student");
            }

            if (!MailAddress.TryCreate(studentEmail, out _))
            {
                throw RowError(row, $"'{studentEmail}' is not a valid student email");
            }

            if (!studentCodes.Add(studentCode))
            {
                throw RowError(row, $"duplicate MSSV '{studentCode}'");
            }

            if (!studentEmails.Add(studentEmail))
            {
                throw RowError(row, $"duplicate student email '{studentEmail}'");
            }

            currentGroup.Students.Add(new ImportedStudent(studentName, studentCode, studentEmail));
        }

        AddCompletedGroup(groups, currentGroup);
        if (groups.Count == 0)
        {
            throw new BusinessRuleException("The uploaded Excel worksheet contains no group data.");
        }

        return groups;
    }

    private static Dictionary<string, int> ResolveColumns(IXLRow headerRow)
    {
        var requiredHeaders = new[]
        {
            ProjectTitleHeader,
            StudentNameHeader,
            StudentCodeHeader,
            StudentEmailHeader,
            LeaderNameHeader
        };
        var availableColumns = headerRow.CellsUsed()
            .Where(cell => !string.IsNullOrWhiteSpace(cell.GetString()))
            .GroupBy(cell => NormalizeHeader(cell.GetString()), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in requiredHeaders)
        {
            if (!availableColumns.TryGetValue(NormalizeHeader(header), out var columnNumber))
            {
                throw new BusinessRuleException($"Required Excel column '{header}' was not found.");
            }

            result[header] = columnNumber;
        }

        return result;
    }

    private static void AddCompletedGroup(List<ImportedGroup> groups, ImportedGroupBuilder? group)
    {
        if (group is null)
        {
            return;
        }

        if (group.Students.Count == 0)
        {
            throw new BusinessRuleException($"Excel row {group.FirstRowNumber}: project '{group.ProjectTitle}' has no students.");
        }

        if (string.IsNullOrWhiteSpace(group.LeaderName))
        {
            throw new BusinessRuleException($"Excel row {group.FirstRowNumber}: project '{group.ProjectTitle}' has no leader.");
        }

        var leaderMatches = group.Students.Count(student =>
            student.FullName.Equals(group.LeaderName, StringComparison.OrdinalIgnoreCase));
        if (leaderMatches != 1)
        {
            throw new BusinessRuleException(
                $"Excel row {group.FirstRowNumber}: leader '{group.LeaderName}' must match exactly one student in project '{group.ProjectTitle}'.");
        }

        groups.Add(new ImportedGroup(group.ProjectTitle, group.LeaderName, group.FirstRowNumber, group.Students));
    }

    private static void EnsureSelectedCellsAreValues(IXLRow row, IEnumerable<int> columnNumbers)
    {
        foreach (var columnNumber in columnNumbers)
        {
            if (row.Cell(columnNumber).HasFormula)
            {
                throw RowError(row, "formulas are not allowed in imported columns");
            }
        }
    }

    private static string GetCellString(IXLRow row, int columnNumber) => row.Cell(columnNumber).GetString().Trim();

    private static bool SelectedValuesAreEmpty(params string[] values) => values.All(string.IsNullOrWhiteSpace);

    private static string NormalizeHeader(string value)
    {
        var normalized = new StringBuilder(value.Length);
        var previousWasWhitespace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                {
                    normalized.Append(' ');
                }

                previousWasWhitespace = true;
                continue;
            }

            normalized.Append(character);
            previousWasWhitespace = false;
        }

        return normalized.ToString();
    }

    private static BusinessRuleException RowError(IXLRow row, string message) =>
        new($"Excel row {row.RowNumber()}: {message}.");

    private sealed class ImportedGroupBuilder(string projectTitle, string leaderName, int firstRowNumber)
    {
        public string ProjectTitle { get; } = projectTitle;
        public string LeaderName { get; set; } = leaderName;
        public int FirstRowNumber { get; } = firstRowNumber;
        public List<ImportedStudent> Students { get; } = [];
    }
}

internal sealed record ImportedGroup(
    string ProjectTitle,
    string LeaderName,
    int FirstRowNumber,
    IReadOnlyList<ImportedStudent> Students);

internal sealed record ImportedStudent(string FullName, string StudentCode, string Email);

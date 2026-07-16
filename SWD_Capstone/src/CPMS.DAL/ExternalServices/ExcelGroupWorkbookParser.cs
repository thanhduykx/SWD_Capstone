using System.IO.Compression;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CPMS.BLL.Academic.Import;
using CPMS.BLL.Exceptions;

namespace CPMS.DAL.ExternalServices;

public sealed class ExcelGroupWorkbookParser : IGroupWorkbookParser
{
    private const int MaximumArchiveEntries = 512;
    private const long MaximumUncompressedArchiveBytes = 50 * 1024 * 1024;
    private const long MaximumWorksheetXmlBytes = 10 * 1024 * 1024;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private const string DataValidationsPattern =
        "<(?:[a-zA-Z0-9\\-]+:)?dataValidations[^>]*>[\\s\\S]*?</(?:[a-zA-Z0-9\\-]+:)?dataValidations>";
    private static readonly string[][] HeaderAliases =
    [
        ["Tên dự án (có thể trùng tên nhóm)", "TÃªn dá»± Ã¡n (cÃ³ thá»ƒ trÃ¹ng tÃªn nhÃ³m)", "Project Title"],
        ["Họ & Tên", "Há» & TÃªn", "Student Name"],
        ["MSSV", "Student Code"],
        ["Email (email trên FAP của SV)", "Email (email trÃªn FAP cá»§a SV)", "Student Email"],
        ["Họ & Tên Leader", "Há» & TÃªn Leader", "Leader Name"]
    ];

    public IReadOnlyList<ImportedGroupData> Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using MemoryStream cleaned = RemoveDataValidation(stream);
        using var workbook = new XLWorkbook(cleaned);
        IXLWorksheet worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new BusinessRuleException("The uploaded Excel workbook contains no worksheets.");
        IXLRow header = worksheet.FirstRowUsed()
            ?? throw new BusinessRuleException("The uploaded Excel worksheet is empty.");
        var columns = ResolveColumns(header);
        var groups = new List<ImportedGroupData>();
        GroupBuilder? current = null;
        var studentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var studentEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (IXLRow? row in worksheet.RowsUsed().Where(x => x.RowNumber() > header.RowNumber()))
        {
            RowData data = ReadRow(row, columns);
            var project = data.Project;
            var name = data.Name;
            var code = data.Code;
            var email = data.Email;
            var leader = data.Leader;
            if (new[] { project, name, code, email, leader }.All(string.IsNullOrWhiteSpace)) continue;
            current = ResolveGroup(row, data, current, groups);
            UpdateLeader(row, current, leader);
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(email))
                continue;
            AddStudent(row, current, name, code, email, studentCodes, studentEmails);
        }
        AddGroup(groups, current);
        if (groups.Count == 0) throw new BusinessRuleException("The uploaded Excel worksheet contains no group data.");
        return groups;
    }

    private static RowData ReadRow(IXLRow row, int[] columns)
    {
        if (columns.Any(column => row.Cell(column).HasFormula))
            throw RowError(row, "formulas are not allowed in imported columns");
        return new RowData(
            Cell(row, columns[0]), Cell(row, columns[1]), Cell(row, columns[2]),
            Cell(row, columns[3]), Cell(row, columns[4]));
    }

    private static GroupBuilder ResolveGroup(
        IXLRow row,
        RowData data,
        GroupBuilder? current,
        List<ImportedGroupData> groups)
    {
        if (string.IsNullOrWhiteSpace(data.Project))
            return current ?? throw RowError(row, "a student row appears before the first project title");
        AddGroup(groups, current);
        return new GroupBuilder(data.Project, data.Leader, row.RowNumber());
    }

    private static void AddStudent(
        IXLRow row,
        GroupBuilder group,
        string name,
        string code,
        string email,
        HashSet<string> studentCodes,
        HashSet<string> studentEmails)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(email))
            throw RowError(row, "Họ & Tên, MSSV, and Email are all required for each student");
        if (!MailAddress.TryCreate(email, out _)) throw RowError(row, $"'{email}' is not a valid student email");
        if (!studentCodes.Add(code)) throw RowError(row, $"duplicate MSSV '{code}'");
        if (!studentEmails.Add(email)) throw RowError(row, $"duplicate student email '{email}'");
        group.Students.Add(new ImportedStudentData(name, code, email));
    }

    private static void UpdateLeader(IXLRow row, GroupBuilder group, string leader)
    {
        if (string.IsNullOrWhiteSpace(leader)) return;
        if (!string.IsNullOrWhiteSpace(group.LeaderName) &&
            !group.LeaderName.Equals(leader, StringComparison.OrdinalIgnoreCase))
            throw RowError(row, "multiple leader names were provided for the same project");
        group.LeaderName = leader;
    }

    private static int[] ResolveColumns(IXLRow header)
    {
        var available = header.CellsUsed()
            .Where(x => !string.IsNullOrWhiteSpace(x.GetString()))
            .GroupBy(x => Normalize(x.GetString()), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
        return HeaderAliases.Select(aliases => aliases.Select(Normalize)
                .Where(available.ContainsKey).Select(alias => available[alias]).Cast<int?>().FirstOrDefault()
                ?? throw new BusinessRuleException($"Required Excel column '{aliases[0]}' was not found."))
            .ToArray();
    }

    private static void AddGroup(List<ImportedGroupData> groups, GroupBuilder? group)
    {
        if (group is null) return;
        if (group.Students.Count == 0)
            throw new BusinessRuleException($"Excel row {group.Row}: project '{group.ProjectTitle}' has no students.");
        if (string.IsNullOrWhiteSpace(group.LeaderName))
            throw new BusinessRuleException($"Excel row {group.Row}: project '{group.ProjectTitle}' has no leader.");
        if (group.Students.Count(x => x.FullName.Equals(group.LeaderName, StringComparison.OrdinalIgnoreCase)) != 1)
            throw new BusinessRuleException(
                $"Excel row {group.Row}: leader '{group.LeaderName}' must match exactly one student in project '{group.ProjectTitle}'.");
        groups.Add(new ImportedGroupData(group.ProjectTitle, group.LeaderName, group.Row, group.Students));
    }

    private static MemoryStream RemoveDataValidation(Stream source)
    {
        var memory = new MemoryStream();
        CopyWithLimit(source, memory, GroupImportLimits.MaximumWorkbookBytes);
        memory.Position = 0;
        try
        {
            using var archive = new ZipArchive(memory, ZipArchiveMode.Update, leaveOpen: true);
            ValidateArchive(archive);
            foreach (ZipArchiveEntry? entry in archive.Entries.Where(x =>
                         x.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                         x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                string xml;
                using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
                var updated = Regex.Replace(
                    xml, DataValidationsPattern, string.Empty, RegexOptions.IgnoreCase, RegexTimeout);
                if (updated == xml) continue;
                var name = entry.FullName;
                entry.Delete();
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(updated);
            }
        }
        catch (InvalidDataException)
        {
            throw new BusinessRuleException("The uploaded file is not a valid .xlsx workbook.");
        }
        memory.Position = 0;
        return memory;
    }

    private static void CopyWithLimit(Stream source, Stream destination, long maximumBytes)
    {
        var buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;
        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > maximumBytes)
                throw new BusinessRuleException("The uploaded Excel workbook must not exceed 5 MB.");
            destination.Write(buffer, 0, bytesRead);
        }
    }

    private static void ValidateArchive(ZipArchive archive)
    {
        if (archive.Entries.Count > MaximumArchiveEntries)
            throw new BusinessRuleException("The uploaded Excel workbook contains too many archive entries.");

        long totalUncompressedBytes = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            totalUncompressedBytes += entry.Length;
            if (entry.Length > MaximumWorksheetXmlBytes &&
                entry.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("An Excel worksheet exceeds the safe processing limit.");
            if (totalUncompressedBytes > MaximumUncompressedArchiveBytes)
                throw new BusinessRuleException("The uploaded Excel workbook expands beyond the safe processing limit.");
        }
    }
    private static string Cell(IXLRow row, int column) => row.Cell(column).GetString().Trim();
    private static string Normalize(string value)
    {
        var builder = new StringBuilder();
        var whitespace = false;
        foreach (var c in value.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                if (!whitespace) builder.Append(' ');
                whitespace = true;
            }
            else { builder.Append(c); whitespace = false; }
        }
        return builder.ToString();
    }
    private static BusinessRuleException RowError(IXLRow row, string message) =>
        new($"Excel row {row.RowNumber()}: {message}.");
    private sealed class GroupBuilder(string projectTitle, string leaderName, int row)
    {
        public string ProjectTitle { get; } = projectTitle;
        public string LeaderName { get; set; } = leaderName;
        public int Row { get; } = row;
        public List<ImportedStudentData> Students { get; } = [];
    }

    private sealed record RowData(string Project, string Name, string Code, string Email, string Leader);
}

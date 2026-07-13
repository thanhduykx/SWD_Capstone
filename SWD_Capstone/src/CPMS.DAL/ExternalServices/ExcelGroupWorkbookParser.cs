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
        using var cleaned = RemoveDataValidation(stream);
        using var workbook = new XLWorkbook(cleaned);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new BusinessRuleException("The uploaded Excel workbook contains no worksheets.");
        var header = worksheet.FirstRowUsed()
            ?? throw new BusinessRuleException("The uploaded Excel worksheet is empty.");
        var columns = ResolveColumns(header);
        var groups = new List<ImportedGroupData>();
        GroupBuilder? current = null;
        var studentCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var studentEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in worksheet.RowsUsed().Where(x => x.RowNumber() > header.RowNumber()))
        {
            foreach (var column in columns)
                if (row.Cell(column).HasFormula) throw RowError(row, "formulas are not allowed in imported columns");
            var project = Cell(row, columns[0]);
            var name = Cell(row, columns[1]);
            var code = Cell(row, columns[2]);
            var email = Cell(row, columns[3]);
            var leader = Cell(row, columns[4]);
            if (new[] { project, name, code, email, leader }.All(string.IsNullOrWhiteSpace)) continue;
            if (!string.IsNullOrWhiteSpace(project))
            {
                AddGroup(groups, current);
                current = new GroupBuilder(project, leader, row.RowNumber());
            }
            else if (current is null) throw RowError(row, "a student row appears before the first project title");
            if (current is null) throw RowError(row, "project title is required");
            if (!string.IsNullOrWhiteSpace(leader))
            {
                if (!string.IsNullOrWhiteSpace(current.LeaderName) &&
                    !current.LeaderName.Equals(leader, StringComparison.OrdinalIgnoreCase))
                    throw RowError(row, "multiple leader names were provided for the same project");
                current.LeaderName = leader;
            }
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(email))
                continue;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(email))
                throw RowError(row, "Họ & Tên, MSSV, and Email are all required for each student");
            if (!MailAddress.TryCreate(email, out _)) throw RowError(row, $"'{email}' is not a valid student email");
            if (!studentCodes.Add(code)) throw RowError(row, $"duplicate MSSV '{code}'");
            if (!studentEmails.Add(email)) throw RowError(row, $"duplicate student email '{email}'");
            current.Students.Add(new ImportedStudentData(name, code, email));
        }
        AddGroup(groups, current);
        if (groups.Count == 0) throw new BusinessRuleException("The uploaded Excel worksheet contains no group data.");
        return groups;
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

    private static void AddGroup(ICollection<ImportedGroupData> groups, GroupBuilder? group)
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
        source.CopyTo(memory);
        memory.Position = 0;
        try
        {
            using var archive = new ZipArchive(memory, ZipArchiveMode.Update, leaveOpen: true);
            foreach (var entry in archive.Entries.Where(x =>
                         x.FullName.StartsWith("xl/worksheets/sheet", StringComparison.OrdinalIgnoreCase) &&
                         x.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                string xml;
                using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
                var updated = Regex.Replace(xml,
                    "<(?:[a-zA-Z0-9\\-]+:)?dataValidations[^>]*>[\\s\\S]*?</(?:[a-zA-Z0-9\\-]+:)?dataValidations>",
                    string.Empty, RegexOptions.IgnoreCase);
                if (updated == xml) continue;
                var name = entry.FullName;
                entry.Delete();
                using var writer = new StreamWriter(archive.CreateEntry(name).Open());
                writer.Write(updated);
            }
        }
        catch (InvalidDataException) { }
        memory.Position = 0;
        return memory;
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
}

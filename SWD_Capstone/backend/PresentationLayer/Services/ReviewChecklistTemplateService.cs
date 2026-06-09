using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CPMS.Core.Enums;

namespace CPMS.Api.Services;

public sealed class ReviewChecklistTemplateService(IWebHostEnvironment environment)
{
    private const string TemplateFileName = "05_Checklist_CapstoneProjectReview1_Final.xlsx";

    public IReadOnlyList<ReviewChecklistTemplateItem> GetItems(ReviewType type)
    {
        using var workbook = OpenTemplate();
        var sheet = workbook.Worksheet(SheetName(type));
        return ReadItems(sheet, type);
    }

    public byte[] ExportWorkbook(ReviewChecklistExportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var workbook = OpenTemplate();
        var sheet = workbook.Worksheet(SheetName(data.Type));
        FillMetadata(sheet, data);

        var rows = ReadItems(sheet, data.Type);
        foreach (var row in rows.Where(x => !x.IsSection))
        {
            var reviewerResponses = data.Reviewers
                .Select(reviewer => new
                {
                    Reviewer = reviewer,
                    Response = reviewer.Items.SingleOrDefault(x => x.ItemKey == row.ItemKey)
                })
                .Where(x => x.Response is not null)
                .ToList();

            if (data.Type == ReviewType.Review3)
            {
                var comments = reviewerResponses
                    .Where(x => !string.IsNullOrWhiteSpace(x.Response!.Comment))
                    .Select(x => Prefix(x.Reviewer, x.Response!.Comment!));
                SetWrapped(sheet.Cell(row.RowNumber, 6), string.Join(Environment.NewLine, comments));
                continue;
            }

            if (reviewerResponses.Any(x => x.Response!.Answer == ReviewChecklistAnswer.Yes))
            {
                sheet.Cell(row.RowNumber, 2).SetValue("X");
            }

            if (reviewerResponses.Any(x => x.Response!.Answer == ReviewChecklistAnswer.No))
            {
                sheet.Cell(row.RowNumber, 3).SetValue("X");
            }

            if (reviewerResponses.Any(x => x.Response!.Answer == ReviewChecklistAnswer.NotApplicable))
            {
                sheet.Cell(row.RowNumber, 4).SetValue("X");
            }

            var noteLines = reviewerResponses
                .Where(x => !string.IsNullOrWhiteSpace(x.Response!.Comment))
                .Select(x => Prefix(x.Reviewer, x.Response!.Comment!));
            SetWrapped(sheet.Cell(row.RowNumber, 5), string.Join(Environment.NewLine, noteLines));
        }

        if (data.Type == ReviewType.Review3)
        {
            FillReviewThreeFinalComment(sheet, data.Reviewers.Select(x => Prefix(x, x.ReviewerComment)));
        }
        else
        {
            FillTextBlock(sheet, "* Comments", data.Reviewers.Select(x => Prefix(x, x.ReviewerComment)));
            FillTextBlock(sheet, "* Suggestion", data.Reviewers.Select(x => Prefix(x, x.Suggestion)));
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private XLWorkbook OpenTemplate()
    {
        var templatePath = Path.Combine(environment.ContentRootPath, "ReviewTemplates", TemplateFileName);
        if (!File.Exists(templatePath))
        {
            throw new InvalidOperationException($"Review checklist template was not found at {templatePath}.");
        }

        return new XLWorkbook(templatePath);
    }

    private static IReadOnlyList<ReviewChecklistTemplateItem> ReadItems(IXLWorksheet sheet, ReviewType type)
    {
        return type == ReviewType.Review3
            ? ReadReviewThreeItems(sheet, type)
            : ReadReviewChecklistItems(sheet, type);
    }

    private static IReadOnlyList<ReviewChecklistTemplateItem> ReadReviewChecklistItems(IXLWorksheet sheet, ReviewType type)
    {
        var headerRow = FindRow(sheet, "Question");
        var commentsRow = FindRow(sheet, "* Comments");
        var items = new List<ReviewChecklistTemplateItem>();

        for (var row = headerRow + 1; row < commentsRow; row++)
        {
            var label = sheet.Cell(row, 1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var isSection = IsChecklistSection(label);
            items.Add(new ReviewChecklistTemplateItem(
                ItemKey(type, row),
                label,
                null,
                sheet.Cell(row, 6).GetString().Trim(),
                row,
                isSection,
                null));
        }

        return items;
    }

    private static IReadOnlyList<ReviewChecklistTemplateItem> ReadReviewThreeItems(IXLWorksheet sheet, ReviewType type)
    {
        var headerRow = FindRow(sheet, "Code");
        var items = new List<ReviewChecklistTemplateItem>();

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow;
        for (var row = headerRow + 1; row <= lastRow; row++)
        {
            var code = sheet.Cell(row, 1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(code) || code.StartsWith("II.", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var name = sheet.Cell(row, 2).GetString().Trim();
            var criteria = sheet.Cell(row, 3).GetString().Trim();
            items.Add(new ReviewChecklistTemplateItem(
                $"{type}:{code}",
                name,
                criteria,
                null,
                row,
                false,
                code));
        }

        return items;
    }

    private static int FindRow(IXLWorksheet sheet, string firstCellText)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var row = 1; row <= lastRow; row++)
        {
            if (sheet.Cell(row, 1).GetString().Trim().Equals(firstCellText, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        throw new InvalidOperationException($"Template row '{firstCellText}' was not found in sheet '{sheet.Name}'.");
    }

    private static bool IsChecklistSection(string label) =>
        Regex.IsMatch(label, "^[PD][0-9]+\\s*-", RegexOptions.IgnoreCase);

    private static string ItemKey(ReviewType type, int row) => $"{type}:R{row}";

    private static string SheetName(ReviewType type) => type switch
    {
        ReviewType.Review1 => "Review 1",
        ReviewType.Review2 => "Review 2",
        ReviewType.Review3 => "Review 3- Final",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported review type.")
    };

    private static void FillMetadata(IXLWorksheet sheet, ReviewChecklistExportData data)
    {
        var reviewerLine = string.Join(", ", data.Reviewers.Select(x => $"{x.ReviewerCode} - {x.ReviewerName}"));
        var effortHours = data.Reviewers
            .Where(x => x.EffortHours.HasValue)
            .Select(x => x.EffortHours!.Value)
            .DefaultIfEmpty(0)
            .Sum();

        if (data.Type == ReviewType.Review1)
        {
            sheet.Cell("B3").SetValue(data.GroupCode);
            sheet.Cell("B4").SetValue(data.ProjectName);
            sheet.Cell("B5").SetValue(reviewerLine);
            sheet.Cell("B6").SetValue(data.ReviewDate.ToString("dd-MMM-yy", CultureInfo.InvariantCulture));
            sheet.Cell("B7").SetValue(effortHours == 0 ? string.Empty : effortHours.ToString("0.##", CultureInfo.InvariantCulture));
            return;
        }

        if (data.Type == ReviewType.Review2)
        {
            sheet.Cell("B3").SetValue(data.GroupCode);
            sheet.Cell("B4").SetValue(data.WorkProductVersion ?? string.Empty);
            sheet.Cell("B5").SetValue(reviewerLine);
            sheet.Cell("B6").SetValue(data.ReviewDate.ToString("dd-MMM-yy", CultureInfo.InvariantCulture));
            sheet.Cell("B7").SetValue(data.WorkProductSize ?? string.Empty);
            sheet.Cell("B8").SetValue(effortHours == 0 ? string.Empty : effortHours.ToString("0.##", CultureInfo.InvariantCulture));
            return;
        }

        sheet.Cell("B2").SetValue(data.GroupCode);
        sheet.Cell("B3").SetValue(data.ProjectName);
        sheet.Cell("B4").SetValue(reviewerLine);
        sheet.Cell("B5").SetValue(data.ReviewDate.ToString("dd-MMM-yy", CultureInfo.InvariantCulture));
        sheet.Cell("B6").SetValue(effortHours == 0 ? string.Empty : effortHours.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private static void FillTextBlock(IXLWorksheet sheet, string label, IEnumerable<string> lines)
    {
        var content = string.Join(Environment.NewLine, lines.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var row = FindRow(sheet, label) + 1;
        SetWrapped(sheet.Cell(row, 1), content);
    }

    private static void FillReviewThreeFinalComment(IXLWorksheet sheet, IEnumerable<string> lines)
    {
        var content = string.Join(Environment.NewLine, lines.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var row = 1; row <= lastRow; row++)
        {
            if (sheet.Cell(row, 5).GetString().Trim().Equals("Reviewer(s) Comment", StringComparison.OrdinalIgnoreCase))
            {
                SetWrapped(sheet.Cell(row + 1, 5), content);
                return;
            }
        }
    }

    private static void SetWrapped(IXLCell cell, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        cell.SetValue(value);
        cell.Style.Alignment.WrapText = true;
    }

    private static string Prefix(ReviewChecklistExportReviewer reviewer, string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"[{reviewer.ReviewerCode}] {value.Trim()}";
}

public sealed record ReviewChecklistTemplateItem(
    string ItemKey,
    string Label,
    string? Description,
    string? Priority,
    int RowNumber,
    bool IsSection,
    string? CriteriaCode);

public sealed record ReviewChecklistExportData(
    int SessionId,
    ReviewType Type,
    string GroupCode,
    string ProjectName,
    DateTime ReviewDate,
    string? WorkProductVersion,
    string? WorkProductSize,
    IReadOnlyList<ReviewChecklistExportReviewer> Reviewers);

public sealed record ReviewChecklistExportReviewer(
    int ReviewerId,
    string ReviewerCode,
    string ReviewerName,
    decimal? EffortHours,
    string? ReviewerComment,
    string? Suggestion,
    IReadOnlyList<ReviewChecklistExportItem> Items);

public sealed record ReviewChecklistExportItem(
    string ItemKey,
    ReviewChecklistAnswer? Answer,
    string? Comment);

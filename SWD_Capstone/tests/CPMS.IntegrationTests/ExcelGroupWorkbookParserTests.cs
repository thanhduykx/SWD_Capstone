using ClosedXML.Excel;
using CPMS.BLL.Academic.Import;
using CPMS.BLL.Exceptions;
using CPMS.DAL.ExternalServices;

namespace CPMS.IntegrationTests;

public sealed class ExcelGroupWorkbookParserTests
{
    private const int MaximumWorkbookBytes = 5 * 1024 * 1024;

    [Fact]
    public void Parse_ReadsOnlySelectedColumnsAndIdentifiesLeader()
    {
        using XLWorkbook workbook = CreateWorkbook();
        IReadOnlyList<ImportedGroupData> groups = Parse(workbook);

        ImportedGroupData group = Assert.Single(groups);
        Assert.Equal("Smart Home Inventory", group.ProjectTitle);
        Assert.Equal("Dương Thành Thanh Duy", group.LeaderName);
        Assert.Equal(2, group.Students.Count);
        Assert.Contains(group.Students, student =>
            student.StudentCode == "SS180745" && student.FullName == group.LeaderName);
    }

    [Fact]
    public void Parse_RejectsLeaderWhoIsNotAGroupMember()
    {
        using XLWorkbook workbook = CreateWorkbook();
        workbook.Worksheet(1).Cell(2, 11).Value = "Người Không Thuộc Nhóm";

        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            Parse(workbook));

        Assert.Contains("must match exactly one student", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsDuplicateStudentCode()
    {
        using XLWorkbook workbook = CreateWorkbook();
        workbook.Worksheet(1).Cell(3, 6).Value = "SE194673";

        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            Parse(workbook));

        Assert.Contains("duplicate MSSV", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_RejectsWorkbookLargerThanConfiguredLimit()
    {
        using var stream = new MemoryStream(new byte[MaximumWorkbookBytes + 1]);

        BusinessRuleException exception = Assert.Throws<BusinessRuleException>(() =>
            new ExcelGroupWorkbookParser().Parse(stream));

        Assert.Contains("5 MB", exception.Message, StringComparison.Ordinal);
    }

    private static XLWorkbook CreateWorkbook()
    {
        var workbook = new XLWorkbook();
        IXLWorksheet worksheet = workbook.AddWorksheet("SU26_EXE101 | Group List");
        worksheet.Cell(1, 1).InsertData(new[]
        {
            new object[]
            {
                "STT",
                "Tên Nhóm",
                "Tên dự án\n(có thể trùng tên nhóm)",
                "Mô tả ý tưởng chi tiết",
                "Họ & Tên",
                "MSSV",
                "Ngành",
                "Chuyên Ngành",
                "Email (email trên FAP của SV)",
                "Mã lớp & GV FAP EXE101",
                "Họ & Tên Leader",
                "Note"
            }
        });
        worksheet.Cell(2, 1).InsertData(new[]
        {
            new object[]
            {
                1,
                "Ignored group name",
                "Smart Home Inventory",
                "Ignored description",
                "Huỳnh Nhật Thịnh",
                "SE194673",
                "Ignored field",
                "Ignored field",
                "duongduy12317@gmail.com",
                "Ignored lecturer field",
                "Dương Thành Thanh Duy",
                "Ignored note"
            }
        });
        worksheet.Cell(3, 5).Value = "Dương Thành Thanh Duy";
        worksheet.Cell(3, 6).Value = "SS180745";
        worksheet.Cell(3, 9).Value = "victornhatthinh@gmail.com";
        return workbook;
    }

    private static IReadOnlyList<CPMS.BLL.Academic.Import.ImportedGroupData> Parse(
        XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new ExcelGroupWorkbookParser().Parse(stream);
    }
}

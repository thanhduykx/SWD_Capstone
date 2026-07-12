import json
import sys
from pathlib import Path

from docx import Document
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt, RGBColor


BLUE = "2E74B5"
DARK_BLUE = "1F4D78"
LIGHT_GRAY = "F2F4F7"
MUTED = RGBColor(90, 98, 108)


def set_font(run, size=None, bold=None, color=None):
    run.font.name = "Calibri"
    run._element.get_or_add_rPr().rFonts.set(qn("w:ascii"), "Calibri")
    run._element.get_or_add_rPr().rFonts.set(qn("w:hAnsi"), "Calibri")
    if size is not None:
        run.font.size = Pt(size)
    if bold is not None:
        run.bold = bold
    if color is not None:
        run.font.color.rgb = color


def shade(cell, fill):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = tc_pr.find(qn("w:shd")) or OxmlElement("w:shd")
    shd.set(qn("w:fill"), fill)
    if shd.getparent() is None:
        tc_pr.append(shd)


def set_cell_margin(cell, top=80, start=120, bottom=80, end=120):
    tc_pr = cell._tc.get_or_add_tcPr()
    margins = tc_pr.first_child_found_in("w:tcMar")
    if margins is None:
        margins = OxmlElement("w:tcMar")
        tc_pr.append(margins)
    for name, value in (("top", top), ("start", start), ("bottom", bottom), ("end", end)):
        node = margins.find(qn(f"w:{name}")) or OxmlElement(f"w:{name}")
        node.set(qn("w:w"), str(value))
        node.set(qn("w:type"), "dxa")
        if node.getparent() is None:
            margins.append(node)


def set_table_geometry(table, widths_dxa):
    table.autofit = False
    tbl_pr = table._tbl.tblPr
    width = tbl_pr.first_child_found_in("w:tblW")
    width.set(qn("w:w"), str(sum(widths_dxa)))
    width.set(qn("w:type"), "dxa")
    indent = tbl_pr.first_child_found_in("w:tblInd")
    if indent is None:
        indent = OxmlElement("w:tblInd")
        tbl_pr.append(indent)
    indent.set(qn("w:w"), "120")
    indent.set(qn("w:type"), "dxa")
    grid = table._tbl.tblGrid
    for child in list(grid):
        grid.remove(child)
    for value in widths_dxa:
        col = OxmlElement("w:gridCol")
        col.set(qn("w:w"), str(value))
        grid.append(col)
    for row in table.rows:
        for idx, cell in enumerate(row.cells):
            tc_w = cell._tc.get_or_add_tcPr().get_or_add_tcW()
            tc_w.set(qn("w:w"), str(widths_dxa[idx]))
            tc_w.set(qn("w:type"), "dxa")
            set_cell_margin(cell)
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER


def add_page_number(paragraph):
    paragraph.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    run = paragraph.add_run("Trang ")
    set_font(run, 9, color=MUTED)
    begin = OxmlElement("w:fldChar")
    begin.set(qn("w:fldCharType"), "begin")
    instr = OxmlElement("w:instrText")
    instr.set(qn("xml:space"), "preserve")
    instr.text = " PAGE "
    end = OxmlElement("w:fldChar")
    end.set(qn("w:fldCharType"), "end")
    run._r.extend([begin, instr, end])


def configure_styles(doc):
    normal = doc.styles["Normal"]
    normal.font.name = "Calibri"
    normal.font.size = Pt(11)
    normal.paragraph_format.space_after = Pt(6)
    normal.paragraph_format.line_spacing = 1.1
    for name, size, color, before, after in (
        ("Heading 1", 16, BLUE, 16, 8),
        ("Heading 2", 13, BLUE, 12, 6),
        ("Heading 3", 12, DARK_BLUE, 8, 4),
    ):
        style = doc.styles[name]
        style.font.name = "Calibri"
        style.font.size = Pt(size)
        style.font.bold = True
        style.font.color.rgb = RGBColor.from_string(color)
        style.paragraph_format.space_before = Pt(before)
        style.paragraph_format.space_after = Pt(after)
        style.paragraph_format.keep_with_next = True


def add_summary_table(doc, report):
    rows = [
        ("Mã bộ dữ liệu", report["marker"]),
        ("Database", report["database"]),
        ("Thời điểm UTC", report["generatedAtUtc"]),
        ("Số bảng EF Core", str(report["validation"]["tableCount"])),
        ("Kết quả transaction", report["validation"]["transaction"]),
        ("Tất cả bảng có dữ liệu", "Có" if report["validation"]["allTablesNonEmpty"] else "Không"),
    ]
    table = doc.add_table(rows=0, cols=2)
    table.style = "Table Grid"
    table.alignment = WD_TABLE_ALIGNMENT.LEFT
    for label, value in rows:
        cells = table.add_row().cells
        cells[0].text, cells[1].text = label, value
        shade(cells[0], LIGHT_GRAY)
        for run in cells[0].paragraphs[0].runs:
            set_font(run, 10, bold=True)
        for run in cells[1].paragraphs[0].runs:
            set_font(run, 10)
    set_table_geometry(table, [2700, 6660])


def create_report(input_path, output_path):
    report = json.loads(Path(input_path).read_text(encoding="utf-8"))
    doc = Document()
    section = doc.sections[0]
    section.page_width, section.page_height = Inches(8.5), Inches(11)
    section.top_margin = section.right_margin = section.bottom_margin = section.left_margin = Inches(1)
    section.header_distance = section.footer_distance = Inches(0.492)
    configure_styles(doc)

    header = section.header.paragraphs[0]
    header.text = "CPMS | Báo cáo dữ liệu demo"
    for run in header.runs:
        set_font(run, 9, bold=True, color=MUTED)
    add_page_number(section.footer.paragraphs[0])

    title = doc.add_paragraph()
    title.paragraph_format.space_after = Pt(4)
    run = title.add_run("BÁO CÁO DỮ LIỆU DEMO CPMS")
    set_font(run, 23, bold=True, color=RGBColor(0, 0, 0))
    subtitle = doc.add_paragraph()
    subtitle.paragraph_format.space_after = Pt(16)
    run = subtitle.add_run("Bộ dữ liệu DEMO_2026 trên PostgreSQL/Supabase")
    set_font(run, 14, color=RGBColor(55, 55, 55))

    doc.add_heading("1. Kết quả thực hiện", level=1)
    doc.add_paragraph("Bộ dữ liệu demo đã được nạp trong một transaction và được xác minh bằng lần chạy thứ hai. Công cụ không xóa hoặc cập nhật dữ liệu nghiệp vụ đã có; khóa idempotency là DEMO_2026.")
    add_summary_table(doc, report)

    doc.add_heading("2. Số liệu theo bảng", level=1)
    doc.add_paragraph("Bảng dưới đây phản ánh số liệu ngay sau lần nạp đầu tiên. Cột 'Thêm mới' là số bản ghi được tạo bởi lần chạy này; tổng số có thể bao gồm dữ liệu đã tồn tại trên hệ thống.")
    table = doc.add_table(rows=1, cols=4)
    table.style = "Table Grid"
    table.alignment = WD_TABLE_ALIGNMENT.LEFT
    headers = ("Tên bảng", "Trước", "Sau", "Thêm mới")
    for index, text in enumerate(headers):
        table.rows[0].cells[index].text = text
        shade(table.rows[0].cells[index], LIGHT_GRAY)
    for item in report["tables"]:
        cells = table.add_row().cells
        values = (item["name"], item["before"], item["after"], item["added"])
        for index, value in enumerate(values):
            cells[index].text = str(value)
            cells[index].paragraphs[0].alignment = WD_ALIGN_PARAGRAPH.LEFT if index == 0 else WD_ALIGN_PARAGRAPH.CENTER
    for row_index, row in enumerate(table.rows):
        for cell in row.cells:
            for paragraph in cell.paragraphs:
                paragraph.paragraph_format.space_after = Pt(0)
                for run in paragraph.runs:
                    set_font(run, 9, bold=row_index == 0)
    set_table_geometry(table, [5040, 1440, 1440, 1440])
    table.rows[0]._tr.get_or_add_trPr().append(OxmlElement("w:tblHeader"))

    doc.add_heading("3. Tài khoản kiểm thử", level=1)
    doc.add_paragraph("Các tài khoản sau chỉ dành cho demo. Tất cả dùng chung mật khẩu Demo@2026. Tài liệu không chứa password hash, refresh token hoặc chuỗi kết nối.")
    table = doc.add_table(rows=1, cols=3)
    table.style = "Table Grid"
    for index, text in enumerate(("Username", "Email", "Vai trò")):
        table.rows[0].cells[index].text = text
        shade(table.rows[0].cells[index], LIGHT_GRAY)
    for account in report["demoAccounts"]:
        cells = table.add_row().cells
        cells[0].text, cells[1].text, cells[2].text = account["Username"], account["Email"], account["role"]
    for row_index, row in enumerate(table.rows):
        for cell in row.cells:
            for paragraph in cell.paragraphs:
                paragraph.paragraph_format.space_after = Pt(0)
                for run in paragraph.runs:
                    set_font(run, 9, bold=row_index == 0)
    set_table_geometry(table, [2200, 4300, 2860])

    doc.add_heading("4. Luồng nghiệp vụ được mô phỏng", level=1)
    flows = [
        "Tài khoản và hồ sơ: quản trị, phòng đào tạo, 3 giảng viên và 6 sinh viên.",
        "Học thuật: học kỳ, đề tài, 2 nhóm, syllabus, CLO và từ khóa đánh giá.",
        "Đánh giá tài liệu: báo cáo cuối kỳ, evaluation report/detail và thảo luận inline.",
        "Review: lịch rảnh, đăng ký slot, lịch công bố, phiên review, checklist, điểm danh và nhận xét.",
        "Bảo vệ: hội đồng, vòng bảo vệ, phiên bảo vệ, evidence tham chiếu, điểm và lịch sử nộp điểm.",
        "Vận hành: notification, email log ở trạng thái không gửi, audit log và refresh token đã thu hồi/hết hạn.",
    ]
    for text in flows:
        doc.add_paragraph(text, style="List Bullet")

    doc.add_heading("5. Kiểm soát an toàn và xác minh", level=1)
    doc.add_paragraph("Database tự kiểm tra khóa ngoại và constraint khi transaction commit. Lần chạy thứ hai trả về trạng thái verified-existing và không thêm bản ghi. Các đường dẫn tài liệu/evidence là tham chiếu demo; không khẳng định tệp vật lý tồn tại. Seeder không gửi email, không phát message và không kích hoạt background job.")

    output = Path(output_path)
    output.parent.mkdir(parents=True, exist_ok=True)
    doc.save(output)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("Usage: create_report.py <seed-report.json> <output.docx>")
    create_report(sys.argv[1], sys.argv[2])

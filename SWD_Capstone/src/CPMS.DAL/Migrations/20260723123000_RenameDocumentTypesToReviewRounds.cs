using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CPMS.DAL.Migrations;

[DbContext(typeof(CpmsDbContext))]
[Migration("20260723123000_RenameDocumentTypesToReviewRounds")]
public partial class RenameDocumentTypesToReviewRounds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE documents
            SET doc_type = CASE doc_type
                WHEN 'Proposal' THEN 'Review1'
                WHEN 'Progress' THEN 'Review2'
                WHEN 'Final' THEN 'Review3'
                ELSE doc_type
            END
            WHERE doc_type IN ('Proposal', 'Progress', 'Final');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE documents
            SET doc_type = CASE doc_type
                WHEN 'Review1' THEN 'Proposal'
                WHEN 'Review2' THEN 'Progress'
                WHEN 'Review3' THEN 'Final'
                ELSE doc_type
            END
            WHERE doc_type IN ('Review1', 'Review2', 'Review3');
            """);
    }
}

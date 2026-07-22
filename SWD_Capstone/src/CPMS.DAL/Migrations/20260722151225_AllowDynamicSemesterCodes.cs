using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CPMS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AllowDynamicSemesterCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_semesters_supported_codes",
                table: "semesters");

            migrationBuilder.AddCheckConstraint(
                name: "ck_semesters_supported_codes",
                table: "semesters",
                sql: "upper(\"code\") ~ '^(SP|SU|FA)[0-9]{2}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_semesters_supported_codes",
                table: "semesters");

            migrationBuilder.AddCheckConstraint(
                name: "ck_semesters_supported_codes",
                table: "semesters",
                sql: "upper(\"code\") IN ('SP26', 'SU26', 'FA26')");
        }
    }
}

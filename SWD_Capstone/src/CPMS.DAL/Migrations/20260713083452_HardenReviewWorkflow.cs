using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CPMS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class HardenReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_students_group_id",
                table: "students");

            migrationBuilder.Sql(
                """
                WITH duplicate_leaders AS (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY group_id ORDER BY id) AS leader_rank
                    FROM students
                    WHERE is_leader = TRUE AND group_id IS NOT NULL
                )
                UPDATE students AS student
                SET is_leader = FALSE
                FROM duplicate_leaders
                WHERE student.id = duplicate_leaders.id AND duplicate_leaders.leader_rank > 1;

                UPDATE review_comments SET content = LEFT(content, 2000) WHERE LENGTH(content) > 2000;
                UPDATE review_attendances SET note = LEFT(note, 1000) WHERE LENGTH(note) > 1000;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "content",
                table: "review_comments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "note",
                table: "review_attendances",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_students_group_id",
                table: "students",
                column: "group_id",
                unique: true,
                filter: "\"is_leader\" = TRUE");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_students_group_id",
                table: "students");

            migrationBuilder.AlterColumn<string>(
                name: "content",
                table: "review_comments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "note",
                table: "review_attendances",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_students_group_id",
                table: "students",
                column: "group_id");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CPMS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class SupportFlexibleReviewPanelsAndMultipleComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "review_session_reviewers",
                columns: table => new
                {
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    lecturer_id = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_session_reviewers", x => new { x.session_id, x.lecturer_id });
                    table.CheckConstraint("ck_review_session_reviewers_position", "position > 0");
                    table.ForeignKey(
                        name: "fk_review_session_reviewers_lecturers_lecturer_id",
                        column: x => x.lecturer_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_session_reviewers_review_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "review_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_submission_comments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    submission_id = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_submission_comments", x => x.id);
                    table.CheckConstraint("ck_review_submission_comments_position", "position > 0");
                    table.ForeignKey(
                        name: "fk_review_submission_comments_review_checklist_submissions_sub",
                        column: x => x.submission_id,
                        principalTable: "review_checklist_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_review_session_reviewers_lecturer_id",
                table: "review_session_reviewers",
                column: "lecturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_session_reviewers_session_id_position",
                table: "review_session_reviewers",
                columns: new[] { "session_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_review_submission_comments_submission_id_position",
                table: "review_submission_comments",
                columns: new[] { "submission_id", "position" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO review_session_reviewers (session_id, lecturer_id, position)
                SELECT id, reviewer1id, 1 FROM review_sessions WHERE reviewer1id IS NOT NULL
                ON CONFLICT (session_id, lecturer_id) DO NOTHING;
                INSERT INTO review_session_reviewers (session_id, lecturer_id, position)
                SELECT id, reviewer2id, 2 FROM review_sessions WHERE reviewer2id IS NOT NULL
                ON CONFLICT (session_id, lecturer_id) DO NOTHING;
                INSERT INTO review_submission_comments
                    (submission_id, content, position, created_at, updated_at)
                SELECT id, reviewer_comment, 1, last_saved_at, last_saved_at
                FROM review_checklist_submissions
                WHERE reviewer_comment IS NOT NULL AND btrim(reviewer_comment) <> '';
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_session_reviewers");

            migrationBuilder.DropTable(
                name: "review_submission_comments");

        }
    }
}

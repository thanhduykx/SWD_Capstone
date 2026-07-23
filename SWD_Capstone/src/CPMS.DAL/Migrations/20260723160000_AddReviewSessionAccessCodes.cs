using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CPMS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewSessionAccessCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "access_code_generated_at",
                table: "review_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "access_code_generated_by_user_id",
                table: "review_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "access_code_hash",
                table: "review_sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "access_code_version",
                table: "review_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "review_session_access_grants",
                columns: table => new
                {
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    lecturer_id = table.Column<int>(type: "integer", nullable: false),
                    code_version = table.Column<int>(type: "integer", nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_session_access_grants", x => new { x.session_id, x.lecturer_id });
                    table.ForeignKey(
                        name: "fk_review_session_access_grants_lecturers_lecturer_id",
                        column: x => x.lecturer_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_session_access_grants_review_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "review_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_access_code_generated_by_user_id",
                table: "review_sessions",
                column: "access_code_generated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_session_access_grants_lecturer_id",
                table: "review_session_access_grants",
                column: "lecturer_id");

            migrationBuilder.AddForeignKey(
                name: "fk_review_sessions_users_access_code_generated_by_user_id",
                table: "review_sessions",
                column: "access_code_generated_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_review_sessions_users_access_code_generated_by_user_id",
                table: "review_sessions");

            migrationBuilder.DropTable(
                name: "review_session_access_grants");

            migrationBuilder.DropIndex(
                name: "ix_review_sessions_access_code_generated_by_user_id",
                table: "review_sessions");

            migrationBuilder.DropColumn(
                name: "access_code_generated_at",
                table: "review_sessions");

            migrationBuilder.DropColumn(
                name: "access_code_generated_by_user_id",
                table: "review_sessions");

            migrationBuilder.DropColumn(
                name: "access_code_hash",
                table: "review_sessions");

            migrationBuilder.DropColumn(
                name: "access_code_version",
                table: "review_sessions");
        }
    }
}

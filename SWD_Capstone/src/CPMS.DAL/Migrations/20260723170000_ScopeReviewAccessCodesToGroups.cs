using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CPMS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class ScopeReviewAccessCodesToGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Session-level grants cannot be mapped safely to one group. Requiring
            // lecturers to enter newly generated group codes is the secure migration.
            migrationBuilder.Sql("DELETE FROM review_session_access_grants;");

            migrationBuilder.DropPrimaryKey(
                name: "pk_review_session_access_grants",
                table: "review_session_access_grants");

            migrationBuilder.AddColumn<int>(
                name: "group_id",
                table: "review_session_access_grants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "access_code_generated_at",
                table: "group_review_slots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "access_code_generated_by_user_id",
                table: "group_review_slots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "access_code_hash",
                table: "group_review_slots",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "access_code_version",
                table: "group_review_slots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "pk_review_session_access_grants",
                table: "review_session_access_grants",
                columns: new[] { "session_id", "group_id", "lecturer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_review_session_access_grants_group_id",
                table: "review_session_access_grants",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_review_slots_access_code_generated_by_user_id",
                table: "group_review_slots",
                column: "access_code_generated_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_group_review_slots_users_access_code_generated_by_user_id",
                table: "group_review_slots",
                column: "access_code_generated_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_review_session_access_grants_capstone_groups_group_id",
                table: "review_session_access_grants",
                column: "group_id",
                principalTable: "capstone_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM review_session_access_grants;");

            migrationBuilder.DropForeignKey(
                name: "fk_group_review_slots_users_access_code_generated_by_user_id",
                table: "group_review_slots");

            migrationBuilder.DropForeignKey(
                name: "fk_review_session_access_grants_capstone_groups_group_id",
                table: "review_session_access_grants");

            migrationBuilder.DropPrimaryKey(
                name: "pk_review_session_access_grants",
                table: "review_session_access_grants");

            migrationBuilder.DropIndex(
                name: "ix_review_session_access_grants_group_id",
                table: "review_session_access_grants");

            migrationBuilder.DropIndex(
                name: "ix_group_review_slots_access_code_generated_by_user_id",
                table: "group_review_slots");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "review_session_access_grants");

            migrationBuilder.DropColumn(
                name: "access_code_generated_at",
                table: "group_review_slots");

            migrationBuilder.DropColumn(
                name: "access_code_generated_by_user_id",
                table: "group_review_slots");

            migrationBuilder.DropColumn(
                name: "access_code_hash",
                table: "group_review_slots");

            migrationBuilder.DropColumn(
                name: "access_code_version",
                table: "group_review_slots");

            migrationBuilder.AddPrimaryKey(
                name: "pk_review_session_access_grants",
                table: "review_session_access_grants",
                columns: new[] { "session_id", "lecturer_id" });
        }
    }
}

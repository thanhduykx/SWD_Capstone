using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddDefenseRoundsAndProjectScopedSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_defense_sessions_council_id",
                table: "defense_sessions");

            migrationBuilder.AddColumn<int>(
                name: "assigned_by_training_department_id",
                table: "defense_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "defense_sessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "defense_round_id",
                table: "defense_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "group_id",
                table: "defense_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "room",
                table: "defense_sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "slot",
                table: "defense_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "managed_by_training_department_id",
                table: "councils",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "defense_rounds",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    semester_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_by_training_department_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_defense_rounds", x => x.id);
                    table.ForeignKey(
                        name: "fk_defense_rounds_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_defense_rounds_training_departments_created_by_training_dep",
                        column: x => x.created_by_training_department_id,
                        principalTable: "training_departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_defense_sessions_assigned_by_training_department_id",
                table: "defense_sessions",
                column: "assigned_by_training_department_id");

            migrationBuilder.CreateIndex(
                name: "ix_defense_sessions_code_defense_round_id",
                table: "defense_sessions",
                columns: new[] { "code", "defense_round_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_defense_sessions_council_id_session_date_slot",
                table: "defense_sessions",
                columns: new[] { "council_id", "session_date", "slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_defense_sessions_defense_round_id_group_id",
                table: "defense_sessions",
                columns: new[] { "defense_round_id", "group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_defense_sessions_group_id",
                table: "defense_sessions",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_defense_sessions_session_date_room_slot",
                table: "defense_sessions",
                columns: new[] { "session_date", "room", "slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_councils_managed_by_training_department_id",
                table: "councils",
                column: "managed_by_training_department_id");

            migrationBuilder.CreateIndex(
                name: "ix_defense_rounds_code_semester_id",
                table: "defense_rounds",
                columns: new[] { "code", "semester_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_defense_rounds_created_by_training_department_id",
                table: "defense_rounds",
                column: "created_by_training_department_id");

            migrationBuilder.CreateIndex(
                name: "ix_defense_rounds_semester_id_type_status",
                table: "defense_rounds",
                columns: new[] { "semester_id", "type", "status" });

            migrationBuilder.AddForeignKey(
                name: "fk_councils_training_departments_managed_by_training_departmen",
                table: "councils",
                column: "managed_by_training_department_id",
                principalTable: "training_departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_defense_sessions_capstone_groups_group_id",
                table: "defense_sessions",
                column: "group_id",
                principalTable: "capstone_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_defense_sessions_defense_rounds_defense_round_id",
                table: "defense_sessions",
                column: "defense_round_id",
                principalTable: "defense_rounds",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_defense_sessions_training_departments_assigned_by_training_",
                table: "defense_sessions",
                column: "assigned_by_training_department_id",
                principalTable: "training_departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_councils_training_departments_managed_by_training_departmen",
                table: "councils");

            migrationBuilder.DropForeignKey(
                name: "fk_defense_sessions_capstone_groups_group_id",
                table: "defense_sessions");

            migrationBuilder.DropForeignKey(
                name: "fk_defense_sessions_defense_rounds_defense_round_id",
                table: "defense_sessions");

            migrationBuilder.DropForeignKey(
                name: "fk_defense_sessions_training_departments_assigned_by_training_",
                table: "defense_sessions");

            migrationBuilder.DropTable(
                name: "defense_rounds");

            migrationBuilder.DropIndex(
                name: "ix_defense_sessions_assigned_by_training_department_id",
                table: "defense_sessions");

            migrationBuilder.DropIndex(
                name: "ix_defense_sessions_code_defense_round_id",
                table: "defense_sessions");

            migrationBuilder.DropIndex(
                name: "ix_defense_sessions_council_id_session_date_slot",
                table: "defense_sessions");

            migrationBuilder.DropIndex(
                name: "ix_defense_sessions_defense_round_id_group_id",
                table: "defense_sessions");

            migrationBuilder.DropIndex(
                name: "ix_defense_sessions_group_id",
                table: "defense_sessions");

            migrationBuilder.DropIndex(
                name: "ix_defense_sessions_session_date_room_slot",
                table: "defense_sessions");

            migrationBuilder.DropIndex(
                name: "ix_councils_managed_by_training_department_id",
                table: "councils");

            migrationBuilder.DropColumn(
                name: "assigned_by_training_department_id",
                table: "defense_sessions");

            migrationBuilder.DropColumn(
                name: "code",
                table: "defense_sessions");

            migrationBuilder.DropColumn(
                name: "defense_round_id",
                table: "defense_sessions");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "defense_sessions");

            migrationBuilder.DropColumn(
                name: "room",
                table: "defense_sessions");

            migrationBuilder.DropColumn(
                name: "slot",
                table: "defense_sessions");

            migrationBuilder.DropColumn(
                name: "managed_by_training_department_id",
                table: "councils");

            migrationBuilder.CreateIndex(
                name: "ix_defense_sessions_council_id",
                table: "defense_sessions",
                column: "council_id");
        }
    }
}

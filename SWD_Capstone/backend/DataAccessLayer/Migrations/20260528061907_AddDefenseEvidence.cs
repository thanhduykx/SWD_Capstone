using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddDefenseEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "defense_evidences",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    defense_session_id = table.Column<int>(type: "integer", nullable: false),
                    captured_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    captured_by_lecturer_id = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_defense_evidences", x => x.id);
                    table.ForeignKey(
                        name: "fk_defense_evidences_defense_sessions_defense_session_id",
                        column: x => x.defense_session_id,
                        principalTable: "defense_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_defense_evidences_lecturers_captured_by_lecturer_id",
                        column: x => x.captured_by_lecturer_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_defense_evidences_users_captured_by_user_id",
                        column: x => x.captured_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_defense_evidences_captured_by_lecturer_id",
                table: "defense_evidences",
                column: "captured_by_lecturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_defense_evidences_captured_by_user_id",
                table: "defense_evidences",
                column: "captured_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_defense_evidences_defense_session_id",
                table: "defense_evidences",
                column: "defense_session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "defense_evidences");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CPMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDefenseScoringRealtimeAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "score_submission_histories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    score_id = table.Column<int>(type: "integer", nullable: true),
                    defense_session_id = table.Column<int>(type: "integer", nullable: false),
                    scorer_id = table.Column<int>(type: "integer", nullable: false),
                    submitted_by_user_id = table.Column<int>(type: "integer", nullable: false),
                    student_id = table.Column<int>(type: "integer", nullable: false),
                    score_type = table.Column<string>(type: "text", nullable: false),
                    old_score_value = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    new_score_value = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    is_trusted = table.Column<bool>(type: "boolean", nullable: false),
                    trust_reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_score_submission_histories", x => x.id);
                    table.ForeignKey(
                        name: "fk_score_submission_histories_defense_sessions_defense_session",
                        column: x => x.defense_session_id,
                        principalTable: "defense_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_score_submission_histories_lecturers_scorer_id",
                        column: x => x.scorer_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_score_submission_histories_scores_score_id",
                        column: x => x.score_id,
                        principalTable: "scores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_score_submission_histories_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_score_submission_histories_users_submitted_by_user_id",
                        column: x => x.submitted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_score_submission_histories_defense_session_id",
                table: "score_submission_histories",
                column: "defense_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_score_submission_histories_score_id",
                table: "score_submission_histories",
                column: "score_id");

            migrationBuilder.CreateIndex(
                name: "ix_score_submission_histories_scorer_id",
                table: "score_submission_histories",
                column: "scorer_id");

            migrationBuilder.CreateIndex(
                name: "ix_score_submission_histories_student_id",
                table: "score_submission_histories",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_score_submission_histories_submitted_by_user_id",
                table: "score_submission_histories",
                column: "submitted_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "score_submission_histories");
        }
    }
}

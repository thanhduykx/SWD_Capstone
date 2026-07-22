using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CPMS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedEvaluationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_details");

            migrationBuilder.DropTable(
                name: "rule_keywords");

            migrationBuilder.DropTable(
                name: "evaluation_reports");

            migrationBuilder.DropTable(
                name: "clos");

            migrationBuilder.DropTable(
                name: "evaluation_panels");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    syllabus_id = table.Column<int>(type: "integer", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clos", x => x.id);
                    table.ForeignKey(
                        name: "fk_clos_syllabuses_syllabus_id",
                        column: x => x.syllabus_id,
                        principalTable: "syllabuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_panels",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    department = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_panels", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluation_panels_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rule_keywords",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clo_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_admin_id = table.Column<int>(type: "integer", nullable: false),
                    created_by_id = table.Column<int>(type: "integer", nullable: true),
                    keyword_text = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    weight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rule_keywords", x => x.id);
                    table.ForeignKey(
                        name: "fk_rule_keywords_clos_clo_id",
                        column: x => x.clo_id,
                        principalTable: "clos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rule_keywords_system_administrators_created_by_admin_id",
                        column: x => x.created_by_admin_id,
                        principalTable: "system_administrators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_reports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_id = table.Column<int>(type: "integer", nullable: false),
                    evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    gap_analysis_summary = table.Column<string>(type: "text", nullable: true),
                    match_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    overall_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    reviewed_by_id = table.Column<int>(type: "integer", nullable: false),
                    trigger_type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluation_reports_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evaluation_reports_evaluation_panels_reviewed_by_id",
                        column: x => x.reviewed_by_id,
                        principalTable: "evaluation_panels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_details",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    clo_id = table.Column<int>(type: "integer", nullable: false),
                    feedback = table.Column<string>(type: "text", nullable: true),
                    match_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    max_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    missing_evidence = table.Column<string>(type: "text", nullable: true),
                    report_id = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluation_details", x => x.id);
                    table.ForeignKey(
                        name: "fk_evaluation_details_clos_clo_id",
                        column: x => x.clo_id,
                        principalTable: "clos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evaluation_details_evaluation_reports_report_id",
                        column: x => x.report_id,
                        principalTable: "evaluation_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clos_syllabus_id_code",
                table: "clos",
                columns: new[] { "syllabus_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_details_clo_id",
                table: "evaluation_details",
                column: "clo_id");

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_details_report_id_clo_id",
                table: "evaluation_details",
                columns: new[] { "report_id", "clo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_panels_user_id",
                table: "evaluation_panels",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_reports_document_id",
                table: "evaluation_reports",
                column: "document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_reports_reviewed_by_id",
                table: "evaluation_reports",
                column: "reviewed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_rule_keywords_clo_id",
                table: "rule_keywords",
                column: "clo_id");

            migrationBuilder.CreateIndex(
                name: "ix_rule_keywords_created_by_admin_id",
                table: "rule_keywords",
                column: "created_by_admin_id");
        }
    }
}

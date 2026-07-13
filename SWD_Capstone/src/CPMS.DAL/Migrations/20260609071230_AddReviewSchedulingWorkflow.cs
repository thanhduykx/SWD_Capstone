using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewSchedulingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_review_sessions_semester_id",
                table: "review_sessions");

            migrationBuilder.AddColumn<DateTime>(
                name: "published_at",
                table: "review_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "published_by_training_department_id",
                table: "review_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "review_sessions",
                type: "text",
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.CreateTable(
                name: "review_availabilities",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    semester_id = table.Column<int>(type: "integer", nullable: false),
                    lecturer_id = table.Column<int>(type: "integer", nullable: false),
                    week_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_availabilities", x => x.id);
                    table.CheckConstraint("ck_review_availabilities_day_of_week", "day_of_week BETWEEN 1 AND 7");
                    table.CheckConstraint("ck_review_availabilities_slot", "slot BETWEEN 1 AND 8");
                    table.ForeignKey(
                        name: "fk_review_availabilities_lecturers_lecturer_id",
                        column: x => x.lecturer_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_availabilities_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_checklist_submissions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    reviewer_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Draft"),
                    work_product_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    work_product_size = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    effort_hours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    reviewer_comment = table.Column<string>(type: "text", nullable: true),
                    suggestion = table.Column<string>(type: "text", nullable: true),
                    result_text = table.Column<string>(type: "text", nullable: true),
                    last_saved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_checklist_submissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_checklist_submissions_capstone_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "capstone_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_checklist_submissions_lecturers_reviewer_id",
                        column: x => x.reviewer_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_checklist_submissions_review_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "review_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_schedule_publications",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    semester_id = table.Column<int>(type: "integer", nullable: false),
                    review_type = table.Column<string>(type: "text", nullable: false),
                    week_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    published_by_training_department_id = table.Column<int>(type: "integer", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_schedule_publications", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_schedule_publications_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_schedule_publications_training_departments_published",
                        column: x => x.published_by_training_department_id,
                        principalTable: "training_departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_checklist_item_responses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    submission_id = table.Column<int>(type: "integer", nullable: false),
                    item_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    answer = table.Column<string>(type: "text", nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_checklist_item_responses", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_checklist_item_responses_review_checklist_submission",
                        column: x => x.submission_id,
                        principalTable: "review_checklist_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "email_delivery_logs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    publication_id = table.Column<int>(type: "integer", nullable: true),
                    recipient_user_id = table.Column<int>(type: "integer", nullable: true),
                    recipient_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_delivery_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_email_delivery_logs_review_schedule_publications_publicatio",
                        column: x => x.publication_id,
                        principalTable: "review_schedule_publications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_email_delivery_logs_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_published_by_training_department_id",
                table: "review_sessions",
                column: "published_by_training_department_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_semester_id_type_session_date_slot",
                table: "review_sessions",
                columns: new[] { "semester_id", "type", "session_date", "slot" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_sessions_slot",
                table: "review_sessions",
                sql: "slot BETWEEN 1 AND 8");

            migrationBuilder.CreateIndex(
                name: "ix_email_delivery_logs_publication_id",
                table: "email_delivery_logs",
                column: "publication_id");

            migrationBuilder.CreateIndex(
                name: "ix_email_delivery_logs_recipient_email",
                table: "email_delivery_logs",
                column: "recipient_email");

            migrationBuilder.CreateIndex(
                name: "ix_email_delivery_logs_recipient_user_id",
                table: "email_delivery_logs",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_availabilities_lecturer_id",
                table: "review_availabilities",
                column: "lecturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_availabilities_semester_id_lecturer_id_week_start_da",
                table: "review_availabilities",
                columns: new[] { "semester_id", "lecturer_id", "week_start_date", "day_of_week", "slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_checklist_item_responses_submission_id_item_key",
                table: "review_checklist_item_responses",
                columns: new[] { "submission_id", "item_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_checklist_submissions_group_id_type",
                table: "review_checklist_submissions",
                columns: new[] { "group_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_review_checklist_submissions_reviewer_id",
                table: "review_checklist_submissions",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_checklist_submissions_session_id_reviewer_id",
                table: "review_checklist_submissions",
                columns: new[] { "session_id", "reviewer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_schedule_publications_published_by_training_departme",
                table: "review_schedule_publications",
                column: "published_by_training_department_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_schedule_publications_semester_id_review_type_week_s",
                table: "review_schedule_publications",
                columns: new[] { "semester_id", "review_type", "week_start_date" });

            migrationBuilder.AddForeignKey(
                name: "fk_review_sessions_training_departments_published_by_training_",
                table: "review_sessions",
                column: "published_by_training_department_id",
                principalTable: "training_departments",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_review_sessions_training_departments_published_by_training_",
                table: "review_sessions");

            migrationBuilder.DropTable(
                name: "email_delivery_logs");

            migrationBuilder.DropTable(
                name: "review_availabilities");

            migrationBuilder.DropTable(
                name: "review_checklist_item_responses");

            migrationBuilder.DropTable(
                name: "review_schedule_publications");

            migrationBuilder.DropTable(
                name: "review_checklist_submissions");

            migrationBuilder.DropIndex(
                name: "ix_review_sessions_published_by_training_department_id",
                table: "review_sessions");

            migrationBuilder.DropIndex(
                name: "ix_review_sessions_semester_id_type_session_date_slot",
                table: "review_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_sessions_slot",
                table: "review_sessions");

            migrationBuilder.DropColumn(
                name: "published_at",
                table: "review_sessions");

            migrationBuilder.DropColumn(
                name: "published_by_training_department_id",
                table: "review_sessions");

            migrationBuilder.DropColumn(
                name: "status",
                table: "review_sessions");

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_semester_id",
                table: "review_sessions",
                column: "semester_id");
        }
    }
}

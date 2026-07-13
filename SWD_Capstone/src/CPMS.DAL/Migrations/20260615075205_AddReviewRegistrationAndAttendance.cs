using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewRegistrationAndAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "review_attendances",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    student_id = table.Column<int>(type: "integer", nullable: false),
                    is_present = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    marked_by_lecturer_id = table.Column<int>(type: "integer", nullable: false),
                    marked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_attendances", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_attendances_lecturers_marked_by_lecturer_id",
                        column: x => x.marked_by_lecturer_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_attendances_review_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "review_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_attendances_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_comments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    author_user_id = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_comments_capstone_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "capstone_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_comments_review_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "review_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_review_comments_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_rounds",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    semester_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    week_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    week_end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Draft"),
                    created_by_moderator_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_rounds", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_rounds_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_rounds_training_departments_created_by_moderator_id",
                        column: x => x.created_by_moderator_id,
                        principalTable: "training_departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_slot_registrations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    review_round_id = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    registered_by_student_id = table.Column<int>(type: "integer", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_slot_registrations", x => x.id);
                    table.CheckConstraint("ck_student_slot_registrations_day_of_week", "day_of_week BETWEEN 1 AND 6");
                    table.CheckConstraint("ck_student_slot_registrations_slot", "slot BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_student_slot_registrations_capstone_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "capstone_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_slot_registrations_review_rounds_review_round_id",
                        column: x => x.review_round_id,
                        principalTable: "review_rounds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_student_slot_registrations_students_registered_by_student_id",
                        column: x => x.registered_by_student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_review_attendances_marked_by_lecturer_id",
                table: "review_attendances",
                column: "marked_by_lecturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_attendances_session_id_student_id",
                table: "review_attendances",
                columns: new[] { "session_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_attendances_student_id",
                table: "review_attendances",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_comments_author_user_id",
                table: "review_comments",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_comments_group_id",
                table: "review_comments",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_comments_session_id_group_id",
                table: "review_comments",
                columns: new[] { "session_id", "group_id" });

            migrationBuilder.CreateIndex(
                name: "ix_review_rounds_created_by_moderator_id",
                table: "review_rounds",
                column: "created_by_moderator_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_rounds_semester_id_type_week_start_date",
                table: "review_rounds",
                columns: new[] { "semester_id", "type", "week_start_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_slot_registrations_group_id",
                table: "student_slot_registrations",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_slot_registrations_registered_by_student_id",
                table: "student_slot_registrations",
                column: "registered_by_student_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_slot_registrations_review_round_id_day_of_week_slot",
                table: "student_slot_registrations",
                columns: new[] { "review_round_id", "day_of_week", "slot" });

            migrationBuilder.CreateIndex(
                name: "ix_student_slot_registrations_review_round_id_group_id",
                table: "student_slot_registrations",
                columns: new[] { "review_round_id", "group_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_attendances");

            migrationBuilder.DropTable(
                name: "review_comments");

            migrationBuilder.DropTable(
                name: "student_slot_registrations");

            migrationBuilder.DropTable(
                name: "review_rounds");
        }
    }
}

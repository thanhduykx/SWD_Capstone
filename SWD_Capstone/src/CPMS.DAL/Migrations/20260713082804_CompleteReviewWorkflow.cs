using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CPMS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class CompleteReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_student_slot_registrations_review_round_id_group_id",
                table: "student_slot_registrations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_sessions_slot",
                table: "review_sessions");

            migrationBuilder.DropIndex(
                name: "ix_review_schedule_publications_semester_id_review_type_week_s",
                table: "review_schedule_publications");

            migrationBuilder.DropIndex(
                name: "ix_review_checklist_submissions_session_id_reviewer_id",
                table: "review_checklist_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_availabilities_day_of_week",
                table: "review_availabilities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_availabilities_slot",
                table: "review_availabilities");

            migrationBuilder.AddColumn<DateTime>(
                name: "completed_at",
                table: "group_review_slots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "completed_by_lecturer_id",
                table: "group_review_slots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "group_review_slots",
                type: "text",
                nullable: false,
                defaultValue: "Scheduled");

            migrationBuilder.Sql(
                """
                DELETE FROM review_availabilities
                WHERE day_of_week NOT BETWEEN 1 AND 6 OR slot NOT BETWEEN 1 AND 5;

                UPDATE review_sessions
                SET status = 'Cancelled'
                WHERE day_of_week NOT BETWEEN 1 AND 6 OR slot NOT BETWEEN 1 AND 5;

                WITH ranked AS (
                    SELECT id, ROW_NUMBER() OVER (PARTITION BY session_id ORDER BY group_position, id) AS new_position
                    FROM group_review_slots
                )
                UPDATE group_review_slots AS target
                SET group_position = ranked.new_position
                FROM ranked
                WHERE target.id = ranked.id;

                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM group_review_slots WHERE group_position > 3) THEN
                        RAISE EXCEPTION 'Cannot enforce the three-groups-per-session rule: legacy review sessions with more than three groups require manual remediation.';
                    END IF;
                END $$;

                DELETE FROM review_schedule_publications AS duplicate
                USING review_schedule_publications AS keeper
                WHERE duplicate.semester_id = keeper.semester_id
                  AND duplicate.review_type = keeper.review_type
                  AND duplicate.week_start_date = keeper.week_start_date
                  AND duplicate.id > keeper.id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_student_slot_registrations_review_round_id_group_id_day_of_",
                table: "student_slot_registrations",
                columns: new[] { "review_round_id", "group_id", "day_of_week", "slot" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_sessions_day_of_week",
                table: "review_sessions",
                sql: "status = 'Cancelled' OR day_of_week BETWEEN 1 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_sessions_slot",
                table: "review_sessions",
                sql: "status = 'Cancelled' OR slot BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "ix_review_schedule_publications_semester_id_review_type_week_s",
                table: "review_schedule_publications",
                columns: new[] { "semester_id", "review_type", "week_start_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_checklist_submissions_session_id_group_id_reviewer_id",
                table: "review_checklist_submissions",
                columns: new[] { "session_id", "group_id", "reviewer_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_availabilities_day_of_week",
                table: "review_availabilities",
                sql: "day_of_week BETWEEN 1 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_availabilities_slot",
                table: "review_availabilities",
                sql: "slot BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "ix_group_review_slots_completed_by_lecturer_id",
                table: "group_review_slots",
                column: "completed_by_lecturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_review_slots_session_id_group_position",
                table: "group_review_slots",
                columns: new[] { "session_id", "group_position" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_group_review_slots_position",
                table: "group_review_slots",
                sql: "group_position BETWEEN 1 AND 3");

            migrationBuilder.AddForeignKey(
                name: "fk_group_review_slots_lecturers_completed_by_lecturer_id",
                table: "group_review_slots",
                column: "completed_by_lecturer_id",
                principalTable: "lecturers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_group_review_slots_lecturers_completed_by_lecturer_id",
                table: "group_review_slots");

            migrationBuilder.DropIndex(
                name: "ix_student_slot_registrations_review_round_id_group_id_day_of_",
                table: "student_slot_registrations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_sessions_day_of_week",
                table: "review_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_sessions_slot",
                table: "review_sessions");

            migrationBuilder.DropIndex(
                name: "ix_review_schedule_publications_semester_id_review_type_week_s",
                table: "review_schedule_publications");

            migrationBuilder.DropIndex(
                name: "ix_review_checklist_submissions_session_id_group_id_reviewer_id",
                table: "review_checklist_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_availabilities_day_of_week",
                table: "review_availabilities");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_availabilities_slot",
                table: "review_availabilities");

            migrationBuilder.DropIndex(
                name: "ix_group_review_slots_completed_by_lecturer_id",
                table: "group_review_slots");

            migrationBuilder.DropIndex(
                name: "ix_group_review_slots_session_id_group_position",
                table: "group_review_slots");

            migrationBuilder.DropCheckConstraint(
                name: "ck_group_review_slots_position",
                table: "group_review_slots");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "group_review_slots");

            migrationBuilder.DropColumn(
                name: "completed_by_lecturer_id",
                table: "group_review_slots");

            migrationBuilder.DropColumn(
                name: "status",
                table: "group_review_slots");

            migrationBuilder.Sql(
                """
                DELETE FROM student_slot_registrations AS duplicate
                USING student_slot_registrations AS keeper
                WHERE duplicate.review_round_id = keeper.review_round_id
                  AND duplicate.group_id = keeper.group_id
                  AND duplicate.id > keeper.id;

                DELETE FROM review_checklist_submissions AS duplicate
                USING review_checklist_submissions AS keeper
                WHERE duplicate.session_id = keeper.session_id
                  AND duplicate.reviewer_id = keeper.reviewer_id
                  AND duplicate.id > keeper.id;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_student_slot_registrations_review_round_id_group_id",
                table: "student_slot_registrations",
                columns: new[] { "review_round_id", "group_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_sessions_slot",
                table: "review_sessions",
                sql: "slot BETWEEN 1 AND 8");

            migrationBuilder.CreateIndex(
                name: "ix_review_schedule_publications_semester_id_review_type_week_s",
                table: "review_schedule_publications",
                columns: new[] { "semester_id", "review_type", "week_start_date" });

            migrationBuilder.CreateIndex(
                name: "ix_review_checklist_submissions_session_id_reviewer_id",
                table: "review_checklist_submissions",
                columns: new[] { "session_id", "reviewer_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_availabilities_day_of_week",
                table: "review_availabilities",
                sql: "day_of_week BETWEEN 1 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_availabilities_slot",
                table: "review_availabilities",
                sql: "slot BETWEEN 1 AND 8");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CPMS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class ScopeReviewAvailabilityAndEmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_review_sessions_day_of_week",
                table: "review_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_sessions_slot",
                table: "review_sessions");

            migrationBuilder.DropIndex(
                name: "ix_review_availability_submissions_semester_id_lecturer_id_wee",
                table: "review_availability_submissions");

            migrationBuilder.DropIndex(
                name: "ix_review_availabilities_semester_id_lecturer_id_week_start_da",
                table: "review_availabilities");

            migrationBuilder.AddColumn<int>(
                name: "review_round_id",
                table: "review_availability_submissions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "review_round_id",
                table: "review_availabilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "body",
                table: "email_delivery_logs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                DELETE FROM review_availability_submissions AS submission
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM review_rounds AS round
                    WHERE round.semester_id = submission.semester_id
                      AND round.week_start_date = submission.week_start_date);

                UPDATE review_availability_submissions AS submission
                SET review_round_id = (
                    SELECT MIN(round.id)
                    FROM review_rounds AS round
                    WHERE round.semester_id = submission.semester_id
                      AND round.week_start_date = submission.week_start_date);

                INSERT INTO review_availability_submissions
                    (review_round_id, semester_id, lecturer_id, week_start_date, updated_at, submitted_at)
                SELECT round.id, submission.semester_id, submission.lecturer_id,
                       submission.week_start_date, submission.updated_at, submission.submitted_at
                FROM review_availability_submissions AS submission
                JOIN review_rounds AS round
                  ON round.semester_id = submission.semester_id
                 AND round.week_start_date = submission.week_start_date
                 AND round.id <> submission.review_round_id;

                DELETE FROM review_availabilities AS availability
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM review_rounds AS round
                    WHERE round.semester_id = availability.semester_id
                      AND round.week_start_date = availability.week_start_date);

                UPDATE review_availabilities AS availability
                SET review_round_id = (
                    SELECT MIN(round.id)
                    FROM review_rounds AS round
                    WHERE round.semester_id = availability.semester_id
                      AND round.week_start_date = availability.week_start_date);

                INSERT INTO review_availabilities
                    (review_round_id, semester_id, lecturer_id, week_start_date, day_of_week, slot, created_at)
                SELECT round.id, availability.semester_id, availability.lecturer_id,
                       availability.week_start_date, availability.day_of_week, availability.slot, availability.created_at
                FROM review_availabilities AS availability
                JOIN review_rounds AS round
                  ON round.semester_id = availability.semester_id
                 AND round.week_start_date = availability.week_start_date
                 AND round.id <> availability.review_round_id;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "review_round_id",
                table: "review_availability_submissions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "review_round_id",
                table: "review_availabilities",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_sessions_day_of_week",
                table: "review_sessions",
                sql: "status = 'Cancelled' OR day_of_week BETWEEN 1 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_sessions_slot",
                table: "review_sessions",
                sql: "status = 'Cancelled' OR slot BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "ix_review_availability_submissions_review_round_id_lecturer_id",
                table: "review_availability_submissions",
                columns: new[] { "review_round_id", "lecturer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_availability_submissions_semester_id",
                table: "review_availability_submissions",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_availabilities_review_round_id_lecturer_id_day_of_we",
                table: "review_availabilities",
                columns: new[] { "review_round_id", "lecturer_id", "day_of_week", "slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_availabilities_semester_id",
                table: "review_availabilities",
                column: "semester_id");

            migrationBuilder.AddForeignKey(
                name: "fk_review_availabilities_review_rounds_review_round_id",
                table: "review_availabilities",
                column: "review_round_id",
                principalTable: "review_rounds",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_review_availability_submissions_review_rounds_review_round_",
                table: "review_availability_submissions",
                column: "review_round_id",
                principalTable: "review_rounds",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_review_availabilities_review_rounds_review_round_id",
                table: "review_availabilities");

            migrationBuilder.DropForeignKey(
                name: "fk_review_availability_submissions_review_rounds_review_round_",
                table: "review_availability_submissions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_sessions_day_of_week",
                table: "review_sessions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_sessions_slot",
                table: "review_sessions");

            migrationBuilder.DropIndex(
                name: "ix_review_availability_submissions_review_round_id_lecturer_id",
                table: "review_availability_submissions");

            migrationBuilder.DropIndex(
                name: "ix_review_availability_submissions_semester_id",
                table: "review_availability_submissions");

            migrationBuilder.DropIndex(
                name: "ix_review_availabilities_review_round_id_lecturer_id_day_of_we",
                table: "review_availabilities");

            migrationBuilder.DropIndex(
                name: "ix_review_availabilities_semester_id",
                table: "review_availabilities");

            migrationBuilder.Sql(
                """
                DELETE FROM review_availability_submissions AS duplicate
                USING review_availability_submissions AS keeper
                WHERE duplicate.semester_id = keeper.semester_id
                  AND duplicate.lecturer_id = keeper.lecturer_id
                  AND duplicate.week_start_date = keeper.week_start_date
                  AND duplicate.id > keeper.id;

                DELETE FROM review_availabilities AS duplicate
                USING review_availabilities AS keeper
                WHERE duplicate.semester_id = keeper.semester_id
                  AND duplicate.lecturer_id = keeper.lecturer_id
                  AND duplicate.week_start_date = keeper.week_start_date
                  AND duplicate.day_of_week = keeper.day_of_week
                  AND duplicate.slot = keeper.slot
                  AND duplicate.id > keeper.id;
                """);

            migrationBuilder.DropColumn(
                name: "review_round_id",
                table: "review_availability_submissions");

            migrationBuilder.DropColumn(
                name: "review_round_id",
                table: "review_availabilities");

            migrationBuilder.DropColumn(
                name: "body",
                table: "email_delivery_logs");

            migrationBuilder.Sql(
                """
                UPDATE review_sessions
                SET day_of_week = 1
                WHERE day_of_week NOT BETWEEN 1 AND 6;

                UPDATE review_sessions
                SET slot = 1
                WHERE slot NOT BETWEEN 1 AND 5;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_sessions_day_of_week",
                table: "review_sessions",
                sql: "day_of_week BETWEEN 1 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_sessions_slot",
                table: "review_sessions",
                sql: "slot BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "ix_review_availability_submissions_semester_id_lecturer_id_wee",
                table: "review_availability_submissions",
                columns: new[] { "semester_id", "lecturer_id", "week_start_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_availabilities_semester_id_lecturer_id_week_start_da",
                table: "review_availabilities",
                columns: new[] { "semester_id", "lecturer_id", "week_start_date", "day_of_week", "slot" },
                unique: true);
        }
    }
}

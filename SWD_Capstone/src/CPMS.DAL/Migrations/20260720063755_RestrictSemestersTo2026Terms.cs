using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CPMS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RestrictSemestersTo2026Terms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $cleanup$
                DECLARE canonical_id integer; legacy_id integer;
                BEGIN
                    SELECT id INTO canonical_id FROM semesters WHERE upper(code) = 'SU26' LIMIT 1;
                    SELECT id INTO legacy_id FROM semesters WHERE upper(code) = 'SU2026' LIMIT 1;
                    IF canonical_id IS NULL AND legacy_id IS NULL AND NOT EXISTS (SELECT 1 FROM semesters) THEN
                        RETURN;
                    END IF;
                    IF canonical_id IS NULL THEN RAISE EXCEPTION 'SU26 does not exist'; END IF;
                    IF legacy_id IS NOT NULL THEN
                        INSERT INTO data_normalization_archives (batch_key, entity_type, source_id, payload)
                        SELECT 'restrict-semester-codes-v1', 'semester', id::text, to_jsonb(s)
                        FROM semesters s WHERE id = legacy_id ON CONFLICT DO NOTHING;
                        IF EXISTS (SELECT 1 FROM topics WHERE semester_id = legacy_id)
                           OR EXISTS (SELECT 1 FROM capstone_groups WHERE semester_id = legacy_id)
                           OR EXISTS (SELECT 1 FROM review_rounds WHERE semester_id = legacy_id)
                           OR EXISTS (SELECT 1 FROM review_sessions WHERE semester_id = legacy_id) THEN
                            RAISE EXCEPTION 'SU2026 contains academic data; refusing unsafe deletion';
                        END IF;
                        UPDATE semesters SET is_active = false WHERE id = legacy_id;
                        DELETE FROM semesters WHERE id = legacy_id;
                    END IF;
                    UPDATE semesters SET is_active = false WHERE is_active AND id <> canonical_id;
                    UPDATE semesters SET is_active = true WHERE id = canonical_id;
                END
                $cleanup$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_semesters_supported_codes",
                table: "semesters",
                sql: "upper(\"code\") IN ('SP26', 'SU26', 'FA26')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_semesters_supported_codes",
                table: "semesters");
        }
    }
}

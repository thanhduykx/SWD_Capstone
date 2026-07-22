using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CPMS.DAL.Migrations;

/// <summary>
/// Binds the real Google account requested for the seeded leader without
/// replacing the student row or any of its review/group relationships.
/// </summary>
[DbContext(typeof(CpmsDbContext))]
[Migration("20260723120000_BindRequestedStudentGoogleAccount")]
public partial class BindRequestedStudentGoogleAccount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM users
                    WHERE lower(email) = lower('duongduy12314@gmail.com')
                      AND id <> (SELECT user_id FROM students WHERE code = 'SS180745' LIMIT 1)
                ) THEN
                    RAISE EXCEPTION 'Requested student Gmail is already bound to another account.';
                END IF;

                UPDATE users AS u
                SET email = 'duongduy12314@gmail.com', updated_at = NOW()
                FROM students AS s
                WHERE s.user_id = u.id
                  AND s.code = 'SS180745';
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE users AS u
            SET email = 'victornhatthinh@gmail.com', updated_at = NOW()
            FROM students AS s
            WHERE s.user_id = u.id
              AND s.code = 'SS180745'
              AND lower(u.email) = lower('duongduy12314@gmail.com');
            """);
    }
}

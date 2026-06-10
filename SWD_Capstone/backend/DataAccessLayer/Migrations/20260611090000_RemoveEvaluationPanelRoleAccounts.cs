using CPMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CpmsDbContext))]
    [Migration("20260611090000_RemoveEvaluationPanelRoleAccounts")]
    public partial class RemoveEvaluationPanelRoleAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM refresh_tokens
                WHERE user_id IN (SELECT id FROM users WHERE role = 'EvaluationPanel');

                DELETE FROM notifications
                WHERE recipient_id IN (SELECT id FROM users WHERE role = 'EvaluationPanel')
                   OR sender_id IN (SELECT id FROM users WHERE role = 'EvaluationPanel');

                DELETE FROM email_delivery_logs
                WHERE recipient_user_id IN (SELECT id FROM users WHERE role = 'EvaluationPanel');

                DELETE FROM audit_logs
                WHERE user_id IN (SELECT id FROM users WHERE role = 'EvaluationPanel');

                DELETE FROM evaluation_panels
                WHERE user_id IN (SELECT id FROM users WHERE role = 'EvaluationPanel');

                DELETE FROM users
                WHERE role = 'EvaluationPanel';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // EvaluationPanel accounts are intentionally not restored.
        }
    }
}

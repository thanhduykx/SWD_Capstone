using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CPMS.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentReviewCheckIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "marked_by_lecturer_id",
                table: "review_attendances",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "student_confirmed_at",
                table: "review_attendances",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "student_confirmed_at",
                table: "review_attendances");

            migrationBuilder.AlterColumn<int>(
                name: "marked_by_lecturer_id",
                table: "review_attendances",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}

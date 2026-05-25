using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CPMS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCpmsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "semesters",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    academic_year = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_semesters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "topics",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    name_vn = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    semester_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_topics", x => x.id);
                    table.ForeignKey(
                        name: "fk_topics_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_panels",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    department = table.Column<string>(type: "text", nullable: false)
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
                name: "lecturers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    department = table.Column<string>(type: "text", nullable: false),
                    is_part_time = table.Column<bool>(type: "boolean", nullable: false),
                    max_groups = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lecturers", x => x.id);
                    table.ForeignKey(
                        name: "fk_lecturers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recipient_id = table.Column<int>(type: "integer", nullable: false),
                    sender_id = table.Column<int>(type: "integer", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_users_sender_id",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    device_info = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "system_administrators",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    admin_level = table.Column<string>(type: "text", nullable: false),
                    permission_scope = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_administrators", x => x.id);
                    table.ForeignKey(
                        name: "fk_system_administrators_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "training_departments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    department_name = table.Column<string>(type: "text", nullable: false),
                    staff_code = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_departments", x => x.id);
                    table.ForeignKey(
                        name: "fk_training_departments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "capstone_groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    topic_id = table.Column<int>(type: "integer", nullable: false),
                    semester_id = table.Column<int>(type: "integer", nullable: false),
                    lecturer_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capstone_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_capstone_groups_lecturers_lecturer_id",
                        column: x => x.lecturer_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_capstone_groups_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_capstone_groups_topics_topic_id",
                        column: x => x.topic_id,
                        principalTable: "topics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "councils",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    semester_id = table.Column<int>(type: "integer", nullable: false),
                    chairman_id = table.Column<int>(type: "integer", nullable: false),
                    secretary_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_councils", x => x.id);
                    table.ForeignKey(
                        name: "fk_councils_lecturers_chairman_id",
                        column: x => x.chairman_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_councils_lecturers_secretary_id",
                        column: x => x.secretary_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_councils_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_sessions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "text", nullable: false),
                    semester_id = table.Column<int>(type: "integer", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    room = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    reviewer1id = table.Column<int>(type: "integer", nullable: true),
                    reviewer2id = table.Column<int>(type: "integer", nullable: true),
                    session_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_sessions_lecturers_reviewer1id",
                        column: x => x.reviewer1id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_sessions_lecturers_reviewer2id",
                        column: x => x.reviewer2id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_sessions_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "syllabuses",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    training_department_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    major = table.Column<string>(type: "text", nullable: false),
                    academic_year = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_syllabuses", x => x.id);
                    table.ForeignKey(
                        name: "fk_syllabuses_training_departments_training_department_id",
                        column: x => x.training_department_id,
                        principalTable: "training_departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    semester_id = table.Column<int>(type: "integer", nullable: false),
                    review1result = table.Column<string>(type: "text", nullable: true),
                    review2result = table.Column<string>(type: "text", nullable: true),
                    review3result = table.Column<string>(type: "text", nullable: true),
                    supervisor_result = table.Column<string>(type: "text", nullable: true),
                    defense1result = table.Column<string>(type: "text", nullable: true),
                    defense2result = table.Column<string>(type: "text", nullable: true),
                    final_result = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_results_capstone_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "capstone_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_group_results_semesters_semester_id",
                        column: x => x.semester_id,
                        principalTable: "semesters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "students",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    class_code = table.Column<string>(type: "text", nullable: false),
                    batch = table.Column<string>(type: "text", nullable: true),
                    major = table.Column<string>(type: "text", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_students", x => x.id);
                    table.ForeignKey(
                        name: "fk_students_capstone_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "capstone_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_students_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "council_groups",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    council_id = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    conflict_flag = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_council_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_council_groups_capstone_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "capstone_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_council_groups_councils_council_id",
                        column: x => x.council_id,
                        principalTable: "councils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "council_members",
                columns: table => new
                {
                    council_id = table.Column<int>(type: "integer", nullable: false),
                    lecturer_id = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_council_members", x => new { x.council_id, x.lecturer_id });
                    table.ForeignKey(
                        name: "fk_council_members_councils_council_id",
                        column: x => x.council_id,
                        principalTable: "councils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_council_members_lecturers_lecturer_id",
                        column: x => x.lecturer_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "defense_sessions",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    council_id = table.Column<int>(type: "integer", nullable: false),
                    session_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_by_id = table.Column<int>(type: "integer", nullable: true),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_defense_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_defense_sessions_councils_council_id",
                        column: x => x.council_id,
                        principalTable: "councils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_defense_sessions_users_started_by_id",
                        column: x => x.started_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "group_review_slots",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    group_position = table.Column<int>(type: "integer", nullable: false),
                    result = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    conflict_flag = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_review_slots", x => x.id);
                    table.ForeignKey(
                        name: "fk_group_review_slots_capstone_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "capstone_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_group_review_slots_review_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "review_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "clos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    syllabus_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
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
                name: "documents",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    syllabus_id = table.Column<int>(type: "integer", nullable: false),
                    uploaded_by_id = table.Column<int>(type: "integer", nullable: false),
                    doc_type = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    file_path = table.Column<string>(type: "text", nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_documents_capstone_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "capstone_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documents_syllabuses_syllabus_id",
                        column: x => x.syllabus_id,
                        principalTable: "syllabuses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_documents_users_uploaded_by_id",
                        column: x => x.uploaded_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    defense_session_id = table.Column<int>(type: "integer", nullable: false),
                    scorer_id = table.Column<int>(type: "integer", nullable: false),
                    student_id = table.Column<int>(type: "integer", nullable: false),
                    score_type = table.Column<string>(type: "text", nullable: false),
                    score_value = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scores", x => x.id);
                    table.ForeignKey(
                        name: "fk_scores_defense_sessions_defense_session_id",
                        column: x => x.defense_session_id,
                        principalTable: "defense_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_scores_lecturers_scorer_id",
                        column: x => x.scorer_id,
                        principalTable: "lecturers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_scores_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
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
                    created_by_admin_id = table.Column<int>(type: "integer", nullable: false),
                    keyword_text = table.Column<string>(type: "text", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<int>(type: "integer", nullable: true)
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
                    reviewed_by_id = table.Column<int>(type: "integer", nullable: false),
                    overall_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    match_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    gap_analysis_summary = table.Column<string>(type: "text", nullable: true),
                    trigger_type = table.Column<string>(type: "text", nullable: false),
                    evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "inline_comments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_id = table.Column<int>(type: "integer", nullable: false),
                    author_id = table.Column<int>(type: "integer", nullable: false),
                    paragraph_index = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    parent_comment_id = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inline_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_inline_comments_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inline_comments_inline_comments_parent_comment_id",
                        column: x => x.parent_comment_id,
                        principalTable: "inline_comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inline_comments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_details",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    report_id = table.Column<int>(type: "integer", nullable: false),
                    clo_id = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    max_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    match_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    feedback = table.Column<string>(type: "text", nullable: true),
                    missing_evidence = table.Column<string>(type: "text", nullable: true)
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
                name: "ix_audit_logs_user_id",
                table: "audit_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_capstone_groups_code_semester_id",
                table: "capstone_groups",
                columns: new[] { "code", "semester_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_capstone_groups_lecturer_id",
                table: "capstone_groups",
                column: "lecturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_capstone_groups_semester_id",
                table: "capstone_groups",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "ix_capstone_groups_topic_id",
                table: "capstone_groups",
                column: "topic_id");

            migrationBuilder.CreateIndex(
                name: "ix_clos_syllabus_id_code",
                table: "clos",
                columns: new[] { "syllabus_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_council_groups_council_id_group_id",
                table: "council_groups",
                columns: new[] { "council_id", "group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_council_groups_group_id",
                table: "council_groups",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_council_members_lecturer_id",
                table: "council_members",
                column: "lecturer_id");

            migrationBuilder.CreateIndex(
                name: "ix_councils_chairman_id",
                table: "councils",
                column: "chairman_id");

            migrationBuilder.CreateIndex(
                name: "ix_councils_code_semester_id",
                table: "councils",
                columns: new[] { "code", "semester_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_councils_secretary_id",
                table: "councils",
                column: "secretary_id");

            migrationBuilder.CreateIndex(
                name: "ix_councils_semester_id",
                table: "councils",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "ix_defense_sessions_council_id",
                table: "defense_sessions",
                column: "council_id");

            migrationBuilder.CreateIndex(
                name: "ix_defense_sessions_started_by_id",
                table: "defense_sessions",
                column: "started_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_group_id",
                table: "documents",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_syllabus_id",
                table: "documents",
                column: "syllabus_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_uploaded_by_id",
                table: "documents",
                column: "uploaded_by_id");

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
                name: "ix_group_results_group_id_semester_id",
                table: "group_results",
                columns: new[] { "group_id", "semester_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_results_semester_id",
                table: "group_results",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_review_slots_group_id",
                table: "group_review_slots",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_review_slots_session_id_group_id",
                table: "group_review_slots",
                columns: new[] { "session_id", "group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inline_comments_author_id",
                table: "inline_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_inline_comments_document_id",
                table: "inline_comments",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "ix_inline_comments_parent_comment_id",
                table: "inline_comments",
                column: "parent_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_lecturers_code",
                table: "lecturers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lecturers_user_id",
                table: "lecturers",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_id",
                table: "notifications",
                column: "recipient_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_sender_id",
                table: "notifications",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_code_semester_id",
                table: "review_sessions",
                columns: new[] { "code", "semester_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_reviewer1id",
                table: "review_sessions",
                column: "reviewer1id");

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_reviewer2id",
                table: "review_sessions",
                column: "reviewer2id");

            migrationBuilder.CreateIndex(
                name: "ix_review_sessions_semester_id",
                table: "review_sessions",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "ix_rule_keywords_clo_id",
                table: "rule_keywords",
                column: "clo_id");

            migrationBuilder.CreateIndex(
                name: "ix_rule_keywords_created_by_admin_id",
                table: "rule_keywords",
                column: "created_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_scores_defense_session_id_scorer_id_student_id_score_type",
                table: "scores",
                columns: new[] { "defense_session_id", "scorer_id", "student_id", "score_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scores_scorer_id",
                table: "scores",
                column: "scorer_id");

            migrationBuilder.CreateIndex(
                name: "ix_scores_student_id",
                table: "scores",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_semesters_code",
                table: "semesters",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_semesters_is_active",
                table: "semesters",
                column: "is_active",
                unique: true,
                filter: "\"is_active\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "ix_students_code",
                table: "students",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_students_group_id",
                table: "students",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_user_id",
                table: "students",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_syllabuses_training_department_id",
                table: "syllabuses",
                column: "training_department_id");

            migrationBuilder.CreateIndex(
                name: "ix_system_administrators_user_id",
                table: "system_administrators",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_topics_code",
                table: "topics",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_topics_semester_id",
                table: "topics",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_departments_user_id",
                table: "training_departments",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION prevent_audit_log_mutation()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'Audit logs are append-only and cannot be changed or deleted.';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER tr_audit_logs_append_only
                BEFORE UPDATE OR DELETE ON audit_logs
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_log_mutation();
                """);

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION prevent_locked_score_mutation()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'Locked scores cannot be changed or deleted.';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER tr_scores_locked_immutable
                BEFORE UPDATE OR DELETE ON scores
                FOR EACH ROW
                WHEN (OLD.is_locked = TRUE)
                EXECUTE FUNCTION prevent_locked_score_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_scores_locked_immutable ON scores;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_locked_score_mutation();");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS tr_audit_logs_append_only ON audit_logs;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_audit_log_mutation();");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "council_groups");

            migrationBuilder.DropTable(
                name: "council_members");

            migrationBuilder.DropTable(
                name: "evaluation_details");

            migrationBuilder.DropTable(
                name: "group_results");

            migrationBuilder.DropTable(
                name: "group_review_slots");

            migrationBuilder.DropTable(
                name: "inline_comments");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "rule_keywords");

            migrationBuilder.DropTable(
                name: "scores");

            migrationBuilder.DropTable(
                name: "evaluation_reports");

            migrationBuilder.DropTable(
                name: "review_sessions");

            migrationBuilder.DropTable(
                name: "clos");

            migrationBuilder.DropTable(
                name: "system_administrators");

            migrationBuilder.DropTable(
                name: "defense_sessions");

            migrationBuilder.DropTable(
                name: "students");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "evaluation_panels");

            migrationBuilder.DropTable(
                name: "councils");

            migrationBuilder.DropTable(
                name: "capstone_groups");

            migrationBuilder.DropTable(
                name: "syllabuses");

            migrationBuilder.DropTable(
                name: "lecturers");

            migrationBuilder.DropTable(
                name: "topics");

            migrationBuilder.DropTable(
                name: "training_departments");

            migrationBuilder.DropTable(
                name: "semesters");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}

using CPMS.BLL.Abstractions.Persistence;
using CPMS.BLL.Testing;
using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using CPMS.BLL.Services;
using Microsoft.EntityFrameworkCore;

namespace CPMS.DAL.Data;

public sealed class DatabaseLifecycleService(CpmsDbContext dbContext)
    : IDatabaseLifecycleService, ITestDataResetService
{
    private static readonly string[] ResetTables =
    [
        "audit_logs", "notifications", "review_comments", "review_attendances",
        "student_slot_registrations", "review_rounds", "group_results", "defense_evidences",
        "score_submission_histories", "scores", "defense_sessions", "defense_rounds",
        "council_groups", "council_members", "councils", "email_delivery_logs",
        "review_schedule_publications", "review_checklist_item_responses", "review_checklist_submissions",
        "group_review_slots", "review_sessions", "review_availability_submissions", "review_availabilities",
        "inline_comments", "evaluation_details", "evaluation_reports", "documents", "rule_keywords",
        "clos", "syllabuses", "students", "capstone_groups", "topics", "semesters",
        "system_administrators", "training_departments", "lecturers", "evaluation_panels",
        "refresh_tokens", "users"
    ];

    public async Task InitializeAsync(bool seedDevelopmentData, CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        // Only seed in Dev / explicit test-support. Never mutate shared website data on normal Production starts.
        if (seedDevelopmentData)
            await SeedDevelopmentDataAsync(cancellationToken);
    }

    /// <summary>
    /// Opt-in upsert of shared lecturer personas/dataset on the active semester (web + mobile).
    /// Must not be called from normal Production startup; use Dev / ExposeTestSupport only.
    /// </summary>
    public Task EnsureMobileLecturerDatasetAsync(CancellationToken cancellationToken) =>
        SeedDevelopmentDataAsync(cancellationToken);

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        dbContext.Database.CanConnectAsync(cancellationToken);

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        var sql = $"TRUNCATE TABLE {string.Join(", ", ResetTables.Select(x => $"\"{x}\""))} RESTART IDENTITY CASCADE;";
        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        await SeedDevelopmentDataAsync(cancellationToken);
    }

    private async Task SeedDevelopmentDataAsync(CancellationToken cancellationToken)
    {
        await SeedModeratorAsync(cancellationToken);
        await SeedTestAccountsAsync(cancellationToken);
        await SeedAcademicDataAsync(cancellationToken);
        await SeedLecturerWorkflowDataAsync(cancellationToken);
    }

    private async Task SeedModeratorAsync(CancellationToken cancellationToken)
    {
        const string username = "Admin@gmail.com";
        const string email = "Admin@gmail.com";
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var user = await dbContext.Users.FirstOrDefaultAsync(
            x => x.Username == username || x.Email == email, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("12345", workFactor: 12),
                Role = UserRole.TrainingDepartment,
                IsActive = true
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Existing shared-site moderator: never reset password/role here.
        }
        if (!await dbContext.TrainingDepartments.AnyAsync(x => x.UserId == user.Id, cancellationToken))
            dbContext.TrainingDepartments.Add(new TrainingDepartment
            {
                UserId = user.Id,
                DepartmentName = "Training Department",
                StaffCode = "TD001",
                Position = "Moderator"
            });
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "SEED_MODERATOR_ACCOUNT",
            EntityType = nameof(User),
            EntityId = user.Id,
            NewValue = user.Username,
            UserAgent = "ApplicationStartup"
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SeedTestAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = new (
            string Username,
            string Email,
            UserRole Role,
            string? LecturerCode,
            string? FullName,
            string[] LegacyLogins)[]
        {
            ("pdt.dieuphoi", "pdt.dieuphoi@fpt.edu.vn", UserRole.TrainingDepartment, null, null,
                ["test.training", "test.training@cpms.local"]),
            (AccountUsernameGenerator.Generate("Nguyễn Đức Minh", "GV24001"), "minhnd.gv24001@fpt.edu.vn",
                UserRole.Lecturer, "GV24001", "Nguyễn Đức Minh",
                ["test.lecturer", "test.lecturer@cpms.local"]),
            (AccountUsernameGenerator.Generate("Phạm Thu Hà", "GV24002"), "hapt.gv24002@fpt.edu.vn",
                UserRole.Lecturer, "GV24002", "Phạm Thu Hà",
                ["test.lecturer2", "test.lecturer2@cpms.local"]),
            (AccountUsernameGenerator.Generate("Lê Hoàng Nam", "GV24003"), "namlh.gv24003@fpt.edu.vn",
                UserRole.Lecturer, "GV24003", "Lê Hoàng Nam",
                ["test.lecturer3", "test.lecturer3@cpms.local"]),
            (AccountUsernameGenerator.Generate("Nguyễn Văn Mẫu", "SE199999"), "maunv.se199999@fpt.edu.vn",
                UserRole.Student, null, null,
                ["test.student", "test.student@cpms.local"])
        };

        foreach (var account in accounts)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var user = await dbContext.Users.FirstOrDefaultAsync(x =>
                    x.Username == account.Username ||
                    x.Email == account.Email ||
                    account.LegacyLogins.Contains(x.Username) ||
                    account.LegacyLogins.Contains(x.Email),
                cancellationToken);
            if (user is null)
            {
                user = new User
                {
                    Username = account.Username,
                    Email = account.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123456", workFactor: 12),
                    Role = account.Role,
                    IsActive = true
                };
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                // Migrate legacy logins only; do not overwrite passwords of existing shared-site accounts.
                var isLegacy = account.LegacyLogins.Contains(user.Username)
                    || account.LegacyLogins.Contains(user.Email);
                if (isLegacy
                    || string.Equals(user.Username, account.Username, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(user.Email, account.Email, StringComparison.OrdinalIgnoreCase))
                {
                    user.Username = account.Username;
                    user.Email = account.Email;
                    user.Role = account.Role;
                    user.IsActive = true;
                    user.LockedUntil = null;
                    user.FailedLoginAttempts = 0;
                    user.UpdatedAt = DateTime.UtcNow;
                }
            }

            if (account.Role == UserRole.TrainingDepartment)
            {
                var training = await dbContext.TrainingDepartments
                    .FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
                if (training is null)
                {
                    dbContext.TrainingDepartments.Add(new TrainingDepartment
                    {
                        UserId = user.Id,
                        DepartmentName = "Phòng Đào tạo",
                        StaffCode = "PDT2401",
                        Position = "Chuyên viên điều phối đồ án"
                    });
                }
                else
                {
                    training.DepartmentName = "Phòng Đào tạo";
                    training.StaffCode = "PDT2401";
                    training.Position = "Chuyên viên điều phối đồ án";
                }
            }
            else if (account.Role == UserRole.Lecturer && account.LecturerCode is not null)
            {
                var lecturer = await dbContext.Lecturers.FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken)
                    ?? await dbContext.Lecturers.FirstOrDefaultAsync(
                        x => x.Code == account.LecturerCode ||
                             (x.Code == "LEC_TEST" && account.LecturerCode == "GV24001") ||
                             (x.Code == "LEC_TEST2" && account.LecturerCode == "GV24002"),
                        cancellationToken);

                if (lecturer is null)
                {
                    dbContext.Lecturers.Add(new Lecturer
                    {
                        UserId = user.Id,
                        Code = account.LecturerCode,
                        FullName = account.FullName!,
                        Department = "Kỹ thuật phần mềm"
                    });
                }
                else
                {
                    lecturer.UserId = user.Id;
                    lecturer.Code = account.LecturerCode;
                    lecturer.FullName = account.FullName!;
                    lecturer.Department = "Kỹ thuật phần mềm";
                }
            }
            else if (account.Role == UserRole.Student &&
                     !await dbContext.Students.AnyAsync(x => x.UserId == user.Id, cancellationToken))
            {
                dbContext.Students.Add(new Student
                {
                    UserId = user.Id,
                    Code = "SE199999",
                    FullName = "Nguyễn Văn Mẫu",
                    ClassCode = "SE1901",
                    Major = "Kỹ thuật phần mềm",
                    IsLeader = false
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
    private async Task SeedAcademicDataAsync(CancellationToken cancellationToken)
    {
        var lecturer1 = await dbContext.Lecturers.SingleAsync(x => x.Code == "GV24001", cancellationToken);
        var lecturer2 = await dbContext.Lecturers.SingleAsync(x => x.Code == "GV24002", cancellationToken);
        var lecturer3 = await dbContext.Lecturers.SingleAsync(x => x.Code == "GV24003", cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Shared semester with website workflows (prefer current active term).
        var semester = await dbContext.Semesters.FirstOrDefaultAsync(x => x.IsActive, cancellationToken)
            ?? await dbContext.Semesters.FirstOrDefaultAsync(x => x.Code == "SU2026", cancellationToken);
        if (semester is null)
        {
            semester = new Semester
            {
                Code = "SU2026",
                Name = "Học kỳ Summer 2026",
                AcademicYear = "2025-2026",
                StartDate = new DateOnly(2026, 5, 1),
                EndDate = new DateOnly(2026, 8, 31),
                IsActive = true
            };
            dbContext.Semesters.Add(semester);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var groupsSpec = new[]
        {
            CatalogGroup("SE2601", "TP2601_SMART_HOME",
                "Hệ thống quản lý kho nhà thông minh", "Smart Home Inventory Management System",
                lecturer1.Id, [
                    ("Huỳnh Nhật Thịnh", "SE194673", false),
                    ("Phạm Thị Ánh", "SE182446", false),
                    ("Dương Thành Thanh Duy", "SS180745", true),
                    ("Trương Đinh Hồng Nhung", "SS194356", false),
                    ("Vũ Minh Quang", "SE192755", false),
                    ("Nguyễn Minh Thành", "SE191034", false)
                ]),
            CatalogGroup("SE2602", "TP2602_CAMPUS_NAV",
                "Ứng dụng định hướng khuôn viên trường", "Campus Navigation Mobile Application",
                lecturer2.Id, [
                    ("Hoàng Thị Mai", "SE193201", true),
                    ("Phan Quốc Bảo", "SE193202", false),
                    ("Đặng Ngọc Lan", "SE193203", false),
                    ("Bùi Văn Khoa", "SE193204", false),
                    ("Ngô Thành Đạt", "SE193205", false)
                ]),
            CatalogGroup("SE2603", "TP2603_HEALTH_IOT",
                "Hệ thống theo dõi sức khỏe IoT cho người cao tuổi", "IoT Elderly Health Monitoring Platform",
                lecturer3.Id, [
                    ("Lý Minh Châu", "SE194101", true),
                    ("Trần Gia Huy", "SE194102", false),
                    ("Võ Thanh Tùng", "SE194103", false),
                    ("Đỗ Mỹ Linh", "SE194104", false),
                    ("Phạm Anh Thư", "SE194105", false)
                ]),
            CatalogGroup("SE2604", "TP2604_EDU_TUTOR",
                "Trợ lý học tập cá nhân hóa bằng AI", "AI Personalized Learning Tutor",
                lecturer1.Id, [
                    ("Nguyễn Khánh Vy", "SE194201", true),
                    ("Lê Quang Hùng", "SE194202", false),
                    ("Phan Nhật Nam", "SE194203", false),
                    ("Hà Thu Trang", "SE194204", false),
                    ("Đinh Công Phúc", "SE194205", false)
                ]),
            CatalogGroup("SE2605", "TP2605_FOOD_TRACE",
                "Truy xuất nguồn gốc thực phẩm bằng blockchain", "Blockchain Food Traceability System",
                lecturer2.Id, [
                    ("Cao Thanh Sơn", "SE194301", true),
                    ("Mai Hoàng Yến", "SE194302", false),
                    ("Tống Đức Thịnh", "SE194303", false),
                    ("Lưu Bảo Ngọc", "SE194304", false)
                ]),
            CatalogGroup("SE2606", "TP2606_FLEET_OPS",
                "Nền tảng điều phối đội xe giao hàng nội thành", "Urban Delivery Fleet Operations Platform",
                lecturer3.Id, [
                    ("Ông Nhật Trường", "SE194401", true),
                    ("Quách Gia Bảo", "SE194402", false),
                    ("Tạ Minh Anh", "SE194403", false),
                    ("Uông Thị Hạnh", "SE194404", false),
                    ("Yên Văn Khoa", "SE194405", false)
                ])
        };

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Test@123456", workFactor: 12);
        foreach (var spec in groupsSpec)
        {
            var topic = await dbContext.Topics.FirstOrDefaultAsync(x =>
                    x.SemesterId == semester.Id &&
                    (x.Code == spec.TopicCode || x.NameEn == spec.NameEn || x.NameVn == spec.NameVn),
                cancellationToken);
            if (topic is null)
            {
                topic = new Topic
                {
                    Code = spec.TopicCode,
                    NameVn = spec.NameVn,
                    NameEn = spec.NameEn,
                    SemesterId = semester.Id
                };
                dbContext.Topics.Add(topic);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                topic.Code = spec.TopicCode;
                topic.NameVn = spec.NameVn;
                topic.NameEn = spec.NameEn;
            }

            var group = await dbContext.CapstoneGroups.FirstOrDefaultAsync(x =>
                    x.SemesterId == semester.Id &&
                    (x.Code == spec.GroupCode ||
                     (spec.GroupCode == "SE2601" && x.Code == "TEST_GROUP_SMART_HOME")),
                cancellationToken);
            if (group is null)
            {
                group = new CapstoneGroup
                {
                    Code = spec.GroupCode,
                    TopicId = topic.Id,
                    SemesterId = semester.Id,
                    LecturerId = spec.SupervisorLecturerId,
                    Status = GroupStatus.Active
                };
                dbContext.CapstoneGroups.Add(group);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                group.Code = spec.GroupCode;
                group.TopicId = topic.Id;
                group.LecturerId = spec.SupervisorLecturerId;
                group.Status = GroupStatus.Active;
            }

            foreach (var seed in spec.Students)
            {
                var student = await dbContext.Students.FirstOrDefaultAsync(x => x.Code == seed.Code, cancellationToken);
                var username = AccountUsernameGenerator.Generate(seed.FullName, seed.Code);
                var user = student is not null
                    ? await dbContext.Users.SingleAsync(x => x.Id == student.UserId, cancellationToken)
                    : await dbContext.Users.FirstOrDefaultAsync(x =>
                        x.Username == username || x.Email == seed.Email, cancellationToken);
                if (user is null)
                {
                    user = new User
                    {
                        Username = username,
                        Email = seed.Email,
                        PasswordHash = passwordHash,
                        Role = UserRole.Student,
                        IsActive = true
                    };
                    dbContext.Users.Add(user);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                else if (user.Role != UserRole.Student)
                {
                    throw new InvalidOperationException($"Student {seed.Code} conflicts with a non-student account.");
                }

                if (student is null)
                {
                    student = new Student
                    {
                        UserId = user.Id,
                        Code = seed.Code,
                        FullName = seed.FullName,
                        ClassCode = "SE1901",
                        Major = "Kỹ thuật phần mềm",
                        Batch = "K19"
                    };
                    dbContext.Students.Add(student);
                }

                student.FullName = seed.FullName;
                student.GroupId = group.Id;
                student.IsLeader = seed.IsLeader;
                student.ClassCode = "SE1901";
                student.Major = "Kỹ thuật phần mềm";
                student.Batch = "K19";
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static GroupSeed CatalogGroup(
        string groupCode,
        string topicCode,
        string nameVn,
        string nameEn,
        int supervisorId,
        (string FullName, string Code, bool IsLeader)[] members) =>
        new(
            groupCode,
            topicCode,
            nameVn,
            nameEn,
            supervisorId,
            members.Select(m => new StudentSeed(
                m.FullName,
                m.Code,
                $"{m.Code.ToLowerInvariant()}@fpt.edu.vn",
                m.IsLeader)).ToArray());

    /// <summary>
    /// Seeds a dense review/defense dataset (SU26_…) for the three lecturer accounts.
    /// </summary>
    private async Task SeedLecturerWorkflowDataAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var lecturer1 = await dbContext.Lecturers.SingleAsync(x => x.Code == "GV24001", cancellationToken);
        var lecturer2 = await dbContext.Lecturers.SingleAsync(x => x.Code == "GV24002", cancellationToken);
        var lecturer3 = await dbContext.Lecturers.SingleAsync(x => x.Code == "GV24003", cancellationToken);
        var lecturers = new[] { lecturer1, lecturer2, lecturer3 };
        var lecturerUsers = await dbContext.Users
            .Where(x => lecturers.Select(l => l.UserId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var training = await dbContext.TrainingDepartments.OrderBy(x => x.Id).FirstAsync(cancellationToken);
        var semester = await dbContext.Semesters.FirstOrDefaultAsync(x => x.IsActive, cancellationToken)
            ?? await dbContext.Semesters.SingleAsync(x => x.Code == "SU2026", cancellationToken);

        var groups = await dbContext.CapstoneGroups
            .Where(x => x.SemesterId == semester.Id && x.Code.StartsWith("SE26"))
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
        if (groups.Count == 0)
            throw new InvalidOperationException("No shared SE26xx capstone groups available for lecturer workflow seed.");
        if (groups.Count < 2)
            throw new InvalidOperationException("Need at least 2 shared groups for lecturer workflow seed.");

        var studentsByGroup = new Dictionary<int, List<Student>>();
        foreach (var group in groups)
        {
            var members = await dbContext.Students.Where(x => x.GroupId == group.Id).OrderBy(x => x.Id)
                .ToListAsync(cancellationToken);
            if (members.Count == 0)
                throw new InvalidOperationException($"Group {group.Code} has no students.");
            studentsByGroup[group.Id] = members;
        }

        var review1Week = new DateOnly(2026, 7, 6);
        var review2Week = new DateOnly(2026, 7, 20);
        var review3Week = new DateOnly(2026, 8, 3);

        var round1 = await EnsureReviewRoundAsync(
            semester.Id, ReviewType.Review1, review1Week, ReviewRoundStatus.Published,
            training.Id, now.AddDays(-40), now.AddDays(-39), now.AddDays(-30), cancellationToken);
        var round2 = await EnsureReviewRoundAsync(
            semester.Id, ReviewType.Review2, review2Week, ReviewRoundStatus.Published,
            training.Id, now.AddDays(-20), now.AddDays(-19), now.AddDays(-8), cancellationToken);
        var round3 = await EnsureReviewRoundAsync(
            semester.Id, ReviewType.Review3, review3Week, ReviewRoundStatus.Open,
            training.Id, now.AddDays(-4), now.AddDays(-3), null, cancellationToken);

        // Student availability for every group on every round.
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var leader = studentsByGroup[group.Id].FirstOrDefault(x => x.IsLeader) ?? studentsByGroup[group.Id][0];
            await EnsureStudentSlotsAsync(round1.Id, group.Id, leader.Id, 1 + (i % 5), 1 + (i % 4), cancellationToken);
            await EnsureStudentSlotsAsync(round2.Id, group.Id, leader.Id, 2 + (i % 4), 1 + ((i + 1) % 4), cancellationToken);
            await EnsureStudentSlotsAsync(round3.Id, group.Id, leader.Id, 1 + ((i + 2) % 5), 2 + (i % 3), cancellationToken);
        }

        // Lecturer availability — dense slots across rounds.
        foreach (var lecturer in lecturers)
        {
            await EnsureLecturerAvailabilityAsync(round1, lecturer, true, 2, 1, now, cancellationToken);
            await EnsureLecturerAvailabilityAsync(round1, lecturer, true, 3, 2, now, cancellationToken);
            await EnsureLecturerAvailabilityAsync(round2, lecturer, true, 2, 3, now, cancellationToken);
            await EnsureLecturerAvailabilityAsync(round2, lecturer, true, 4, 1, now, cancellationToken);
            await EnsureLecturerAvailabilityAsync(round3, lecturer, submitted: false, 2, 2, now, cancellationToken);
        }

        await RenameSessionCodeAsync("TEST_REV01", "SU26_R1_S01", cancellationToken);
        await RenameCouncilCodeAsync("TEST_COUNCIL01", "SU26_HD01", cancellationToken);
        await RenameDefenseRoundCodeAsync("TEST_DEF01", "SU26_BV_DOT1", "Đợt bảo vệ lần 1 — Summer 2026", cancellationToken);
        await RenameDefenseSessionCodeAsync("TEST_SESSION01", "SU26_BV1_S01", cancellationToken);

        var rooms = new[] { "A301", "A302", "B205", "B206", "C101", "Hội trường A", "Hội trường B" };
        var sessionPlans = new (string Code, ReviewType Type, int Day, int Slot, string Room, DateTime Date, int R1, int R2, int[] GroupIndexes, ReviewSubmissionStatus StatusForGv1)[]
        {
            ("SU26_R1_S01", ReviewType.Review1, 2, 1, rooms[0], new DateTime(2026, 7, 7, 8, 0, 0, DateTimeKind.Utc), lecturer1.Id, lecturer2.Id, [0, 1], ReviewSubmissionStatus.Draft),
            ("SU26_R1_S02", ReviewType.Review1, 2, 2, rooms[1], new DateTime(2026, 7, 7, 10, 30, 0, DateTimeKind.Utc), lecturer1.Id, lecturer3.Id, [2], ReviewSubmissionStatus.Submitted),
            ("SU26_R1_S03", ReviewType.Review1, 3, 1, rooms[2], new DateTime(2026, 7, 8, 8, 0, 0, DateTimeKind.Utc), lecturer2.Id, lecturer1.Id, [3, 4], ReviewSubmissionStatus.Draft),
            ("SU26_R1_S04", ReviewType.Review1, 4, 3, rooms[3], new DateTime(2026, 7, 9, 13, 0, 0, DateTimeKind.Utc), lecturer3.Id, lecturer1.Id, [5], ReviewSubmissionStatus.Draft),
            ("SU26_R1_S05", ReviewType.Review1, 5, 2, rooms[4], new DateTime(2026, 7, 10, 10, 30, 0, DateTimeKind.Utc), lecturer1.Id, lecturer2.Id, [0, 2, 3], ReviewSubmissionStatus.Submitted),
            ("SU26_R2_S01", ReviewType.Review2, 2, 3, rooms[0], new DateTime(2026, 7, 21, 8, 0, 0, DateTimeKind.Utc), lecturer1.Id, lecturer2.Id, [0, 1], ReviewSubmissionStatus.Draft),
            ("SU26_R2_S02", ReviewType.Review2, 3, 1, rooms[2], new DateTime(2026, 7, 22, 8, 0, 0, DateTimeKind.Utc), lecturer1.Id, lecturer3.Id, [2, 4], ReviewSubmissionStatus.Draft),
            ("SU26_R2_S03", ReviewType.Review2, 4, 2, rooms[5], new DateTime(2026, 7, 23, 10, 30, 0, DateTimeKind.Utc), lecturer2.Id, lecturer3.Id, [3, 5], ReviewSubmissionStatus.Submitted),
            ("SU26_R2_S04", ReviewType.Review2, 5, 1, rooms[1], new DateTime(2026, 7, 24, 8, 0, 0, DateTimeKind.Utc), lecturer3.Id, lecturer1.Id, [1], ReviewSubmissionStatus.Draft),
            ("SU26_R3_S01", ReviewType.Review3, 2, 2, rooms[0], new DateTime(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc), lecturer1.Id, lecturer2.Id, [0, 5], ReviewSubmissionStatus.Draft),
            ("SU26_R3_S02", ReviewType.Review3, 3, 3, rooms[6], new DateTime(2026, 8, 5, 13, 30, 0, DateTimeKind.Utc), lecturer1.Id, lecturer3.Id, [2, 3, 4], ReviewSubmissionStatus.Draft)
        };

        var comments = new[]
        {
            "Nhóm cần hoàn thiện sơ đồ kiến trúc và checklist kiểm thử tích hợp trước buổi chấm.",
            "Ưu tiên ổn định API gateway, bổ sung đo latency và nhật ký lỗi production-like.",
            "Demo còn thiếu luồng xử lý ngoại lệ; nên ghi rõ tiêu chí chấp nhận cho từng user story.",
            "Báo cáo tiến độ tốt; hãy đóng gói evidence deploy và screenshot dashboard giám sát.",
            "Cần phân rõ trách nhiệm thành viên và cập nhật burn-down theo milestone Review."
        };

        foreach (var plan in sessionPlans)
        {
            var groupIndexes = plan.GroupIndexes.Where(i => i >= 0 && i < groups.Count).Distinct().ToArray();
            if (groupIndexes.Length == 0) continue;

            var session = await EnsurePublishedReviewSessionAsync(
                plan.Code, semester.Id, plan.Type, plan.Day, plan.Slot, plan.Room, plan.Date,
                plan.R1, plan.R2, training.Id, now.AddDays(-7), cancellationToken);

            for (var pos = 0; pos < groupIndexes.Length; pos++)
            {
                var group = groups[groupIndexes[pos]];
                await EnsureGroupReviewSlotAsync(session.Id, group.Id, pos + 1, cancellationToken);

                var product = $"{group.Code} — {plan.Type} deliverable";
                await EnsureChecklistSubmissionAsync(
                    session.Id, group.Id, plan.R1, plan.Type, plan.StatusForGv1, now, product, cancellationToken);
                await EnsureChecklistSubmissionAsync(
                    session.Id, group.Id, plan.R2, plan.Type,
                    plan.StatusForGv1 == ReviewSubmissionStatus.Submitted
                        ? ReviewSubmissionStatus.Submitted
                        : ReviewSubmissionStatus.Draft,
                    now, product, cancellationToken);

                await EnsureReviewCommentAsync(
                    session.Id, group.Id, lecturerUsers[lecturer1.UserId].Id,
                    comments[(groupIndexes[pos] + plan.Day) % comments.Length],
                    now.AddDays(-3 - pos), cancellationToken);

                await EnsureReviewAttendancesAsync(
                    session.Id, studentsByGroup[group.Id], plan.R1, now, cancellationToken);
            }
        }

        await EnsurePublicationAsync(semester.Id, ReviewType.Review1, review1Week, training.Id,
            "Lịch Review 1 — Học kỳ Summer 2026",
            "Phòng Đào tạo công bố lịch Review 1 cho toàn bộ nhóm đồ án đang active.",
            now.AddDays(-30), cancellationToken);
        await EnsurePublicationAsync(semester.Id, ReviewType.Review2, review2Week, training.Id,
            "Lịch Review 2 — Học kỳ Summer 2026",
            "Lịch Review 2 đã được chốt theo availability của giảng viên và nhóm.",
            now.AddDays(-10), cancellationToken);
        await EnsurePublicationAsync(semester.Id, ReviewType.Review3, review3Week, training.Id,
            "Lịch Review 3 (dự kiến) — Học kỳ Summer 2026",
            "Review 3 đang mở đăng ký lịch; vui lòng cập nhật availability trước hạn.",
            now.AddDays(-2), cancellationToken);

        var council1 = await EnsureCouncilAsync(
            "SU26_HD01", semester.Id, training.Id, lecturer1.Id, lecturer2.Id,
            CouncilType.Defense1, CouncilStatus.Active, cancellationToken);
        await EnsureCouncilMemberAsync(council1.Id, lecturer1.Id, CouncilMemberRole.Chairman, cancellationToken);
        await EnsureCouncilMemberAsync(council1.Id, lecturer2.Id, CouncilMemberRole.Secretary, cancellationToken);
        await EnsureCouncilMemberAsync(council1.Id, lecturer3.Id, CouncilMemberRole.Member, cancellationToken);
        var council1GroupCount = Math.Min(3, groups.Count);
        for (var i = 0; i < council1GroupCount; i++)
            await EnsureCouncilGroupAsync(council1.Id, groups[i].Id, i + 1, cancellationToken);

        var council2 = await EnsureCouncilAsync(
            "SU26_HD02", semester.Id, training.Id, lecturer2.Id, lecturer3.Id,
            CouncilType.Defense1, CouncilStatus.Active, cancellationToken);
        await EnsureCouncilMemberAsync(council2.Id, lecturer2.Id, CouncilMemberRole.Chairman, cancellationToken);
        await EnsureCouncilMemberAsync(council2.Id, lecturer3.Id, CouncilMemberRole.Secretary, cancellationToken);
        await EnsureCouncilMemberAsync(council2.Id, lecturer1.Id, CouncilMemberRole.Member, cancellationToken);
        for (var i = council1GroupCount; i < groups.Count; i++)
            await EnsureCouncilGroupAsync(council2.Id, groups[i].Id, i - council1GroupCount + 1, cancellationToken);

        var defenseRound = await EnsureDefenseRoundAsync(
            "SU26_BV_DOT1", "Đợt bảo vệ lần 1 — Summer 2026", semester.Id, training.Id,
            new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 14), DefenseRoundStatus.InProgress, cancellationToken);
        var defenseRound2 = await EnsureDefenseRoundAsync(
            "SU26_BV_DOT2", "Đợt bảo vệ lần 2 — Summer 2026", semester.Id, training.Id,
            new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 21), DefenseRoundStatus.Published, cancellationToken);

        var defensePlans = new (string Code, int RoundId, int CouncilId, int GroupIndex, DateTime Date, int Slot, string Room, bool Locked)[]
        {
            ("SU26_BV1_S01", defenseRound.Id, council1.Id, 0, new DateTime(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc), 1, "Hội trường A", false),
            ("SU26_BV1_S02", defenseRound.Id, council1.Id, 1, new DateTime(2026, 8, 10, 10, 30, 0, DateTimeKind.Utc), 2, "Hội trường A", false),
            ("SU26_BV1_S03", defenseRound.Id, council1.Id, 2, new DateTime(2026, 8, 11, 8, 0, 0, DateTimeKind.Utc), 1, "Hội trường B", false),
            ("SU26_BV1_S04", defenseRound.Id, council2.Id, 3, new DateTime(2026, 8, 11, 13, 30, 0, DateTimeKind.Utc), 2, "Hội trường B", false),
            ("SU26_BV1_S05", defenseRound.Id, council2.Id, 4, new DateTime(2026, 8, 12, 8, 0, 0, DateTimeKind.Utc), 1, "Phòng hội thảo C", false),
            ("SU26_BV1_S06", defenseRound.Id, council2.Id, 5, new DateTime(2026, 8, 12, 13, 30, 0, DateTimeKind.Utc), 2, "Phòng hội thảo C", false),
            ("SU26_BV2_S01", defenseRound2.Id, council1.Id, 0, new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc), 1, "Hội trường A", false),
            ("SU26_BV2_S02", defenseRound2.Id, council2.Id, Math.Min(3, groups.Count - 1), new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc), 1, "Hội trường B", false)
        };

        foreach (var plan in defensePlans)
        {
            if (plan.GroupIndex < 0 || plan.GroupIndex >= groups.Count) continue;
            await EnsureDefenseSessionAsync(
                plan.Code, plan.RoundId, plan.CouncilId, groups[plan.GroupIndex].Id,
                plan.Date, plan.Slot, plan.Room, training.Id, plan.Locked, cancellationToken);
            await EnsureGroupResultAsync(groups[plan.GroupIndex].Id, semester.Id, cancellationToken);
        }

        // Sample scores on first defense session for realism.
        var firstDefense = await dbContext.DefenseSessions.SingleAsync(x => x.Code == "SU26_BV1_S01", cancellationToken);
        var firstGroupStudents = studentsByGroup[groups[0].Id].Take(4).ToList();
        foreach (var lecturer in lecturers)
        {
            foreach (var student in firstGroupStudents)
            {
                await EnsureScoreAsync(firstDefense.Id, lecturer.Id, student.Id, 7.5m + (student.Id % 20) / 10m, now, cancellationToken);
            }
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = lecturer1.UserId,
            Action = "SEED_LECTURER_WORKFLOW_DATA",
            EntityType = "LecturerWorkflow",
            EntityId = semester.Id,
            NewValue = $"groups={groups.Count}; reviewSessions={sessionPlans.Length}; defenseSessions={defensePlans.Length}",
            UserAgent = "ApplicationStartup",
            CreatedAt = now
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task EnsurePublicationAsync(
        int semesterId, ReviewType type, DateOnly weekStart, int trainingId,
        string subject, string body, DateTime publishedAt, CancellationToken cancellationToken)
    {
        if (await dbContext.ReviewSchedulePublications.AnyAsync(
                x => x.SemesterId == semesterId && x.ReviewType == type && x.WeekStartDate == weekStart,
                cancellationToken))
            return;
        dbContext.ReviewSchedulePublications.Add(new ReviewSchedulePublication
        {
            SemesterId = semesterId,
            ReviewType = type,
            WeekStartDate = weekStart,
            PublishedByTrainingDepartmentId = trainingId,
            PublishedAt = publishedAt,
            Subject = subject,
            Body = body
        });
    }

    private async Task<Council> EnsureCouncilAsync(
        string code, int semesterId, int trainingId, int chairmanId, int secretaryId,
        CouncilType type, CouncilStatus status, CancellationToken cancellationToken)
    {
        var council = await dbContext.Councils.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (council is null)
        {
            council = new Council
            {
                Code = code,
                SemesterId = semesterId,
                ManagedByTrainingDepartmentId = trainingId,
                ChairmanId = chairmanId,
                SecretaryId = secretaryId,
                Type = type,
                Status = status
            };
            dbContext.Councils.Add(council);
            await dbContext.SaveChangesAsync(cancellationToken);
            return council;
        }

        council.ChairmanId = chairmanId;
        council.SecretaryId = secretaryId;
        council.Status = status;
        await dbContext.SaveChangesAsync(cancellationToken);
        return council;
    }

    private async Task<DefenseRound> EnsureDefenseRoundAsync(
        string code, string name, int semesterId, int trainingId,
        DateOnly start, DateOnly end, DefenseRoundStatus status, CancellationToken cancellationToken)
    {
        var round = await dbContext.DefenseRounds.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (round is null)
        {
            round = new DefenseRound
            {
                Code = code,
                Name = name,
                SemesterId = semesterId,
                Type = CouncilType.Defense1,
                StartDate = start,
                EndDate = end,
                Status = status,
                CreatedByTrainingDepartmentId = trainingId
            };
            dbContext.DefenseRounds.Add(round);
            await dbContext.SaveChangesAsync(cancellationToken);
            return round;
        }

        round.Name = name;
        round.Status = status;
        round.StartDate = start;
        round.EndDate = end;
        await dbContext.SaveChangesAsync(cancellationToken);
        return round;
    }

    private async Task EnsureScoreAsync(
        int defenseSessionId, int scorerId, int studentId, decimal value, DateTime now,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Scores.AnyAsync(
                x => x.DefenseSessionId == defenseSessionId && x.ScorerId == scorerId && x.StudentId == studentId,
                cancellationToken))
            return;
        dbContext.Scores.Add(new Score
        {
            DefenseSessionId = defenseSessionId,
            ScorerId = scorerId,
            StudentId = studentId,
            ScoreType = ScoreType.BaoVe,
            ScoreValue = Math.Round(Math.Clamp(value, 0m, 10m), 1),
            SubmittedAt = now.AddDays(-1),
            IsLocked = false
        });
    }

    private async Task RenameSessionCodeAsync(string from, string to, CancellationToken cancellationToken)    {
        var session = await dbContext.ReviewSessions.FirstOrDefaultAsync(x => x.Code == from, cancellationToken);
        if (session is null) return;
        if (!await dbContext.ReviewSessions.AnyAsync(x => x.Code == to, cancellationToken))
            session.Code = to;
    }

    private async Task RenameCouncilCodeAsync(string from, string to, CancellationToken cancellationToken)
    {
        var council = await dbContext.Councils.FirstOrDefaultAsync(x => x.Code == from, cancellationToken);
        if (council is null) return;
        if (!await dbContext.Councils.AnyAsync(x => x.Code == to, cancellationToken))
            council.Code = to;
    }

    private async Task RenameDefenseRoundCodeAsync(
        string from, string to, string name, CancellationToken cancellationToken)
    {
        var round = await dbContext.DefenseRounds.FirstOrDefaultAsync(x => x.Code == from, cancellationToken);
        if (round is null) return;
        if (!await dbContext.DefenseRounds.AnyAsync(x => x.Code == to, cancellationToken))
        {
            round.Code = to;
            round.Name = name;
        }
    }

    private async Task RenameDefenseSessionCodeAsync(string from, string to, CancellationToken cancellationToken)
    {
        var session = await dbContext.DefenseSessions.FirstOrDefaultAsync(x => x.Code == from, cancellationToken);
        if (session is null) return;
        if (!await dbContext.DefenseSessions.AnyAsync(x => x.Code == to, cancellationToken))
            session.Code = to;
    }

    private async Task<ReviewSession> EnsurePublishedReviewSessionAsync(
        string code,
        int semesterId,
        ReviewType type,
        int dayOfWeek,
        int slot,
        string room,
        DateTime sessionDate,
        int reviewer1Id,
        int reviewer2Id,
        int trainingId,
        DateTime publishedAt,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.ReviewSessions.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (session is null)
        {
            session = new ReviewSession
            {
                Code = code,
                SemesterId = semesterId,
                DayOfWeek = dayOfWeek,
                Slot = slot,
                Room = room,
                Type = type,
                Reviewer1Id = reviewer1Id,
                Reviewer2Id = reviewer2Id,
                SessionDate = sessionDate,
                Status = ReviewSessionStatus.Published,
                PublishedAt = publishedAt,
                PublishedByTrainingDepartmentId = trainingId
            };
            dbContext.ReviewSessions.Add(session);
            await dbContext.SaveChangesAsync(cancellationToken);
            return session;
        }

        session.Status = ReviewSessionStatus.Published;
        session.Room = room;
        session.Reviewer1Id = reviewer1Id;
        session.Reviewer2Id = reviewer2Id;
        session.SessionDate = sessionDate;
        session.PublishedAt ??= publishedAt;
        session.PublishedByTrainingDepartmentId ??= trainingId;
        await dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    private async Task EnsureGroupReviewSlotAsync(
        int sessionId, int groupId, int position, CancellationToken cancellationToken)
    {
        if (await dbContext.GroupReviewSlots.AnyAsync(
                x => x.SessionId == sessionId && x.GroupId == groupId, cancellationToken))
            return;
        dbContext.GroupReviewSlots.Add(new GroupReviewSlot
        {
            SessionId = sessionId,
            GroupId = groupId,
            GroupPosition = position,
            Status = ReviewGroupStatus.Scheduled
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureReviewCommentAsync(
        int sessionId, int groupId, int authorUserId, string content, DateTime createdAt,
        CancellationToken cancellationToken)
    {
        if (await dbContext.ReviewComments.AnyAsync(
                x => x.SessionId == sessionId && x.GroupId == groupId && x.Content == content,
                cancellationToken))
            return;
        dbContext.ReviewComments.Add(new ReviewComment
        {
            SessionId = sessionId,
            GroupId = groupId,
            AuthorUserId = authorUserId,
            Content = content,
            CreatedAt = createdAt
        });
    }

    private async Task EnsureReviewAttendancesAsync(
        int sessionId, IEnumerable<Student> students, int lecturerId, DateTime now,
        CancellationToken cancellationToken)
    {
        if (await dbContext.ReviewAttendances.AnyAsync(x => x.SessionId == sessionId, cancellationToken))
            return;
        dbContext.ReviewAttendances.AddRange(students.Select(s => new ReviewAttendance
        {
            SessionId = sessionId,
            StudentId = s.Id,
            IsPresent = true,
            Note = "Có mặt đúng giờ",
            MarkedByLecturerId = lecturerId,
            MarkedAt = now.AddDays(-5)
        }));
    }

    private async Task EnsureCouncilGroupAsync(
        int councilId, int groupId, int position, CancellationToken cancellationToken)
    {
        if (await dbContext.CouncilGroups.AnyAsync(
                x => x.CouncilId == councilId && x.GroupId == groupId, cancellationToken))
            return;
        dbContext.CouncilGroups.Add(new CouncilGroup
        {
            CouncilId = councilId, GroupId = groupId, Position = position
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDefenseSessionAsync(
        string code,
        int defenseRoundId,
        int councilId,
        int groupId,
        DateTime sessionDate,
        int slot,
        string room,
        int trainingId,
        bool locked,
        CancellationToken cancellationToken)
    {
        var defense = await dbContext.DefenseSessions.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);
        if (defense is null)
        {
            dbContext.DefenseSessions.Add(new DefenseSession
            {
                Code = code,
                DefenseRoundId = defenseRoundId,
                CouncilId = councilId,
                GroupId = groupId,
                SessionDate = sessionDate,
                Slot = slot,
                Room = room,
                AssignedByTrainingDepartmentId = trainingId,
                StartedAt = null,
                EndedAt = null,
                IsLocked = locked
            });
            return;
        }

        defense.CouncilId = councilId;
        defense.GroupId = groupId;
        defense.SessionDate = sessionDate;
        defense.Slot = slot;
        defense.Room = room;
        defense.IsLocked = locked;
        defense.EndedAt = null;
    }

    private async Task EnsureGroupResultAsync(int groupId, int semesterId, CancellationToken cancellationToken)
    {
        if (await dbContext.GroupResults.AnyAsync(
                x => x.GroupId == groupId && x.SemesterId == semesterId, cancellationToken))
            return;
        dbContext.GroupResults.Add(new GroupResult
        {
            GroupId = groupId,
            SemesterId = semesterId,
            FinalResult = FinalResult.Pending
        });
    }

    private async Task<ReviewRound> EnsureReviewRoundAsync(        int semesterId,
        ReviewType type,
        DateOnly weekStart,
        ReviewRoundStatus status,
        int moderatorId,
        DateTime createdAt,
        DateTime? openedAt,
        DateTime? closedAt,
        CancellationToken cancellationToken)
    {
        var round = await dbContext.ReviewRounds.FirstOrDefaultAsync(
            x => x.SemesterId == semesterId && x.Type == type && x.WeekStartDate == weekStart,
            cancellationToken);
        if (round is null)
        {
            round = new ReviewRound
            {
                SemesterId = semesterId,
                Type = type,
                WeekStartDate = weekStart,
                WeekEndDate = weekStart.AddDays(5),
                Status = status,
                CreatedByModeratorId = moderatorId,
                CreatedAt = createdAt,
                OpenedAt = openedAt,
                ClosedAt = closedAt
            };
            dbContext.ReviewRounds.Add(round);
            await dbContext.SaveChangesAsync(cancellationToken);
            return round;
        }

        round.Status = status;
        round.OpenedAt ??= openedAt;
        if (status is ReviewRoundStatus.Closed or ReviewRoundStatus.Published)
            round.ClosedAt ??= closedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        return round;
    }

    private async Task EnsureStudentSlotsAsync(
        int roundId, int groupId, int studentId, int dayOfWeek, int slot, CancellationToken cancellationToken)
    {
        // Leaders need 5 slots; seed a full set around the anchor slot for the test group.
        var desired = new[]
        {
            (dayOfWeek, slot),
            (dayOfWeek, Math.Min(5, slot + 1)),
            ((dayOfWeek % 6) + 1, slot),
            ((dayOfWeek % 6) + 1, Math.Min(5, slot + 1)),
            (((dayOfWeek + 1) % 6) + 1, slot)
        }.Distinct().Take(5).ToArray();

        var existing = await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == roundId && x.GroupId == groupId)
            .ToListAsync(cancellationToken);
        if (existing.Count >= 5) return;

        dbContext.StudentSlotRegistrations.RemoveRange(existing);
        dbContext.StudentSlotRegistrations.AddRange(desired.Select(x => new StudentSlotRegistration
        {
            ReviewRoundId = roundId,
            GroupId = groupId,
            RegisteredByStudentId = studentId,
            DayOfWeek = x.Item1,
            Slot = x.Item2
        }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureLecturerAvailabilityAsync(
        ReviewRound round,
        Lecturer lecturer,
        bool submitted,
        int dayOfWeek,
        int slot,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var desired = new[] { (dayOfWeek, slot), (dayOfWeek, Math.Min(5, slot + 1)) };
        foreach (var (dow, s) in desired)
        {
            var exists = await dbContext.ReviewAvailabilities.AnyAsync(
                x => x.ReviewRoundId == round.Id && x.LecturerId == lecturer.Id &&
                     x.DayOfWeek == dow && x.Slot == s, cancellationToken);
            if (exists) continue;
            dbContext.ReviewAvailabilities.Add(new ReviewAvailability
            {
                ReviewRoundId = round.Id,
                SemesterId = round.SemesterId,
                LecturerId = lecturer.Id,
                WeekStartDate = round.WeekStartDate,
                DayOfWeek = dow,
                Slot = s,
                CreatedAt = now.AddDays(-20)
            });
        }

        var submission = await dbContext.ReviewAvailabilitySubmissions.SingleOrDefaultAsync(
            x => x.ReviewRoundId == round.Id && x.LecturerId == lecturer.Id, cancellationToken);
        if (submission is null)
        {
            dbContext.ReviewAvailabilitySubmissions.Add(new ReviewAvailabilitySubmission
            {
                ReviewRoundId = round.Id,
                SemesterId = round.SemesterId,
                LecturerId = lecturer.Id,
                WeekStartDate = round.WeekStartDate,
                UpdatedAt = now.AddDays(-19),
                SubmittedAt = submitted ? now.AddDays(-18) : null
            });
        }
        else if (submitted && submission.SubmittedAt is null)
        {
            submission.SubmittedAt = now.AddDays(-18);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureChecklistSubmissionAsync(
        int sessionId,
        int groupId,
        int reviewerId,
        ReviewType type,
        ReviewSubmissionStatus status,
        DateTime now,
        string workProductVersion,
        CancellationToken cancellationToken)
    {
        var submission = await dbContext.ReviewChecklistSubmissions.FirstOrDefaultAsync(
            x => x.SessionId == sessionId && x.GroupId == groupId && x.ReviewerId == reviewerId,
            cancellationToken);
        if (submission is null)
        {
            submission = new ReviewChecklistSubmission
            {
                SessionId = sessionId,
                GroupId = groupId,
                ReviewerId = reviewerId,
                Type = type,
                Status = status,
                WorkProductVersion = workProductVersion,
                WorkProductSize = "4.5 MB",
                EffortHours = status == ReviewSubmissionStatus.Submitted ? 140m : 95m,
                ReviewerComment = status == ReviewSubmissionStatus.Draft
                    ? "Nháp nhận xét — cần bổ sung minh chứng triển khai."
                    : "Nhóm hoàn thành đủ tiêu chí Review; có thể chuyển sang giai đoạn tiếp theo.",
                Suggestion = "Bổ sung dashboard giám sát và kịch bản kiểm thử hồi quy.",
                ResultText = status == ReviewSubmissionStatus.Submitted ? "Pass" : null,
                LastSavedAt = now.AddDays(-4),
                SubmittedAt = status == ReviewSubmissionStatus.Submitted ? now.AddDays(-3) : null
            };
            dbContext.ReviewChecklistSubmissions.Add(submission);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (!(submission.Status == ReviewSubmissionStatus.Submitted &&
                   status == ReviewSubmissionStatus.Draft))
        {
            submission.Status = status;
            submission.WorkProductVersion = workProductVersion;
            submission.LastSavedAt = now.AddDays(-4);
            if (status == ReviewSubmissionStatus.Submitted)
                submission.SubmittedAt ??= now.AddDays(-3);
        }

        if (!await dbContext.ReviewChecklistItemResponses.AnyAsync(
                x => x.SubmissionId == submission.Id, cancellationToken))
        {
            dbContext.ReviewChecklistItemResponses.AddRange(
                new ReviewChecklistItemResponse
                {
                    SubmissionId = submission.Id,
                    ItemKey = "SCOPE",
                    Answer = ReviewChecklistAnswer.Yes,
                    Comment = "Phạm vi đề tài rõ ràng, bám milestone kỳ này.",
                    UpdatedAt = now.AddDays(-4)
                },
                new ReviewChecklistItemResponse
                {
                    SubmissionId = submission.Id,
                    ItemKey = "DEMO",
                    Answer = ReviewChecklistAnswer.No,
                    Comment = "Demo còn thiếu luồng xử lý ngoại lệ.",
                    UpdatedAt = now.AddDays(-4)
                });
        }
    }

    private async Task EnsureCouncilMemberAsync(
        int councilId, int lecturerId, CouncilMemberRole role, CancellationToken cancellationToken)
    {
        if (!await dbContext.CouncilMembers.AnyAsync(
                x => x.CouncilId == councilId && x.LecturerId == lecturerId, cancellationToken))
        {
            dbContext.CouncilMembers.Add(new CouncilMember
            {
                CouncilId = councilId, LecturerId = lecturerId, Role = role
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed record StudentSeed(string FullName, string Code, string Email, bool IsLeader);

    private sealed record GroupSeed(
        string GroupCode,
        string TopicCode,
        string NameVn,
        string NameEn,
        int SupervisorLecturerId,
        StudentSeed[] Students);
}

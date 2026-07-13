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
        if (seedDevelopmentData) await SeedDevelopmentDataAsync(cancellationToken);
    }

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
            user.Username = username;
            user.Email = email;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("12345", workFactor: 12);
            user.Role = UserRole.TrainingDepartment;
            user.IsActive = true;
            user.LockedUntil = null;
            user.FailedLoginAttempts = 0;
            user.UpdatedAt = DateTime.UtcNow;
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
        var accounts = new[]
        {
            ("test.training", "test.training@cpms.local", UserRole.TrainingDepartment),
            ("test.lecturer", "test.lecturer@cpms.local", UserRole.Lecturer),
            ("test.student", "test.student@cpms.local", UserRole.Student)
        };
        foreach (var account in accounts)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var user = await dbContext.Users.FirstOrDefaultAsync(x =>
                x.Username == account.Item1 || x.Email == account.Item2, cancellationToken);
            if (user is null)
            {
                user = new User
                {
                    Username = account.Item1,
                    Email = account.Item2,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123456", workFactor: 12),
                    Role = account.Item3,
                    IsActive = true
                };
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                user.Username = account.Item1;
                user.Email = account.Item2;
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123456", workFactor: 12);
                user.Role = account.Item3;
                user.IsActive = true;
                user.LockedUntil = null;
                user.FailedLoginAttempts = 0;
                user.UpdatedAt = DateTime.UtcNow;
            }
            if (account.Item3 == UserRole.TrainingDepartment &&
                !await dbContext.TrainingDepartments.AnyAsync(x => x.UserId == user.Id, cancellationToken))
                dbContext.TrainingDepartments.Add(new TrainingDepartment
                {
                    UserId = user.Id, DepartmentName = "Training Dept", StaffCode = "TD_TEST", Position = "Officer"
                });
            else if (account.Item3 == UserRole.Lecturer &&
                     !await dbContext.Lecturers.AnyAsync(x => x.UserId == user.Id, cancellationToken))
                dbContext.Lecturers.Add(new Lecturer
                {
                    UserId = user.Id, Code = "LEC_TEST", FullName = "Dr. Test Lecturer", Department = "SWD"
                });
            else if (account.Item3 == UserRole.Student &&
                     !await dbContext.Students.AnyAsync(x => x.UserId == user.Id, cancellationToken))
                dbContext.Students.Add(new Student
                {
                    UserId = user.Id, Code = "SE190000", FullName = "Test Student",
                    ClassCode = "SE1901", Major = "SWD", IsLeader = false
                });
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private async Task SeedAcademicDataAsync(CancellationToken cancellationToken)
    {
        var lecturer = await dbContext.Lecturers.SingleAsync(x => x.Code == "LEC_TEST", cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var semester = await dbContext.Semesters.FirstOrDefaultAsync(x => x.IsActive, cancellationToken)
            ?? await dbContext.Semesters.FirstOrDefaultAsync(x => x.Code == "SU2026", cancellationToken);
        if (semester is null)
        {
            semester = new Semester
            {
                Code = "SU2026", Name = "Summer 2026", AcademicYear = "2025-2026",
                StartDate = new DateOnly(2026, 5, 1), EndDate = new DateOnly(2026, 8, 31), IsActive = true
            };
            dbContext.Semesters.Add(semester);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else semester.IsActive = true;
        const string title = "Smart Home Inventory";
        var topic = await dbContext.Topics.FirstOrDefaultAsync(x =>
            x.SemesterId == semester.Id && (x.NameVn == title || x.NameEn == title), cancellationToken);
        if (topic is null)
        {
            topic = new Topic
            {
                Code = BuildTopicCode(semester.Code), NameVn = title, NameEn = title, SemesterId = semester.Id
            };
            dbContext.Topics.Add(topic);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        var group = await dbContext.CapstoneGroups.FirstOrDefaultAsync(x =>
            x.SemesterId == semester.Id && x.TopicId == topic.Id, cancellationToken);
        if (group is null)
        {
            group = new CapstoneGroup
            {
                Code = "TEST_GROUP_SMART_HOME", TopicId = topic.Id, SemesterId = semester.Id,
                LecturerId = lecturer.Id, Status = GroupStatus.Active
            };
            dbContext.CapstoneGroups.Add(group);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else { group.LecturerId = lecturer.Id; group.Status = GroupStatus.Active; }
        var students = new[]
        {
            new StudentSeed("Huỳnh Nhật Thịnh", "SE194673", "duongduy12317@gmail.com", false),
            new StudentSeed("Phạm Thị Ánh", "SE182446", "thanhduykx@gmail.com", false),
            new StudentSeed("Dương Thành Thanh Duy", "SS180745", "victornhatthinh@gmail.com", true),
            new StudentSeed("Trương Đinh Hồng Nhung", "SS194356", "a@gmail.com", false),
            new StudentSeed("Vũ Minh Quang", "SE192755", "acc@gmail.com", false),
            new StudentSeed("Nguyễn Minh Thành", "SE191034", "ab@gmail.com", false)
        };
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Test@123456", workFactor: 12);
        foreach (var seed in students)
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
                    Username = username, Email = seed.Email, PasswordHash = passwordHash,
                    Role = UserRole.Student, IsActive = true
                };
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else if (user.Role != UserRole.Student)
                throw new InvalidOperationException($"Test student {seed.Code} conflicts with a non-student account.");
            else
            {
                user.Username = username; user.Email = seed.Email; user.PasswordHash = passwordHash;
                user.IsActive = true; user.LockedUntil = null; user.FailedLoginAttempts = 0;
            }
            if (student is null)
            {
                student = new Student
                {
                    UserId = user.Id, Code = seed.Code, FullName = seed.FullName,
                    ClassCode = "EXE101", Major = "Unspecified"
                };
                dbContext.Students.Add(student);
            }
            student.FullName = seed.FullName;
            student.GroupId = group.Id;
            student.IsLeader = seed.IsLeader;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string BuildTopicCode(string semesterCode)
    {
        var code = $"TEST_TOPIC_{semesterCode}_SMART_HOME";
        return code.Length <= 50 ? code : code[..50];
    }
    private sealed record StudentSeed(string FullName, string Code, string Email, bool IsLeader);
}

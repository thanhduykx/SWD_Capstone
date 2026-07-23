using System.Text.Json;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using CPMS.DAL.Entities;
using Microsoft.EntityFrameworkCore;

const string semesterCode = "SU26";
const int targetDayOfWeek = 4;
const int targetSlot = 3;
var targetDate = new DateTime(2026, 7, 23, 0, 0, 0, DateTimeKind.Utc);
string[] lecturerCodes = ["GV24001", "GV24002", "GV24003", "GV24004"];
string[] allQaGroupCodes =
[
    "QA_SLOT3_TEAM_01", "QA_SLOT3_TEAM_02", "QA_SLOT3_TEAM_03", "QA_SLOT3_TEAM_04"
];
TeamSpec[] activeTeams =
[
    new(1, "QA_SLOT3_TEAM_01", "QA_SLOT3_TOPIC_01", "Hệ thống quản lý lịch học thông minh",
    [
        new("qa.slot3.leader1@fpt.edu.vn", "SEQA2301", "Nguyễn Minh Anh", true),
        new("qa.slot3.team1.member2@fpt.edu.vn", "SEQA2312", "Trần Quốc Bảo", false),
        new("qa.slot3.team1.member3@fpt.edu.vn", "SEQA2313", "Lê Hoàng Duy", false),
        new("qa.slot3.team1.member4@fpt.edu.vn", "SEQA2314", "Phạm Gia Hân", false),
        new("qa.slot3.team1.member5@fpt.edu.vn", "SEQA2315", "Võ Minh Khang", false),
        new("qa.slot3.team1.member6@fpt.edu.vn", "SEQA2316", "Đỗ Ngọc Linh", false)
    ]),
    new(2, "QA_SLOT3_TEAM_02", "QA_SLOT3_TOPIC_02", "Nền tảng theo dõi sức khỏe sinh viên",
    [
        new("qa.slot3.leader2@fpt.edu.vn", "SEQA2302", "Trần Gia Huy", true),
        new("qa.slot3.team2.member2@fpt.edu.vn", "SEQA2322", "Nguyễn Hải Nam", false),
        new("qa.slot3.team2.member3@fpt.edu.vn", "SEQA2323", "Bùi Thu Phương", false),
        new("qa.slot3.team2.member4@fpt.edu.vn", "SEQA2324", "Hoàng Đức Anh", false),
        new("qa.slot3.team2.member5@fpt.edu.vn", "SEQA2325", "Phan Khánh Vy", false),
        new("qa.slot3.team2.member6@fpt.edu.vn", "SEQA2326", "Đặng Tuấn Kiệt", false)
    ])
];

var password = Environment.GetEnvironmentVariable("CPMS_QA_PASSWORD");
if (string.IsNullOrWhiteSpace(password))
    throw new InvalidOperationException("CPMS_QA_PASSWORD is required.");

try
{
    var root = FindRepositoryRoot();
    var options = new DbContextOptionsBuilder<CpmsDbContext>()
        .UseNpgsql(ReadConnectionString(root))
        .UseSnakeCaseNamingConvention()
        .Options;
    await using var db = new CpmsDbContext(options);
    await db.Database.OpenConnectionAsync();
    await using var transaction = await db.Database.BeginTransactionAsync();

    var semester = await db.Semesters.SingleAsync(x => x.Code == semesterCode);
    var round = await db.ReviewRounds.SingleAsync(x =>
        x.SemesterId == semester.Id &&
        x.Type == ReviewType.Review2 &&
        x.WeekStartDate == new DateOnly(2026, 7, 20));
    var moderatorId = await db.TrainingDepartments.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
    var lecturers = await db.Lecturers
        .Where(x => lecturerCodes.Contains(x.Code))
        .OrderBy(x => x.Code)
        .ToArrayAsync();
    if (lecturers.Length != lecturerCodes.Length)
        throw new InvalidOperationException("Không tìm thấy đủ GV24001-GV24004.");

    foreach (var lecturer in lecturers)
    {
        var user = await db.Users.SingleAsync(x => x.Id == lecturer.UserId);
        ActivateAccount(user, password);
        if (!await db.ReviewAvailabilities.AnyAsync(x =>
                x.ReviewRoundId == round.Id && x.LecturerId == lecturer.Id &&
                x.DayOfWeek == targetDayOfWeek && x.Slot == targetSlot))
        {
            db.ReviewAvailabilities.Add(new ReviewAvailability
            {
                ReviewRoundId = round.Id,
                SemesterId = semester.Id,
                LecturerId = lecturer.Id,
                WeekStartDate = round.WeekStartDate,
                DayOfWeek = targetDayOfWeek,
                Slot = targetSlot
            });
        }

        var submission = await db.ReviewAvailabilitySubmissions.SingleOrDefaultAsync(x =>
            x.ReviewRoundId == round.Id && x.LecturerId == lecturer.Id);
        if (submission is null)
        {
            db.ReviewAvailabilitySubmissions.Add(new ReviewAvailabilitySubmission
            {
                ReviewRoundId = round.Id,
                SemesterId = semester.Id,
                LecturerId = lecturer.Id,
                WeekStartDate = round.WeekStartDate,
                UpdatedAt = DateTime.UtcNow,
                SubmittedAt = DateTime.UtcNow
            });
        }
        else
        {
            submission.SubmittedAt ??= DateTime.UtcNow;
            submission.UpdatedAt = DateTime.UtcNow;
        }
    }
    await db.SaveChangesAsync();

    var seededTeams = new List<SeededTeam>();
    foreach (var spec in activeTeams)
    {
        var topic = await db.Topics.SingleOrDefaultAsync(x => x.Code == spec.TopicCode);
        if (topic is null)
        {
            topic = new Topic
            {
                Code = spec.TopicCode,
                NameVn = spec.TopicName,
                NameEn = $"QA Slot 3 Project {spec.Number}",
                SemesterId = semester.Id
            };
            db.Topics.Add(topic);
            await db.SaveChangesAsync();
        }

        var group = await db.CapstoneGroups.SingleOrDefaultAsync(x =>
            x.Code == spec.GroupCode && x.SemesterId == semester.Id);
        if (group is null)
        {
            group = new CapstoneGroup
            {
                Code = spec.GroupCode,
                TopicId = topic.Id,
                SemesterId = semester.Id,
                Status = GroupStatus.Active
            };
            db.CapstoneGroups.Add(group);
            await db.SaveChangesAsync();
        }
        else
        {
            group.TopicId = topic.Id;
            group.Status = GroupStatus.Active;
        }

        var members = new List<(Student Student, User User)>();
        foreach (var memberSpec in spec.Members)
        {
            var user = await db.Users.SingleOrDefaultAsync(x =>
                x.Username == memberSpec.Email || x.Email == memberSpec.Email);
            if (user is null)
            {
                user = new User
                {
                    Username = memberSpec.Email,
                    Email = memberSpec.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
                    Role = UserRole.Student,
                    IsActive = true
                };
                db.Users.Add(user);
                await db.SaveChangesAsync();
            }
            else
            {
                if (user.Role != UserRole.Student)
                    throw new InvalidOperationException($"{memberSpec.Email} không phải tài khoản sinh viên.");
                user.Username = memberSpec.Email;
                user.Email = memberSpec.Email;
                ActivateAccount(user, password);
            }

            var student = await db.Students.SingleOrDefaultAsync(x =>
                x.UserId == user.Id || x.Code == memberSpec.StudentCode);
            if (student is null)
            {
                student = new Student
                {
                    UserId = user.Id,
                    Code = memberSpec.StudentCode,
                    FullName = memberSpec.FullName,
                    ClassCode = "SE1946",
                    Batch = "K19",
                    Major = "Software Engineering",
                    GroupId = group.Id,
                    IsLeader = memberSpec.IsLeader
                };
                db.Students.Add(student);
            }
            else
            {
                if (student.UserId != user.Id)
                    throw new InvalidOperationException($"Mã {memberSpec.StudentCode} thuộc tài khoản khác.");
                student.Code = memberSpec.StudentCode;
                student.FullName = memberSpec.FullName;
                student.GroupId = group.Id;
                student.IsLeader = memberSpec.IsLeader;
            }
            await db.SaveChangesAsync();
            members.Add((student, user));
        }

        int[] expectedStudentIds = members.Select(x => x.Student.Id).ToArray();
        var unexpectedMembers = await db.Students
            .Where(x => x.GroupId == group.Id && !expectedStudentIds.Contains(x.Id))
            .ToArrayAsync();
        foreach (var unexpected in unexpectedMembers)
        {
            unexpected.GroupId = null;
            unexpected.IsLeader = false;
        }
        seededTeams.Add(new SeededTeam(group, members));
    }
    await db.SaveChangesAsync();

    var allQaGroups = await db.CapstoneGroups
        .Where(x => x.SemesterId == semester.Id && allQaGroupCodes.Contains(x.Code))
        .ToArrayAsync();
    int[] allQaGroupIds = allQaGroups.Select(x => x.Id).ToArray();
    int[] review2SessionIds = await db.ReviewSessions
        .Where(x => x.SemesterId == semester.Id && x.Type == ReviewType.Review2)
        .Select(x => x.Id)
        .ToArrayAsync();

    db.ReviewChecklistSubmissions.RemoveRange(await db.ReviewChecklistSubmissions
        .Where(x => allQaGroupIds.Contains(x.GroupId) && review2SessionIds.Contains(x.SessionId))
        .ToArrayAsync());
    db.GroupReviewSlots.RemoveRange(await db.GroupReviewSlots
        .Where(x => allQaGroupIds.Contains(x.GroupId) && review2SessionIds.Contains(x.SessionId))
        .ToArrayAsync());
    db.StudentSlotRegistrations.RemoveRange(await db.StudentSlotRegistrations
        .Where(x => x.ReviewRoundId == round.Id && allQaGroupIds.Contains(x.GroupId))
        .ToArrayAsync());
    await db.SaveChangesAsync();

    var selectedAvailability = new[]
    {
        (Day: 1, Slot: 1), (Day: 2, Slot: 2), (Day: 3, Slot: 3),
        (Day: targetDayOfWeek, Slot: targetSlot), (Day: 5, Slot: 2)
    };
    foreach (var team in seededTeams)
    {
        var leader = team.Members.Single(x => x.Student.IsLeader).Student;
        db.StudentSlotRegistrations.AddRange(selectedAvailability.Select(x => new StudentSlotRegistration
        {
            ReviewRoundId = round.Id,
            GroupId = team.Group.Id,
            RegisteredByStudentId = leader.Id,
            DayOfWeek = x.Day,
            Slot = x.Slot
        }));
    }

    var session = await EnsureSessionAsync(
        db, "SU26_R2_SLOT3_4TEAM_A", semester.Id, targetDate, "B206-QA", moderatorId);
    await db.SaveChangesAsync();
    await ReplacePanelAsync(db, session.Id, lecturers);
    await AddGroupsAsync(db, session, seededTeams.Select(x => x.Group).ToArray(), lecturers);
    await db.SaveChangesAsync();

    int[] targetSessionIds = await db.ReviewSessions
        .Where(x => x.SemesterId == semester.Id && x.Type == ReviewType.Review2 &&
                    x.SessionDate == targetDate && x.Slot == targetSlot)
        .Select(x => x.Id)
        .ToArrayAsync();
    int totalGroupsInSlot = await db.GroupReviewSlots
        .Where(x => targetSessionIds.Contains(x.SessionId))
        .Select(x => x.GroupId)
        .Distinct()
        .CountAsync();
    int[] activeQaGroupIds = seededTeams.Select(t => t.Group.Id).ToArray();
    int qaScheduledCount = await db.GroupReviewSlots.CountAsync(x =>
        x.SessionId == session.Id && activeQaGroupIds.Contains(x.GroupId));
    int reviewerCount = await db.ReviewSessionReviewers.CountAsync(x => x.SessionId == session.Id);
    int qaSlotRegistrationCount = await db.StudentSlotRegistrations.CountAsync(x =>
        x.ReviewRoundId == round.Id &&
        activeQaGroupIds.Contains(x.GroupId) &&
        x.DayOfWeek == targetDayOfWeek && x.Slot == targetSlot);
    var memberCounts = new List<int>();
    foreach (var team in seededTeams)
        memberCounts.Add(await db.Students.CountAsync(x => x.GroupId == team.Group.Id));

    if (totalGroupsInSlot > 3 || qaScheduledCount != 2 || reviewerCount != 4 ||
        qaSlotRegistrationCount != 2 || memberCounts.Any(count => count != 6))
    {
        throw new InvalidOperationException(
            $"Xác minh thất bại: total={totalGroupsInSlot}, qa={qaScheduledCount}, " +
            $"reviewers={reviewerCount}, registrations={qaSlotRegistrationCount}, " +
            $"members={string.Join(',', memberCounts)}.");
    }

    await transaction.CommitAsync();

    var accounts = seededTeams.SelectMany(team => team.Members.Select(member => new
    {
        groupCode = team.Group.Code,
        member.Student.Code,
        member.Student.FullName,
        member.Student.IsLeader,
        member.User.Email
    })).ToArray();
    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new
    {
        status = "verified",
        date = "2026-07-23",
        slot = 3,
        totalGroupsInSlot,
        qaGroups = seededTeams.Select(x => x.Group.Code),
        removedFromSchedule = new[] { "QA_SLOT3_TEAM_03", "QA_SLOT3_TEAM_04" },
        membersPerQaGroup = memberCounts,
        reviewerCodes = lecturerCodes,
        password,
        accounts
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"QA team seed failed; transaction was not committed. {exception}");
    return 1;
}

static void ActivateAccount(User user, string password)
{
    user.IsActive = true;
    user.FailedLoginAttempts = 0;
    user.LockedUntil = null;
    if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
}

static async Task<ReviewSession> EnsureSessionAsync(
    CpmsDbContext db, string code, int semesterId, DateTime sessionDate, string room, int moderatorId)
{
    var session = await db.ReviewSessions.SingleOrDefaultAsync(x =>
        x.Code == code && x.SemesterId == semesterId);
    if (session is null)
    {
        session = new ReviewSession
        {
            Code = code,
            SemesterId = semesterId,
            Room = room
        };
        db.ReviewSessions.Add(session);
    }
    session.DayOfWeek = targetDayOfWeek;
    session.Slot = targetSlot;
    session.Room = room;
    session.Type = ReviewType.Review2;
    session.SessionDate = sessionDate;
    session.Status = ReviewSessionStatus.Published;
    session.PublishedAt ??= DateTime.UtcNow;
    session.PublishedByTrainingDepartmentId = moderatorId;
    return session;
}

static async Task ReplacePanelAsync(
    CpmsDbContext db, int sessionId, IReadOnlyList<Lecturer> lecturers)
{
    db.ReviewSessionReviewers.RemoveRange(
        await db.ReviewSessionReviewers.Where(x => x.SessionId == sessionId).ToArrayAsync());
    db.ReviewSessionReviewers.AddRange(lecturers.Select((lecturer, index) => new ReviewSessionReviewer
    {
        SessionId = sessionId,
        LecturerId = lecturer.Id,
        Position = index + 1
    }));
}

static Task AddGroupsAsync(
    CpmsDbContext db, ReviewSession session,
    IReadOnlyList<CapstoneGroup> groups, IReadOnlyList<Lecturer> reviewers)
{
    for (var index = 0; index < groups.Count; index++)
    {
        var group = groups[index];
        db.GroupReviewSlots.Add(new GroupReviewSlot
        {
            SessionId = session.Id,
            GroupId = group.Id,
            GroupPosition = index + 1,
            ConflictFlag = false,
            Status = ReviewGroupStatus.Scheduled
        });
        db.ReviewChecklistSubmissions.AddRange(reviewers.Select(reviewer => new ReviewChecklistSubmission
        {
            SessionId = session.Id,
            GroupId = group.Id,
            ReviewerId = reviewer.Id,
            Type = ReviewType.Review2,
            Status = ReviewSubmissionStatus.Draft,
            LastSavedAt = DateTime.UtcNow
        }));
    }
    return Task.CompletedTask;
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "CPMS.sln"))) return directory.FullName;
    throw new DirectoryNotFoundException("Run this tool from inside the CPMS repository.");
}

static string ReadConnectionString(string root)
{
    var environmentValue = Environment.GetEnvironmentVariable("ConnectionStrings__CpmsDatabase");
    if (!string.IsNullOrWhiteSpace(environmentValue)) return environmentValue;
    using var json = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(root, "src", "CPMS.API", "appsettings.json")));
    return json.RootElement.GetProperty("ConnectionStrings").GetProperty("CpmsDatabase").GetString()
        ?? throw new InvalidOperationException("ConnectionStrings:CpmsDatabase is missing.");
}

internal sealed record MemberSpec(string Email, string StudentCode, string FullName, bool IsLeader);
internal sealed record TeamSpec(
    int Number, string GroupCode, string TopicCode, string TopicName, IReadOnlyList<MemberSpec> Members);
internal sealed record SeededTeam(
    CapstoneGroup Group, IReadOnlyList<(Student Student, User User)> Members);

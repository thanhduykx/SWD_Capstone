using System.Text.Json;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using CPMS.DAL.Entities;
using Microsoft.EntityFrameworkCore;

const string sessionCode = "SU26_R2_TODAY_20260723";
var sessionDate = new DateTime(2026, 7, 23, 0, 0, 0, DateTimeKind.Utc);

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

    var semester = await db.Semesters.SingleAsync(x => x.Code == "SU26");
    var round = await db.ReviewRounds.SingleAsync(x =>
        x.SemesterId == semester.Id &&
        x.Type == ReviewType.Review2 &&
        x.WeekStartDate == new DateOnly(2026, 7, 20));
    var group = await db.CapstoneGroups.SingleAsync(x =>
        x.SemesterId == semester.Id && x.Code == "GROUP_2_2_SMART_HOME_INVENTORY");
    var reviewers = await db.Lecturers
        .Where(x => x.Code == "GV24001" || x.Code == "GV24002")
        .OrderBy(x => x.Code)
        .ToArrayAsync();

    if (reviewers.Length != 2)
        throw new InvalidOperationException("The two reviewers for the target session are missing.");
    if (reviewers.Any(x => x.Id == group.LecturerId))
        throw new InvalidOperationException("A selected reviewer supervises the target group.");

    var student = await db.Students
        .Where(x => x.GroupId == group.Id)
        .OrderByDescending(x => x.IsLeader)
        .ThenBy(x => x.Id)
        .FirstAsync();
    var registrations = await db.StudentSlotRegistrations
        .Where(x => x.ReviewRoundId == round.Id && x.GroupId == group.Id)
        .ToListAsync();
    if (registrations.Count == 0)
    {
        var now = DateTime.UtcNow;
        var selectedSlots = new[]
        {
            (Day: 1, Slot: 1),
            (Day: 2, Slot: 2),
            (Day: 3, Slot: 3),
            (Day: 4, Slot: 1),
            (Day: 5, Slot: 2)
        };
        db.StudentSlotRegistrations.AddRange(selectedSlots.Select(x => new StudentSlotRegistration
        {
            ReviewRoundId = round.Id,
            GroupId = group.Id,
            RegisteredByStudentId = student.Id,
            DayOfWeek = x.Day,
            Slot = x.Slot,
            CreatedAt = now
        }));
    }
    else if (registrations.Count != 5 || registrations.All(x => x.DayOfWeek != 4 || x.Slot != 1))
    {
        throw new InvalidOperationException("The target group has incompatible Review 2 availability data.");
    }

    var session = await db.ReviewSessions.SingleOrDefaultAsync(x => x.Code == sessionCode);
    var operation = "verified-existing";
    if (session is null)
    {
        var now = DateTime.UtcNow;
        session = new ReviewSession
        {
            Code = sessionCode,
            SemesterId = semester.Id,
            DayOfWeek = 4,
            Slot = 1,
            Room = "B206",
            Type = ReviewType.Review2,
            Reviewer1Id = reviewers[0].Id,
            Reviewer2Id = reviewers[1].Id,
            SessionDate = sessionDate,
            Status = ReviewSessionStatus.Published,
            PublishedAt = now,
            PublishedByTrainingDepartmentId = await db.TrainingDepartments
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstAsync()
        };
        db.ReviewSessions.Add(session);
        await db.SaveChangesAsync();

        db.GroupReviewSlots.Add(new GroupReviewSlot
        {
            SessionId = session.Id,
            GroupId = group.Id,
            GroupPosition = 1,
            ConflictFlag = false
        });
        db.ReviewChecklistSubmissions.AddRange(reviewers.Select(reviewer => new ReviewChecklistSubmission
        {
            SessionId = session.Id,
            GroupId = group.Id,
            ReviewerId = reviewer.Id,
            Type = ReviewType.Review2,
            Status = ReviewSubmissionStatus.Draft,
            LastSavedAt = now
        }));
        operation = "created";
    }

    await db.SaveChangesAsync();

    var reviewerIds = reviewers.Select(x => x.Id).ToArray();
    var isValid = session.SessionDate == sessionDate &&
                  session.Status == ReviewSessionStatus.Published &&
                  await db.GroupReviewSlots.AnyAsync(x => x.SessionId == session.Id && x.GroupId == group.Id) &&
                  await db.ReviewChecklistSubmissions.CountAsync(x =>
                      x.SessionId == session.Id && x.GroupId == group.Id && reviewerIds.Contains(x.ReviewerId)) == 2;
    if (!isValid)
        throw new InvalidOperationException("The 2026-07-23 review session failed validation.");

    await transaction.CommitAsync();
    Console.WriteLine(
        $"TODAY_REVIEW operation={operation} sessionId={session.Id} date={session.SessionDate:yyyy-MM-dd} " +
        $"group={group.Code} reviewers={string.Join(',', reviewers.Select(x => x.Code))}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Today review seed failed; transaction was not committed. {exception.Message}");
    return 1;
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

using System.Text.Json;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using CPMS.DAL.Entities;
using Microsoft.EntityFrameworkCore;

const string sessionCode = "SU26_R2_TODAY_20260723";
const int sessionSlot = 3;
const string demoPassword = "Test@123456";
string[] targetReviewerCodes = ["GV24001", "GV24002", "GV24003", "GV24004"];
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
        .Where(x => targetReviewerCodes.Contains(x.Code))
        .OrderBy(x => x.Code)
        .ToArrayAsync();

    if (reviewers.Length != targetReviewerCodes.Length)
        throw new InvalidOperationException("The four reviewers for the target session are missing.");
    if (reviewers.Any(x => x.Id == group.LecturerId))
        throw new InvalidOperationException("A selected reviewer supervises the target group.");

    foreach (var reviewer in reviewers)
    {
        var reviewerUser = await db.Users.SingleAsync(x => x.Id == reviewer.UserId);
        if (!BCrypt.Net.BCrypt.Verify(demoPassword, reviewerUser.PasswordHash))
            reviewerUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(demoPassword, workFactor: 12);
        reviewerUser.IsActive = true;

        var hasTargetAvailability = await db.ReviewAvailabilities.AnyAsync(x =>
            x.ReviewRoundId == round.Id && x.LecturerId == reviewer.Id &&
            x.DayOfWeek == 4 && x.Slot == sessionSlot);
        if (!hasTargetAvailability)
        {
            db.ReviewAvailabilities.Add(new ReviewAvailability
            {
                ReviewRoundId = round.Id,
                SemesterId = semester.Id,
                LecturerId = reviewer.Id,
                WeekStartDate = round.WeekStartDate,
                DayOfWeek = 4,
                Slot = sessionSlot,
                CreatedAt = DateTime.UtcNow
            });
        }
        var availabilitySubmission = await db.ReviewAvailabilitySubmissions.SingleOrDefaultAsync(x =>
            x.ReviewRoundId == round.Id && x.LecturerId == reviewer.Id);
        if (availabilitySubmission is null)
        {
            db.ReviewAvailabilitySubmissions.Add(new ReviewAvailabilitySubmission
            {
                ReviewRoundId = round.Id,
                SemesterId = semester.Id,
                LecturerId = reviewer.Id,
                WeekStartDate = round.WeekStartDate,
                UpdatedAt = DateTime.UtcNow,
                SubmittedAt = DateTime.UtcNow
            });
        }
        else if (availabilitySubmission.SubmittedAt is null)
        {
            availabilitySubmission.SubmittedAt = DateTime.UtcNow;
            availabilitySubmission.UpdatedAt = DateTime.UtcNow;
        }
    }

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
            (Day: 4, Slot: sessionSlot),
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
    else if (registrations.Count == 5 && registrations.All(x => x.DayOfWeek != 4 || x.Slot != sessionSlot))
    {
        var previousTarget = registrations.SingleOrDefault(x => x.DayOfWeek == 4 && x.Slot == 1)
            ?? throw new InvalidOperationException("The target group has incompatible Review 2 availability data.");
        previousTarget.Slot = sessionSlot;
    }
    else if (registrations.Count != 5)
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
            Slot = sessionSlot,
            Room = "B206",
            Type = ReviewType.Review2,
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

        operation = "created";
    }
    else if (session.SessionDate != sessionDate || session.DayOfWeek != 4 || session.Slot != sessionSlot)
    {
        session.SessionDate = sessionDate;
        session.DayOfWeek = 4;
        session.Slot = sessionSlot;
        operation = "updated";
    }

    var reviewerIds = reviewers.Select(x => x.Id).ToArray();
    var existingReviewerAssignments = await db.ReviewSessionReviewers
        .Where(x => x.SessionId == session.Id).ToListAsync();
    db.ReviewSessionReviewers.RemoveRange(existingReviewerAssignments.Where(x => !reviewerIds.Contains(x.LecturerId)));
    foreach (var reviewer in reviewers.Select((value, index) => new { value, position = index + 1 }))
    {
        var assignment = existingReviewerAssignments.SingleOrDefault(x => x.LecturerId == reviewer.value.Id);
        if (assignment is null)
        {
            db.ReviewSessionReviewers.Add(new ReviewSessionReviewer
            {
                SessionId = session.Id,
                LecturerId = reviewer.value.Id,
                Position = reviewer.position
            });
            operation = operation == "created" ? operation : "updated";
        }
        else
        {
            assignment.Position = reviewer.position;
        }
    }

    if (!await db.GroupReviewSlots.AnyAsync(x => x.SessionId == session.Id && x.GroupId == group.Id))
    {
        db.GroupReviewSlots.Add(new GroupReviewSlot
        {
            SessionId = session.Id,
            GroupId = group.Id,
            GroupPosition = 1,
            ConflictFlag = false
        });
    }
    foreach (var reviewer in reviewers)
    {
        if (!await db.ReviewChecklistSubmissions.AnyAsync(x =>
                x.SessionId == session.Id && x.GroupId == group.Id && x.ReviewerId == reviewer.Id))
        {
            db.ReviewChecklistSubmissions.Add(new ReviewChecklistSubmission
            {
                SessionId = session.Id,
                GroupId = group.Id,
                ReviewerId = reviewer.Id,
                Type = ReviewType.Review2,
                Status = ReviewSubmissionStatus.Draft,
                LastSavedAt = DateTime.UtcNow
            });
            operation = operation == "created" ? operation : "updated";
        }
    }

    await db.SaveChangesAsync();

    var isValid = session.SessionDate == sessionDate &&
                  session.DayOfWeek == 4 &&
                  session.Slot == sessionSlot &&
                  session.Status == ReviewSessionStatus.Published &&
                  await db.StudentSlotRegistrations.CountAsync(x =>
                      x.ReviewRoundId == round.Id && x.GroupId == group.Id) == 5 &&
                  await db.StudentSlotRegistrations.AnyAsync(x =>
                      x.ReviewRoundId == round.Id && x.GroupId == group.Id &&
                      x.DayOfWeek == 4 && x.Slot == sessionSlot) &&
                  await db.ReviewAvailabilities.CountAsync(x =>
                      x.ReviewRoundId == round.Id && x.DayOfWeek == 4 && x.Slot == sessionSlot &&
                      reviewerIds.Contains(x.LecturerId)) == reviewerIds.Length &&
                  await db.ReviewSessionReviewers.CountAsync(x =>
                      x.SessionId == session.Id && reviewerIds.Contains(x.LecturerId)) == reviewerIds.Length &&
                  await db.GroupReviewSlots.AnyAsync(x => x.SessionId == session.Id && x.GroupId == group.Id) &&
                  await db.ReviewChecklistSubmissions.CountAsync(x =>
                      x.SessionId == session.Id && x.GroupId == group.Id &&
                      reviewerIds.Contains(x.ReviewerId)) == reviewerIds.Length;
    if (!isValid)
        throw new InvalidOperationException("The 2026-07-23 review session failed validation.");

    await transaction.CommitAsync();
    Console.WriteLine(
        $"TODAY_REVIEW operation={operation} sessionId={session.Id} date={session.SessionDate:yyyy-MM-dd} slot={session.Slot} " +
        $"group={group.Code} reviewers={string.Join(',', reviewers.Select(x => x.Code))} " +
        $"accounts={string.Join(',', await db.Users.Where(x => reviewers.Select(r => r.UserId).Contains(x.Id)).OrderBy(x => x.Email).Select(x => x.Email).ToArrayAsync())}");
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

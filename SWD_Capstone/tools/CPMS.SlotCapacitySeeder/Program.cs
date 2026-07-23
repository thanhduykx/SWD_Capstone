using System.Text.Json;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using CPMS.DAL.Entities;
using Microsoft.EntityFrameworkCore;

const string semesterCode = "SU26";
const int maxLecturersPerSlot = 4;
const int maxGroupsPerSlot = 3;
string[] lecturerCodes = ["GV24001", "GV24002", "GV24003"];
string[] groupCodes =
[
    "QA_SLOT3_TEAM_01",
    "QA_SLOT3_TEAM_02",
    "GROUP_2_2_SMART_HOME_INVENTORY"
];
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
        x.Type == ReviewType.Review3 &&
        x.WeekStartDate == new DateOnly(2026, 8, 3));
    var lecturers = await db.Lecturers
        .Where(x => lecturerCodes.Contains(x.Code))
        .ToDictionaryAsync(x => x.Code);
    var groups = await db.CapstoneGroups
        .Where(x => x.SemesterId == semester.Id && groupCodes.Contains(x.Code))
        .ToDictionaryAsync(x => x.Code);
    if (lecturers.Count != lecturerCodes.Length || groups.Count != groupCodes.Length)
        throw new InvalidOperationException("Demo lecturers or groups are missing.");

    int[] lecturerIds = lecturers.Values.Select(x => x.Id).ToArray();
    int[] groupIds = groups.Values.Select(x => x.Id).ToArray();
    (int Day, int Slot)[] allSlots =
        (from day in Enumerable.Range(1, 6)
         from slot in Enumerable.Range(1, 5)
         select (day, slot)).ToArray();
    var occupiedLecturerSlots = await db.ReviewAvailabilities
        .Where(x => x.ReviewRoundId == round.Id && !lecturerIds.Contains(x.LecturerId) &&
                    db.ReviewAvailabilitySubmissions.Any(submission =>
                        submission.ReviewRoundId == round.Id &&
                        submission.LecturerId == x.LecturerId &&
                        submission.SubmittedAt != null))
        .Select(x => new { x.DayOfWeek, x.Slot })
        .Distinct()
        .ToArrayAsync();
    var occupiedGroupSlots = await db.StudentSlotRegistrations
        .Where(x => x.ReviewRoundId == round.Id && !groupIds.Contains(x.GroupId))
        .Select(x => new { x.DayOfWeek, x.Slot })
        .Distinct()
        .ToArrayAsync();
    (int Day, int Slot)[] lecturerDemoSlots = allSlots
        .Where(slot => !occupiedLecturerSlots.Any(x => x.DayOfWeek == slot.Day && x.Slot == slot.Slot))
        .Take(7)
        .ToArray();
    (int Day, int Slot)[] groupDemoSlots = allSlots
        .Where(slot => !occupiedGroupSlots.Any(x => x.DayOfWeek == slot.Day && x.Slot == slot.Slot))
        .Take(7)
        .ToArray();
    if (lecturerDemoSlots.Length < 7 || groupDemoSlots.Length < 7)
        throw new InvalidOperationException("Not enough empty slots to create safe capacity demo data.");
    var lecturerPatterns = BuildLecturerPatterns(lecturerDemoSlots);
    var groupPatterns = BuildGroupPatterns(groupDemoSlots);

    db.ReviewAvailabilities.RemoveRange(await db.ReviewAvailabilities
        .Where(x => x.ReviewRoundId == round.Id && lecturerIds.Contains(x.LecturerId))
        .ToArrayAsync());
    foreach (var (code, slots) in lecturerPatterns)
    {
        Lecturer lecturer = lecturers[code];
        db.ReviewAvailabilities.AddRange(slots.Select(slot => new ReviewAvailability
        {
            ReviewRoundId = round.Id,
            SemesterId = semester.Id,
            LecturerId = lecturer.Id,
            WeekStartDate = round.WeekStartDate,
            DayOfWeek = slot.Day,
            Slot = slot.Slot,
            CreatedAt = DateTime.UtcNow
        }));
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
            submission.SubmittedAt = DateTime.UtcNow;
            submission.UpdatedAt = DateTime.UtcNow;
        }
    }

    db.StudentSlotRegistrations.RemoveRange(await db.StudentSlotRegistrations
        .Where(x => x.ReviewRoundId == round.Id && groupIds.Contains(x.GroupId))
        .ToArrayAsync());
    foreach (var (code, slots) in groupPatterns)
    {
        CapstoneGroup group = groups[code];
        Student leader = await db.Students
            .Where(x => x.GroupId == group.Id)
            .OrderByDescending(x => x.IsLeader)
            .ThenBy(x => x.Id)
            .FirstAsync();
        db.StudentSlotRegistrations.AddRange(slots.Select(slot => new StudentSlotRegistration
        {
            ReviewRoundId = round.Id,
            GroupId = group.Id,
            RegisteredByStudentId = leader.Id,
            DayOfWeek = slot.Day,
            Slot = slot.Slot,
            CreatedAt = DateTime.UtcNow
        }));
    }
    await db.SaveChangesAsync();

    var lecturerRows = await db.ReviewAvailabilities
        .Where(x => x.ReviewRoundId == round.Id &&
                    db.ReviewAvailabilitySubmissions.Any(submission =>
                        submission.ReviewRoundId == round.Id &&
                        submission.LecturerId == x.LecturerId &&
                        submission.SubmittedAt != null))
        .Select(x => new { x.DayOfWeek, x.Slot, x.LecturerId })
        .ToArrayAsync();
    var lecturerCounts = lecturerRows
        .GroupBy(x => new { x.DayOfWeek, x.Slot })
        .Select(x => new SlotCount(
            x.Key.DayOfWeek, x.Key.Slot, x.Select(row => row.LecturerId).Distinct().Count()))
        .OrderBy(x => x.Day)
        .ThenBy(x => x.Slot)
        .ToArray();
    var groupRows = await db.StudentSlotRegistrations
        .Where(x => x.ReviewRoundId == round.Id)
        .Select(x => new { x.DayOfWeek, x.Slot, x.GroupId })
        .ToArrayAsync();
    var groupCounts = groupRows
        .GroupBy(x => new { x.DayOfWeek, x.Slot })
        .Select(x => new SlotCount(
            x.Key.DayOfWeek, x.Key.Slot, x.Select(row => row.GroupId).Distinct().Count()))
        .OrderBy(x => x.Day)
        .ThenBy(x => x.Slot)
        .ToArray();

    if (lecturerCounts.Any(x => x.Count > maxLecturersPerSlot))
        throw new InvalidOperationException("A lecturer slot exceeds 4 registrations.");
    if (groupCounts.Any(x => x.Count > maxGroupsPerSlot))
        throw new InvalidOperationException("A student slot exceeds 3 groups.");
    if (!lecturerCounts.Any(x => x.Count == 1) || !lecturerCounts.Any(x => x.Count == 3) ||
        !groupCounts.Any(x => x.Count == 1) || !groupCounts.Any(x => x.Count == 3))
        throw new InvalidOperationException("The requested 1/x and 3/x demo patterns were not produced.");

    await transaction.CommitAsync();
    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new
    {
        status = "verified",
        roundId = round.Id,
        round = round.Type.ToString(),
        week = $"{round.WeekStartDate:yyyy-MM-dd}..{round.WeekEndDate:yyyy-MM-dd}",
        lecturerCounts,
        groupCounts,
        maximumLecturersInAnySlot = lecturerCounts.Max(x => x.Count),
        maximumGroupsInAnySlot = groupCounts.Max(x => x.Count)
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Slot capacity seed failed; transaction was not committed. {exception}");
    return 1;
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "CPMS.sln"))) return directory.FullName;
    throw new DirectoryNotFoundException("Run this tool from inside the CPMS repository.");
}

static IReadOnlyDictionary<string, (int Day, int Slot)[]> BuildLecturerPatterns(
    IReadOnlyList<(int Day, int Slot)> slots) => new Dictionary<string, (int Day, int Slot)[]>
{
    ["GV24001"] = [slots[0], slots[1], slots[2], slots[3], slots[4], slots[5], slots[6]],
    ["GV24002"] = [slots[1], slots[2], slots[3], slots[5], slots[6]],
    ["GV24003"] = [slots[1], slots[3], slots[4], slots[5], slots[6]]
};

static IReadOnlyDictionary<string, (int Day, int Slot)[]> BuildGroupPatterns(
    IReadOnlyList<(int Day, int Slot)> slots) => new Dictionary<string, (int Day, int Slot)[]>
{
    ["QA_SLOT3_TEAM_01"] = [slots[0], slots[1], slots[2], slots[3], slots[6]],
    ["QA_SLOT3_TEAM_02"] = [slots[1], slots[2], slots[3], slots[4], slots[6]],
    ["GROUP_2_2_SMART_HOME_INVENTORY"] = [slots[1], slots[3], slots[4], slots[5], slots[6]]
};

static string ReadConnectionString(string root)
{
    var environmentValue = Environment.GetEnvironmentVariable("ConnectionStrings__CpmsDatabase");
    if (!string.IsNullOrWhiteSpace(environmentValue)) return environmentValue;
    using var json = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(root, "src", "CPMS.API", "appsettings.json")));
    return json.RootElement.GetProperty("ConnectionStrings").GetProperty("CpmsDatabase").GetString()
        ?? throw new InvalidOperationException("ConnectionStrings:CpmsDatabase is missing.");
}

internal sealed record SlotCount(int Day, int Slot, int Count);

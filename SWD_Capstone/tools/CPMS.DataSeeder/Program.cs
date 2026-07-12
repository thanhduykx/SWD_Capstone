using System.Text.Json;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

const string marker = "DEMO_2026";
const string password = "Demo@2026";

try
{
    var root = FindRepositoryRoot();
    var connectionString = ReadConnectionString(root);
    var outputPath = GetArgument(args, "--output") ?? Path.Combine(root, "artifacts", "demo-seed-report.json");
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

    var options = new DbContextOptionsBuilder<CpmsDbContext>()
        .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("CPMS.Infrastructure"))
        .UseSnakeCaseNamingConvention()
        .Options;
    await using var db = new CpmsDbContext(options);
    await db.Database.OpenConnectionAsync();
    await db.Database.MigrateAsync();

    var before = await CountTablesAsync(db);
    await using var transaction = await db.Database.BeginTransactionAsync();
    var created = !await db.Semesters.AnyAsync(x => x.Code == marker);
    if (created)
    {
        await SeedAsync(db);
    }
    await ValidateAsync(db);
    await transaction.CommitAsync();
    var after = await CountTablesAsync(db);

    var report = new
    {
        marker,
        generatedAtUtc = DateTime.UtcNow,
        database = SanitizeDatabaseName(connectionString),
        operation = created ? "created" : "verified-existing",
        tables = after.Select(x => new { name = x.Key, before = before[x.Key], after = x.Value, added = x.Value - before[x.Key] }),
        demoAccounts = DemoAccounts().Select(x => new { x.Username, x.Email, role = x.Role.ToString(), password }),
        validation = new { tableCount = after.Count, allTablesNonEmpty = after.Values.All(x => x > 0), foreignKeys = "validated-by-database", transaction = "committed", idempotencyKey = marker }
    };
    await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Demo data {report.operation}. Report: {outputPath}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Demo seed failed; transaction was not committed. {exception.Message}");
    return 1;
}

static async Task SeedAsync(CpmsDbContext db)
{
    var now = DateTime.UtcNow;
    var users = DemoAccounts().Select(x => new User
    {
        Username = x.Username, Email = x.Email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
        Role = x.Role, IsActive = true, CreatedAt = now
    }).ToArray();
    db.Users.AddRange(users);
    await db.SaveChangesAsync();

    var adminUser = users.Single(x => x.Role == UserRole.SystemAdministrator);
    var trainingUser = users.Single(x => x.Role == UserRole.TrainingDepartment);
    var lecturerUsers = users.Where(x => x.Role == UserRole.Lecturer).ToArray();
    var studentUsers = users.Where(x => x.Role == UserRole.Student).ToArray();
    var admin = new SystemAdministrator { UserId = adminUser.Id, AdminLevel = "Root Demo", PermissionScope = marker };
    var training = new TrainingDepartment { UserId = trainingUser.Id, DepartmentName = "Software Engineering", StaffCode = marker + "_TD", Position = "Training Officer" };
    var lecturers = lecturerUsers.Select((x, i) => new Lecturer { UserId = x.Id, Code = $"{marker}_LEC{i + 1:00}", FullName = $"Giang vien Demo {i + 1}", Department = "Software Engineering", IsPartTime = i == 2 }).ToArray();
    var panel = new EvaluationPanel { UserId = lecturerUsers[2].Id, FullName = lecturers[2].FullName, Department = lecturers[2].Department };
    db.AddRange(admin, training);
    db.Lecturers.AddRange(lecturers);
    db.EvaluationPanels.Add(panel);
    await db.SaveChangesAsync();

    var semester = new Semester { Code = marker, Name = "Summer 2026 Demo", AcademicYear = "2025-2026", StartDate = new DateOnly(2026, 5, 4), EndDate = new DateOnly(2026, 8, 30), IsActive = false };
    db.Semesters.Add(semester);
    await db.SaveChangesAsync();
    var topics = Enumerable.Range(1, 2).Select(i => new Topic { Code = $"{marker}_TOPIC{i:00}", NameVn = $"He thong quan ly do an {i}", NameEn = $"Capstone Management System {i}", SemesterId = semester.Id }).ToArray();
    db.Topics.AddRange(topics);
    await db.SaveChangesAsync();
    var groups = topics.Select((x, i) => new CapstoneGroup { Code = $"{marker}_G{i + 1:00}", TopicId = x.Id, SemesterId = semester.Id, LecturerId = lecturers[i].Id, Status = GroupStatus.Completed }).ToArray();
    db.CapstoneGroups.AddRange(groups);
    await db.SaveChangesAsync();

    var students = studentUsers.Select((x, i) => new Student { UserId = x.Id, Code = $"{marker}_SE{i + 1:000}", FullName = $"Sinh vien Demo {i + 1}", ClassCode = "SE1901", Batch = "K19", Major = "Software Engineering", GroupId = groups[i / 3].Id, IsLeader = i % 3 == 0 }).ToArray();
    db.Students.AddRange(students);
    var syllabus = new Syllabus { TrainingDepartmentId = training.Id, Name = marker + " Capstone Syllabus", Major = "Software Engineering", AcademicYear = "2025-2026", IsActive = true, CreatedById = adminUser.Id };
    db.Syllabuses.Add(syllabus);
    await db.SaveChangesAsync();
    var clos = new[]
    {
        new Clo { SyllabusId = syllabus.Id, Code = "CLO1", Description = "Analyze and design a maintainable software solution", Weight = .5m },
        new Clo { SyllabusId = syllabus.Id, Code = "CLO2", Description = "Implement, test and present the capstone product", Weight = .5m }
    };
    db.Clos.AddRange(clos);
    await db.SaveChangesAsync();
    db.RuleKeywords.AddRange(clos.Select((x, i) => new RuleKeyword { CloId = x.Id, CreatedByAdminId = admin.Id, KeywordText = marker + (i == 0 ? " architecture testing" : " implementation presentation"), Weight = .5m, CreatedById = adminUser.Id }));
    await db.SaveChangesAsync();

    var document = new CapstoneDocument { GroupId = groups[0].Id, SyllabusId = syllabus.Id, UploadedById = studentUsers[0].Id, DocType = DocumentType.Final, FileName = marker + "_final-report.docx", FilePath = "/demo/DEMO_2026/final-report.docx", FileSize = 1_048_576, VersionNo = 1, Status = DocumentStatus.Approved, UploadedAt = now.AddDays(-14), CreatedById = studentUsers[0].Id };
    db.Documents.Add(document);
    await db.SaveChangesAsync();
    var evaluation = new EvaluationReport { DocumentId = document.Id, ReviewedById = panel.Id, OverallScore = 8.5m, MatchPercentage = 85m, GapAnalysisSummary = marker + ": adequate evidence; improve deployment metrics.", TriggerType = EvaluationTriggerType.Manual, EvaluatedAt = now.AddDays(-12) };
    db.EvaluationReports.Add(evaluation);
    await db.SaveChangesAsync();
    db.EvaluationDetails.AddRange(clos.Select((x, i) => new EvaluationDetail { ReportId = evaluation.Id, CloId = x.Id, Score = i == 0 ? 4.2m : 4.3m, MaxScore = 5m, MatchPercentage = i == 0 ? 84m : 86m, Feedback = marker + " evidence accepted", MissingEvidence = i == 0 ? "Load-test chart" : null }));
    var comment = new InlineComment { DocumentId = document.Id, AuthorId = lecturerUsers[0].Id, ParagraphIndex = 3, Content = marker + ": clarify the architecture boundary.", Status = CommentStatus.Resolved, CreatedById = lecturerUsers[0].Id };
    db.InlineComments.Add(comment);
    await db.SaveChangesAsync();
    db.InlineComments.Add(new InlineComment { DocumentId = document.Id, AuthorId = studentUsers[0].Id, ParagraphIndex = 3, Content = marker + ": architecture diagram updated.", ParentCommentId = comment.Id, Status = CommentStatus.Resolved, CreatedById = studentUsers[0].Id });

    var week = new DateOnly(2026, 7, 6);
    db.ReviewAvailabilities.AddRange(lecturers.Select((x, i) => new ReviewAvailability { SemesterId = semester.Id, LecturerId = x.Id, WeekStartDate = week, DayOfWeek = i + 2, Slot = 2, CreatedAt = now.AddDays(-30) }));
    db.ReviewAvailabilitySubmissions.AddRange(lecturers.Select(x => new ReviewAvailabilitySubmission { SemesterId = semester.Id, LecturerId = x.Id, WeekStartDate = week, UpdatedAt = now.AddDays(-30), SubmittedAt = now.AddDays(-29) }));
    var reviewRound = new ReviewRound { SemesterId = semester.Id, Type = ReviewType.Review1, WeekStartDate = week, WeekEndDate = week.AddDays(5), Status = ReviewRoundStatus.Published, CreatedByModeratorId = training.Id, CreatedAt = now.AddDays(-35), OpenedAt = now.AddDays(-34), ClosedAt = now.AddDays(-28) };
    db.ReviewRounds.Add(reviewRound);
    await db.SaveChangesAsync();
    db.StudentSlotRegistrations.AddRange(groups.Select((x, i) => new StudentSlotRegistration { ReviewRoundId = reviewRound.Id, GroupId = x.Id, RegisteredByStudentId = students[i * 3].Id, DayOfWeek = i + 2, Slot = 2, CreatedAt = now.AddDays(-33) }));
    var reviewSession = new ReviewSession { Code = marker + "_REV01", SemesterId = semester.Id, DayOfWeek = 2, Slot = 2, Room = "DEMO-R101", Type = ReviewType.Review1, Reviewer1Id = lecturers[0].Id, Reviewer2Id = lecturers[1].Id, SessionDate = new DateTime(2026, 7, 7, 9, 0, 0, DateTimeKind.Utc), Status = ReviewSessionStatus.Published, PublishedAt = now.AddDays(-27), PublishedByTrainingDepartmentId = training.Id };
    db.ReviewSessions.Add(reviewSession);
    await db.SaveChangesAsync();
    db.GroupReviewSlots.Add(new GroupReviewSlot { SessionId = reviewSession.Id, GroupId = groups[0].Id, GroupPosition = 1, Result = ReviewResult.Pass, Notes = marker + " review passed", ConflictFlag = false });
    var submission = new ReviewChecklistSubmission { SessionId = reviewSession.Id, GroupId = groups[0].Id, ReviewerId = lecturers[0].Id, Type = ReviewType.Review1, Status = ReviewSubmissionStatus.Submitted, WorkProductVersion = "v1.0-demo", WorkProductSize = "1 MB", EffortHours = 120m, ReviewerComment = marker + " good progress", Suggestion = "Add monitoring dashboard", ResultText = "Pass", LastSavedAt = now.AddDays(-25), SubmittedAt = now.AddDays(-25) };
    db.ReviewChecklistSubmissions.Add(submission);
    await db.SaveChangesAsync();
    db.ReviewChecklistItemResponses.AddRange(
        new ReviewChecklistItemResponse { SubmissionId = submission.Id, ItemKey = marker + "_SCOPE", Answer = ReviewChecklistAnswer.Yes, Comment = "Scope is complete", UpdatedAt = now.AddDays(-25) },
        new ReviewChecklistItemResponse { SubmissionId = submission.Id, ItemKey = marker + "_TEST", Answer = ReviewChecklistAnswer.Yes, Comment = "Automated tests included", UpdatedAt = now.AddDays(-25) });
    var publication = new ReviewSchedulePublication { SemesterId = semester.Id, ReviewType = ReviewType.Review1, WeekStartDate = week, PublishedByTrainingDepartmentId = training.Id, PublishedAt = now.AddDays(-27), Subject = marker + " review schedule", Body = "Synthetic schedule; no email was sent." };
    db.ReviewSchedulePublications.Add(publication);
    await db.SaveChangesAsync();
    db.EmailDeliveryLogs.Add(new EmailDeliveryLog { PublicationId = publication.Id, RecipientUserId = studentUsers[0].Id, RecipientEmail = studentUsers[0].Email, Subject = publication.Subject, Status = EmailDeliveryStatus.Pending, ErrorMessage = "DEMO_ONLY_NOT_SENT", CreatedAt = now.AddDays(-27) });
    db.ReviewAttendances.AddRange(students.Take(3).Select(x => new ReviewAttendance { SessionId = reviewSession.Id, StudentId = x.Id, IsPresent = true, Note = marker, MarkedByLecturerId = lecturers[0].Id, MarkedAt = now.AddDays(-25) }));
    db.ReviewComments.Add(new ReviewComment { SessionId = reviewSession.Id, GroupId = groups[0].Id, AuthorUserId = lecturerUsers[0].Id, Content = marker + ": prepare deployment evidence.", CreatedAt = now.AddDays(-25) });

    var council = new Council { Code = marker + "_COUNCIL01", SemesterId = semester.Id, ManagedByTrainingDepartmentId = training.Id, ChairmanId = lecturers[0].Id, SecretaryId = lecturers[1].Id, Type = CouncilType.Defense1, Status = CouncilStatus.Closed };
    db.Councils.Add(council);
    await db.SaveChangesAsync();
    db.CouncilMembers.AddRange(
        new CouncilMember { CouncilId = council.Id, LecturerId = lecturers[0].Id, Role = CouncilMemberRole.Chairman },
        new CouncilMember { CouncilId = council.Id, LecturerId = lecturers[1].Id, Role = CouncilMemberRole.Secretary },
        new CouncilMember { CouncilId = council.Id, LecturerId = lecturers[2].Id, Role = CouncilMemberRole.Member });
    db.CouncilGroups.Add(new CouncilGroup { CouncilId = council.Id, GroupId = groups[0].Id, Position = 1, ConflictFlag = false });
    var defenseRound = new DefenseRound { Code = marker + "_DEF01", Name = "Demo Defense Round 1", SemesterId = semester.Id, Type = CouncilType.Defense1, StartDate = new DateOnly(2026, 8, 10), EndDate = new DateOnly(2026, 8, 14), Status = DefenseRoundStatus.Completed, CreatedByTrainingDepartmentId = training.Id };
    db.DefenseRounds.Add(defenseRound);
    await db.SaveChangesAsync();
    var defense = new DefenseSession { Code = marker + "_SESSION01", DefenseRoundId = defenseRound.Id, CouncilId = council.Id, GroupId = groups[0].Id, SessionDate = new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), Slot = 1, Room = "DEMO-HALL-A", AssignedByTrainingDepartmentId = training.Id, StartedAt = now.AddDays(-5), EndedAt = now.AddDays(-5).AddMinutes(45), StartedById = lecturerUsers[0].Id, IsLocked = true };
    db.DefenseSessions.Add(defense);
    await db.SaveChangesAsync();
    var scores = (from student in students.Take(3) from lecturer in lecturers select new Score { DefenseSessionId = defense.Id, ScorerId = lecturer.Id, StudentId = student.Id, ScoreType = ScoreType.BaoVe, ScoreValue = 8m + ((student.Id + lecturer.Id) % 10) / 10m, SubmittedAt = now.AddDays(-5), IsLocked = true }).ToArray();
    db.Scores.AddRange(scores);
    await db.SaveChangesAsync();
    db.ScoreSubmissionHistories.AddRange(scores.Select(x => new ScoreSubmissionHistory { ScoreId = x.Id, DefenseSessionId = defense.Id, ScorerId = x.ScorerId, SubmittedByUserId = lecturerUsers[Array.FindIndex(lecturers, l => l.Id == x.ScorerId)].Id, StudentId = x.StudentId, ScoreType = x.ScoreType, NewScoreValue = x.ScoreValue, SubmittedAt = x.SubmittedAt, IpAddress = "127.0.0.1", UserAgent = marker, IsTrusted = true, TrustReason = "Synthetic demo submission" }));
    db.DefenseEvidences.Add(new DefenseEvidence { DefenseSessionId = defense.Id, CapturedByUserId = lecturerUsers[0].Id, CapturedByLecturerId = lecturers[0].Id, FileName = marker + "_evidence.jpg", FilePath = "/demo/DEMO_2026/evidence.jpg", ContentType = "image/jpeg", FileSize = 245760, Note = "Synthetic reference only; file is not provisioned.", IpAddress = "127.0.0.1", UserAgent = marker, CapturedAt = now.AddDays(-5) });
    db.GroupResults.AddRange(groups.Select((x, i) => new GroupResult { GroupId = x.Id, SemesterId = semester.Id, Review1Result = ReviewResult.Pass, Review2Result = i == 0 ? ReviewResult.Pass : null, SupervisorResult = ReviewResult.Pass, Defense1Result = i == 0 ? FinalResult.Pass : null, FinalResult = i == 0 ? FinalResult.Pass : FinalResult.Pending }));

    db.RefreshTokens.Add(new RefreshToken { UserId = studentUsers[0].Id, TokenHash = marker + "_REVOKED_TOKEN_HASH", ExpiresAt = now.AddDays(-30), IsRevoked = true, CreatedAt = now.AddDays(-60), DeviceInfo = marker + " synthetic revoked token" });
    db.Notifications.Add(new Notification { RecipientId = studentUsers[0].Id, SenderId = trainingUser.Id, Type = NotificationType.ReportDone, Title = marker + " report completed", Message = "Synthetic demo notification", IsRead = true, CreatedAt = now.AddDays(-12), ReadAt = now.AddDays(-11) });
    db.AuditLogs.Add(new AuditLog { UserId = adminUser.Id, Action = "CREATE_DEMO_DATA", EntityType = "DemoDataset", EntityId = semester.Id, NewValue = marker, IpAddress = "127.0.0.1", UserAgent = "CPMS.DataSeeder", CreatedAt = now });
    await db.SaveChangesAsync();
}

static async Task ValidateAsync(CpmsDbContext db)
{
    var semester = await db.Semesters.SingleOrDefaultAsync(x => x.Code == marker) ?? throw new InvalidOperationException("Demo semester is missing.");
    var demoUsers = await db.Users.Where(x => x.Username.StartsWith("demo.")).ToListAsync();
    if (demoUsers.Count != DemoAccounts().Length || demoUsers.Any(x => !BCrypt.Net.BCrypt.Verify(password, x.PasswordHash)))
        throw new InvalidOperationException("Demo account login credentials could not be verified.");
    if (!await db.CapstoneGroups.AnyAsync(x => x.SemesterId == semester.Id) || !await db.DefenseRounds.AnyAsync(x => x.SemesterId == semester.Id))
        throw new InvalidOperationException("The existing demo dataset is incomplete.");
    var counts = await CountTablesAsync(db);
    var empty = counts.Where(x => x.Value == 0).Select(x => x.Key).ToArray();
    if (empty.Length > 0) throw new InvalidOperationException("Tables without data: " + string.Join(", ", empty));
}

static async Task<SortedDictionary<string, int>> CountTablesAsync(CpmsDbContext db)
{
    var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
    foreach (var entityType in db.Model.GetEntityTypes().Where(x => !x.IsOwned()).OrderBy(x => x.GetTableName()))
    {
        var table = entityType.GetTableName() ?? entityType.ClrType.Name;
#pragma warning disable EF1002 // Identifier comes exclusively from the trusted EF model and is validated below.
        if (table.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '_')))
            throw new InvalidOperationException($"Unsafe table identifier in the EF model: {table}");
        var count = await db.Database.SqlQueryRaw<int>($"SELECT COUNT(*)::int AS \"Value\" FROM \"{table}\"").SingleAsync();
#pragma warning restore EF1002
        result[table] = count;
    }
    return result;
}

static (string Username, string Email, UserRole Role)[] DemoAccounts() =>
[
    ("demo.admin", "demo.admin@cpms.local", UserRole.SystemAdministrator),
    ("demo.training", "demo.training@cpms.local", UserRole.TrainingDepartment),
    ("demo.lecturer1", "demo.lecturer1@cpms.local", UserRole.Lecturer),
    ("demo.lecturer2", "demo.lecturer2@cpms.local", UserRole.Lecturer),
    ("demo.lecturer3", "demo.lecturer3@cpms.local", UserRole.Lecturer),
    ("demo.student1", "demo.student1@cpms.local", UserRole.Student),
    ("demo.student2", "demo.student2@cpms.local", UserRole.Student),
    ("demo.student3", "demo.student3@cpms.local", UserRole.Student),
    ("demo.student4", "demo.student4@cpms.local", UserRole.Student),
    ("demo.student5", "demo.student5@cpms.local", UserRole.Student),
    ("demo.student6", "demo.student6@cpms.local", UserRole.Student)
];

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
    using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src", "CPMS.API", "appsettings.json")));
    return json.RootElement.GetProperty("ConnectionStrings").GetProperty("CpmsDatabase").GetString()
        ?? throw new InvalidOperationException("ConnectionStrings:CpmsDatabase is missing.");
}

static string? GetArgument(string[] arguments, string name)
{
    var index = Array.IndexOf(arguments, name);
    return index >= 0 && index + 1 < arguments.Length ? Path.GetFullPath(arguments[index + 1]) : null;
}

static string SanitizeDatabaseName(string connectionString)
{
    var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
    return $"{builder.Host}/{builder.Database}";
}

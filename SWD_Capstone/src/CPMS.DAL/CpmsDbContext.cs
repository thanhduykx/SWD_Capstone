using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CPMS.DAL.Data;

public sealed class CpmsDbContext(DbContextOptions<CpmsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Lecturer> Lecturers => Set<Lecturer>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<TrainingDepartment> TrainingDepartments => Set<TrainingDepartment>();
    public DbSet<SystemAdministrator> SystemAdministrators => Set<SystemAdministrator>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<CapstoneGroup> CapstoneGroups => Set<CapstoneGroup>();
    public DbSet<Syllabus> Syllabuses => Set<Syllabus>();
    public DbSet<CapstoneDocument> Documents => Set<CapstoneDocument>();
    public DbSet<DocumentPayload> DocumentPayloads => Set<DocumentPayload>();
    public DbSet<InlineComment> InlineComments => Set<InlineComment>();
    public DbSet<ReviewAvailability> ReviewAvailabilities => Set<ReviewAvailability>();
    public DbSet<ReviewAvailabilitySubmission> ReviewAvailabilitySubmissions => Set<ReviewAvailabilitySubmission>();
    public DbSet<ReviewSession> ReviewSessions => Set<ReviewSession>();
    public DbSet<ReviewSessionAccessGrant> ReviewSessionAccessGrants => Set<ReviewSessionAccessGrant>();
    public DbSet<ReviewSessionReviewer> ReviewSessionReviewers => Set<ReviewSessionReviewer>();
    public DbSet<GroupReviewSlot> GroupReviewSlots => Set<GroupReviewSlot>();
    public DbSet<ReviewChecklistSubmission> ReviewChecklistSubmissions => Set<ReviewChecklistSubmission>();
    public DbSet<ReviewSubmissionComment> ReviewSubmissionComments => Set<ReviewSubmissionComment>();
    public DbSet<ReviewChecklistItemResponse> ReviewChecklistItemResponses => Set<ReviewChecklistItemResponse>();
    public DbSet<ReviewSchedulePublication> ReviewSchedulePublications => Set<ReviewSchedulePublication>();
    public DbSet<EmailDeliveryLog> EmailDeliveryLogs => Set<EmailDeliveryLog>();
    public DbSet<Council> Councils => Set<Council>();
    public DbSet<CouncilMember> CouncilMembers => Set<CouncilMember>();
    public DbSet<CouncilGroup> CouncilGroups => Set<CouncilGroup>();
    public DbSet<DefenseRound> DefenseRounds => Set<DefenseRound>();
    public DbSet<DefenseSession> DefenseSessions => Set<DefenseSession>();
    public DbSet<Score> Scores => Set<Score>();
    public DbSet<ScoreSubmissionHistory> ScoreSubmissionHistories => Set<ScoreSubmissionHistory>();
    public DbSet<DefenseEvidence> DefenseEvidences => Set<DefenseEvidence>();
    public DbSet<DefenseComment> DefenseComments => Set<DefenseComment>();
    public DbSet<GroupResult> GroupResults => Set<GroupResult>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ReviewRound> ReviewRounds => Set<ReviewRound>();
    public DbSet<StudentSlotRegistration> StudentSlotRegistrations => Set<StudentSlotRegistration>();
    public DbSet<ReviewAttendance> ReviewAttendances => Set<ReviewAttendance>();
    public DbSet<ReviewComment> ReviewComments => Set<ReviewComment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Username).HasMaxLength(100);
            entity.Property(x => x.Email).HasMaxLength(256);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Lecturer>(entity =>
        {
            entity.ToTable("lecturers");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("students");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.GroupId).IsUnique().HasFilter("\"is_leader\" = TRUE");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CapstoneGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<TrainingDepartment>(entity =>
        {
            entity.ToTable("training_departments");
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<SystemAdministrator>(entity =>
        {
            entity.ToTable("system_administrators");
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Semester>(entity =>
        {
            entity.ToTable("semesters", table => table.HasCheckConstraint(
                "ck_semesters_supported_codes",
                "upper(\"code\") ~ '^(SP|SU|FA)[0-9]{2}$'"));
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.IsActive).IsUnique().HasFilter("\"is_active\" = TRUE");
        });
        modelBuilder.Entity<Topic>(entity =>
        {
            entity.ToTable("topics");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CapstoneGroup>(entity =>
        {
            entity.ToTable("capstone_groups");
            entity.HasIndex(x => new { x.Code, x.SemesterId }).IsUnique();
            entity.Property(x => x.Status).HasConversion<string>();
            entity.HasOne<Topic>().WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.LecturerId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Syllabus>(entity =>
        {
            entity.ToTable("syllabuses");
            entity.HasOne<TrainingDepartment>().WithMany().HasForeignKey(x => x.TrainingDepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CapstoneDocument>(entity =>
        {
            entity.ToTable("documents");
            entity.HasQueryFilter(x => !x.IsDeleted);
            entity.Property(x => x.DocType).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>();
            entity.HasOne<CapstoneGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Syllabus>().WithMany().HasForeignKey(x => x.SyllabusId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DocumentPayload>(entity =>
        {
            entity.ToTable("document_payloads");
            entity.HasKey(x => x.StorageKey);
            entity.Property(x => x.StorageKey).HasMaxLength(500);
            entity.Property(x => x.ContentType).HasMaxLength(200);
            entity.Property(x => x.Content).HasColumnType("bytea");
        });
        modelBuilder.Entity<InlineComment>(entity =>
        {
            entity.ToTable("inline_comments");
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.Reference).HasMaxLength(200);
            entity.HasOne<CapstoneDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<InlineComment>().WithMany().HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureReviewAndDefense(modelBuilder);
        ConfigureSystem(modelBuilder);
    }

    private static void ConfigureReviewAndDefense(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReviewAvailability>(entity =>
        {
            entity.ToTable("review_availabilities", table =>
            {
                table.HasCheckConstraint("ck_review_availabilities_day_of_week", "day_of_week BETWEEN 1 AND 6");
                table.HasCheckConstraint("ck_review_availabilities_slot", "slot BETWEEN 1 AND 5");
            });
            entity.HasIndex(x => new { x.ReviewRoundId, x.LecturerId, x.DayOfWeek, x.Slot }).IsUnique();
            entity.HasOne<ReviewRound>().WithMany().HasForeignKey(x => x.ReviewRoundId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ReviewAvailabilitySubmission>(entity =>
        {
            entity.ToTable("review_availability_submissions");
            entity.HasIndex(x => new { x.ReviewRoundId, x.LecturerId }).IsUnique();
            entity.HasOne<ReviewRound>().WithMany().HasForeignKey(x => x.ReviewRoundId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReviewSession>(entity =>
        {
            entity.ToTable("review_sessions", table =>
            {
                table.HasCheckConstraint("ck_review_sessions_day_of_week", "status = 'Cancelled' OR day_of_week BETWEEN 1 AND 6");
                table.HasCheckConstraint("ck_review_sessions_slot", "status = 'Cancelled' OR slot BETWEEN 1 AND 5");
            });
            entity.HasIndex(x => new { x.Code, x.SemesterId }).IsUnique();
            entity.HasIndex(x => new { x.SemesterId, x.Type, x.SessionDate, x.Slot });
            entity.Property(x => x.AccessCodeHash).HasMaxLength(100);
            entity.Property(x => x.Type).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>().HasDefaultValue(ReviewSessionStatus.Draft);
            entity.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TrainingDepartment>().WithMany().HasForeignKey(x => x.PublishedByTrainingDepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.AccessCodeGeneratedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ReviewSessionAccessGrant>(entity =>
        {
            entity.ToTable("review_session_access_grants");
            entity.HasKey(x => new { x.SessionId, x.LecturerId });
            entity.HasIndex(x => x.LecturerId);
            entity.HasOne<ReviewSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ReviewSessionReviewer>(entity =>
        {
            entity.ToTable("review_session_reviewers", table =>
                table.HasCheckConstraint("ck_review_session_reviewers_position", "position > 0"));
            entity.HasKey(x => new { x.SessionId, x.LecturerId });
            entity.HasIndex(x => new { x.SessionId, x.Position });
            entity.HasOne<ReviewSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<GroupReviewSlot>(entity =>
        {
            entity.ToTable("group_review_slots", table =>
            {
                table.HasCheckConstraint("ck_group_review_slots_position", "group_position BETWEEN 1 AND 3");
            });
            entity.HasIndex(x => new { x.SessionId, x.GroupId }).IsUnique();
            entity.HasIndex(x => new { x.SessionId, x.GroupPosition }).IsUnique();
            entity.Property(x => x.Result).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>().HasDefaultValue(ReviewGroupStatus.Scheduled);
            entity.HasOne<ReviewSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CapstoneGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.CompletedByLecturerId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ReviewChecklistSubmission>(entity =>
        {
            entity.ToTable("review_checklist_submissions");
            entity.HasIndex(x => new { x.SessionId, x.GroupId, x.ReviewerId }).IsUnique();
            entity.HasIndex(x => new { x.GroupId, x.Type });
            entity.Property(x => x.Type).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>().HasDefaultValue(ReviewSubmissionStatus.Draft);
            entity.Property(x => x.WorkProductVersion).HasMaxLength(100);
            entity.Property(x => x.WorkProductSize).HasMaxLength(100);
            entity.Property(x => x.EffortHours).HasPrecision(6, 2);
            entity.HasOne<ReviewSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CapstoneGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ReviewSubmissionComment>(entity =>
        {
            entity.ToTable("review_submission_comments", table =>
                table.HasCheckConstraint("ck_review_submission_comments_position", "position > 0"));
            entity.HasIndex(x => new { x.SubmissionId, x.Position }).IsUnique();
            entity.Property(x => x.Content).HasMaxLength(4000);
            entity.HasOne<ReviewChecklistSubmission>().WithMany().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ReviewChecklistItemResponse>(entity =>
        {
            entity.ToTable("review_checklist_item_responses");
            entity.HasIndex(x => new { x.SubmissionId, x.ItemKey }).IsUnique();
            entity.Property(x => x.ItemKey).HasMaxLength(80);
            entity.Property(x => x.Answer).HasConversion<string>();
            entity.HasOne<ReviewChecklistSubmission>().WithMany().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ReviewSchedulePublication>(entity =>
        {
            entity.ToTable("review_schedule_publications");
            entity.HasIndex(x => new { x.SemesterId, x.ReviewType, x.WeekStartDate }).IsUnique();
            entity.Property(x => x.ReviewType).HasConversion<string>();
            entity.Property(x => x.Subject).HasMaxLength(250);
            entity.Property(x => x.Body);
            entity.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TrainingDepartment>().WithMany().HasForeignKey(x => x.PublishedByTrainingDepartmentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EmailDeliveryLog>(entity =>
        {
            entity.ToTable("email_delivery_logs");
            entity.HasIndex(x => x.PublicationId);
            entity.HasIndex(x => x.RecipientEmail);
            entity.Property(x => x.RecipientEmail).HasMaxLength(256);
            entity.Property(x => x.Subject).HasMaxLength(250);
            entity.Property(x => x.Status).HasConversion<string>();
            entity.Property(x => x.ErrorMessage).HasMaxLength(1000);
            entity.HasOne<ReviewSchedulePublication>().WithMany().HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Council>(entity =>
        {
            entity.ToTable("councils");
            entity.HasIndex(x => new { x.Code, x.SemesterId }).IsUnique();
            entity.Property(x => x.Type).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>();
            entity.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TrainingDepartment>().WithMany().HasForeignKey(x => x.ManagedByTrainingDepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.ChairmanId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.SecretaryId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CouncilMember>(entity =>
        {
            entity.ToTable("council_members");
            entity.HasKey(x => new { x.CouncilId, x.LecturerId });
            entity.Property(x => x.Role).HasConversion<string>();
            entity.HasOne<Council>().WithMany().HasForeignKey(x => x.CouncilId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CouncilGroup>(entity =>
        {
            entity.ToTable("council_groups");
            entity.HasIndex(x => new { x.CouncilId, x.GroupId }).IsUnique();
            entity.HasOne<Council>().WithMany().HasForeignKey(x => x.CouncilId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CapstoneGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DefenseRound>(entity =>
        {
            entity.ToTable("defense_rounds");
            entity.HasIndex(x => new { x.Code, x.SemesterId }).IsUnique();
            entity.HasIndex(x => new { x.SemesterId, x.Type, x.Status });
            entity.Property(x => x.Code).HasMaxLength(50);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Type).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>();
            entity.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TrainingDepartment>().WithMany().HasForeignKey(x => x.CreatedByTrainingDepartmentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DefenseSession>(entity =>
        {
            entity.ToTable("defense_sessions");
            entity.HasIndex(x => new { x.Code, x.DefenseRoundId }).IsUnique();
            entity.HasIndex(x => new { x.DefenseRoundId, x.GroupId }).IsUnique();
            entity.HasIndex(x => new { x.CouncilId, x.SessionDate, x.Slot }).IsUnique();
            entity.HasIndex(x => new { x.SessionDate, x.Room, x.Slot }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(50);
            entity.Property(x => x.Room).HasMaxLength(100);
            entity.HasOne<DefenseRound>().WithMany().HasForeignKey(x => x.DefenseRoundId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Council>().WithMany().HasForeignKey(x => x.CouncilId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CapstoneGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TrainingDepartment>().WithMany().HasForeignKey(x => x.AssignedByTrainingDepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.StartedById).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Score>(entity =>
        {
            entity.ToTable("scores");
            entity.HasIndex(x => new { x.DefenseSessionId, x.ScorerId, x.StudentId, x.ScoreType }).IsUnique();
            entity.Property(x => x.ScoreType).HasConversion<string>();
            entity.Property(x => x.ScoreValue).HasPrecision(4, 2);
            entity.HasOne<DefenseSession>().WithMany().HasForeignKey(x => x.DefenseSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.ScorerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ScoreSubmissionHistory>(entity =>
        {
            entity.ToTable("score_submission_histories");
            entity.Property(x => x.ScoreType).HasConversion<string>();
            entity.Property(x => x.OldScoreValue).HasPrecision(4, 2);
            entity.Property(x => x.NewScoreValue).HasPrecision(4, 2);
            entity.Property(x => x.TrustReason).HasMaxLength(256);
            entity.HasOne<Score>().WithMany().HasForeignKey(x => x.ScoreId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<DefenseSession>().WithMany().HasForeignKey(x => x.DefenseSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.ScorerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DefenseEvidence>(entity =>
        {
            entity.ToTable("defense_evidences");
            entity.HasIndex(x => x.DefenseSessionId);
            entity.Property(x => x.FileName).HasMaxLength(255);
            entity.Property(x => x.FilePath).HasMaxLength(512);
            entity.Property(x => x.ContentType).HasMaxLength(100);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasOne<DefenseSession>().WithMany().HasForeignKey(x => x.DefenseSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.CapturedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.CapturedByLecturerId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<DefenseComment>(entity =>
        {
            entity.ToTable("defense_comments");
            entity.HasIndex(x => new { x.DefenseSessionId, x.ReviewerId }).IsUnique();
            entity.Property(x => x.Content).HasMaxLength(2000);
            entity.HasOne<DefenseSession>().WithMany().HasForeignKey(x => x.DefenseSessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<GroupResult>(entity =>
        {
            entity.ToTable("group_results");
            entity.HasIndex(x => new { x.GroupId, x.SemesterId }).IsUnique();
            entity.Property(x => x.Review1Result).HasConversion<string>();
            entity.Property(x => x.Review2Result).HasConversion<string>();
            entity.Property(x => x.Review3Result).HasConversion<string>();
            entity.Property(x => x.SupervisorResult).HasConversion<string>();
            entity.Property(x => x.Defense1Result).HasConversion<string>();
            entity.Property(x => x.Defense2Result).HasConversion<string>();
            entity.Property(x => x.FinalResult).HasConversion<string>();
            entity.HasOne<CapstoneGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReviewRound>(entity =>
        {
            entity.ToTable("review_rounds");
            entity.HasIndex(x => new { x.SemesterId, x.Type }).IsUnique();
            entity.Property(x => x.Type).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>().HasDefaultValue(ReviewRoundStatus.Draft);
            entity.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TrainingDepartment>().WithMany().HasForeignKey(x => x.CreatedByModeratorId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<StudentSlotRegistration>(entity =>
        {
            entity.ToTable("student_slot_registrations", table =>
            {
                table.HasCheckConstraint("ck_student_slot_registrations_day_of_week", "day_of_week BETWEEN 1 AND 6");
                table.HasCheckConstraint("ck_student_slot_registrations_slot", "slot BETWEEN 1 AND 5");
            });
            entity.HasIndex(x => new { x.ReviewRoundId, x.GroupId, x.DayOfWeek, x.Slot }).IsUnique();
            entity.HasIndex(x => new { x.ReviewRoundId, x.DayOfWeek, x.Slot });
            entity.HasOne<ReviewRound>().WithMany().HasForeignKey(x => x.ReviewRoundId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CapstoneGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Student>().WithMany().HasForeignKey(x => x.RegisteredByStudentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ReviewAttendance>(entity =>
        {
            entity.ToTable("review_attendances");
            entity.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.HasOne<ReviewSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.MarkedByLecturerId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        });
        modelBuilder.Entity<ReviewComment>(entity =>
        {
            entity.ToTable("review_comments");
            entity.HasIndex(x => new { x.SessionId, x.GroupId });
            entity.Property(x => x.Content).HasMaxLength(2000);
            entity.HasOne<ReviewSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CapstoneGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSystem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
            entity.Property(x => x.Type).HasConversion<string>();
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceImmutableData();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceImmutableData();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceImmutableData();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void EnforceImmutableData()
    {
        if (ChangeTracker.Entries<AuditLog>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Audit logs are append-only and cannot be changed or deleted.");
        }

        if (ChangeTracker.Entries<Score>()
            .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted)
            .Any(entry => entry.OriginalValues.GetValue<bool>(nameof(Score.IsLocked))))
        {
            throw new InvalidOperationException("Locked scores cannot be changed or deleted.");
        }
    }
}

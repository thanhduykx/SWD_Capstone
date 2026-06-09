using CPMS.Core.Entities;
using CPMS.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Infrastructure.Data;

public sealed class CpmsDbContext(DbContextOptions<CpmsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Lecturer> Lecturers => Set<Lecturer>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<TrainingDepartment> TrainingDepartments => Set<TrainingDepartment>();
    public DbSet<SystemAdministrator> SystemAdministrators => Set<SystemAdministrator>();
    public DbSet<EvaluationPanel> EvaluationPanels => Set<EvaluationPanel>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<CapstoneGroup> CapstoneGroups => Set<CapstoneGroup>();
    public DbSet<Syllabus> Syllabuses => Set<Syllabus>();
    public DbSet<Clo> Clos => Set<Clo>();
    public DbSet<RuleKeyword> RuleKeywords => Set<RuleKeyword>();
    public DbSet<CapstoneDocument> Documents => Set<CapstoneDocument>();
    public DbSet<EvaluationReport> EvaluationReports => Set<EvaluationReport>();
    public DbSet<EvaluationDetail> EvaluationDetails => Set<EvaluationDetail>();
    public DbSet<InlineComment> InlineComments => Set<InlineComment>();
    public DbSet<ReviewAvailability> ReviewAvailabilities => Set<ReviewAvailability>();
    public DbSet<ReviewSession> ReviewSessions => Set<ReviewSession>();
    public DbSet<GroupReviewSlot> GroupReviewSlots => Set<GroupReviewSlot>();
    public DbSet<ReviewChecklistSubmission> ReviewChecklistSubmissions => Set<ReviewChecklistSubmission>();
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
    public DbSet<GroupResult> GroupResults => Set<GroupResult>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

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
        modelBuilder.Entity<EvaluationPanel>(entity =>
        {
            entity.ToTable("evaluation_panels");
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Semester>(entity =>
        {
            entity.ToTable("semesters");
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
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Syllabus>(entity =>
        {
            entity.ToTable("syllabuses");
            entity.HasOne<TrainingDepartment>().WithMany().HasForeignKey(x => x.TrainingDepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Clo>(entity =>
        {
            entity.ToTable("clos");
            entity.HasIndex(x => new { x.SyllabusId, x.Code }).IsUnique();
            entity.Property(x => x.Weight).HasPrecision(5, 4);
            entity.HasOne<Syllabus>().WithMany().HasForeignKey(x => x.SyllabusId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RuleKeyword>(entity =>
        {
            entity.ToTable("rule_keywords");
            entity.Property(x => x.Weight).HasPrecision(5, 4);
            entity.HasOne<Clo>().WithMany().HasForeignKey(x => x.CloId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<SystemAdministrator>().WithMany().HasForeignKey(x => x.CreatedByAdminId)
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
        modelBuilder.Entity<EvaluationReport>(entity =>
        {
            entity.ToTable("evaluation_reports");
            entity.HasIndex(x => x.DocumentId).IsUnique();
            entity.Property(x => x.OverallScore).HasPrecision(5, 2);
            entity.Property(x => x.MatchPercentage).HasPrecision(5, 2);
            entity.Property(x => x.TriggerType).HasConversion<string>();
            entity.HasOne<CapstoneDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<EvaluationPanel>().WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<EvaluationDetail>(entity =>
        {
            entity.ToTable("evaluation_details");
            entity.HasIndex(x => new { x.ReportId, x.CloId }).IsUnique();
            entity.Property(x => x.Score).HasPrecision(5, 2);
            entity.Property(x => x.MaxScore).HasPrecision(5, 2);
            entity.Property(x => x.MatchPercentage).HasPrecision(5, 2);
            entity.HasOne<EvaluationReport>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Clo>().WithMany().HasForeignKey(x => x.CloId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<InlineComment>(entity =>
        {
            entity.ToTable("inline_comments");
            entity.Property(x => x.Status).HasConversion<string>();
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
                table.HasCheckConstraint("ck_review_availabilities_day_of_week", "day_of_week BETWEEN 1 AND 7");
                table.HasCheckConstraint("ck_review_availabilities_slot", "slot BETWEEN 1 AND 8");
            });
            entity.HasIndex(x => new { x.SemesterId, x.LecturerId, x.WeekStartDate, x.DayOfWeek, x.Slot }).IsUnique();
            entity.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.LecturerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReviewSession>(entity =>
        {
            entity.ToTable("review_sessions", table =>
            {
                table.HasCheckConstraint("ck_review_sessions_slot", "slot BETWEEN 1 AND 8");
            });
            entity.HasIndex(x => new { x.Code, x.SemesterId }).IsUnique();
            entity.HasIndex(x => new { x.SemesterId, x.Type, x.SessionDate, x.Slot });
            entity.Property(x => x.Type).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>().HasDefaultValue(ReviewSessionStatus.Draft);
            entity.HasOne<Semester>().WithMany().HasForeignKey(x => x.SemesterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.Reviewer1Id).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Lecturer>().WithMany().HasForeignKey(x => x.Reviewer2Id).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TrainingDepartment>().WithMany().HasForeignKey(x => x.PublishedByTrainingDepartmentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<GroupReviewSlot>(entity =>
        {
            entity.ToTable("group_review_slots");
            entity.HasIndex(x => new { x.SessionId, x.GroupId }).IsUnique();
            entity.Property(x => x.Result).HasConversion<string>();
            entity.HasOne<ReviewSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CapstoneGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ReviewChecklistSubmission>(entity =>
        {
            entity.ToTable("review_checklist_submissions");
            entity.HasIndex(x => new { x.SessionId, x.ReviewerId }).IsUnique();
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
            entity.HasIndex(x => new { x.SemesterId, x.ReviewType, x.WeekStartDate });
            entity.Property(x => x.ReviewType).HasConversion<string>();
            entity.Property(x => x.Subject).HasMaxLength(250);
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

        foreach (var entry in ChangeTracker.Entries<Score>()
                     .Where(x => x.State is EntityState.Modified or EntityState.Deleted))
        {
            if (entry.OriginalValues.GetValue<bool>(nameof(Score.IsLocked)))
            {
                throw new InvalidOperationException("Locked scores cannot be changed or deleted.");
            }
        }
    }
}

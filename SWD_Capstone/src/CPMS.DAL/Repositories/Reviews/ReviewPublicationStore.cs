using System.Data;
using CPMS.BLL.Abstractions.Messaging;
using CPMS.BLL.Reviews.Publication;
using CPMS.DAL.Entities;
using CPMS.BLL.Enums;
using CPMS.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CPMS.DAL.Repositories.Reviews;

public sealed class ReviewPublicationStore(CpmsDbContext dbContext, IEmailSender emailSender)
    : IReviewPublicationStore
{
    public async Task<int?> FindModeratorIdAsync(
        int userId, bool allowFallback, CancellationToken cancellationToken)
    {
        var moderatorId = await dbContext.TrainingDepartments.Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync(cancellationToken);
        if (moderatorId is null && allowFallback)
        {
            moderatorId = await dbContext.TrainingDepartments.OrderBy(x => x.Id)
                .Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken);
        }
        return moderatorId;
    }

    public async Task<TResult> ExecuteLockedAsync<TResult>(
        ReviewPublicationScope scope,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(operation);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);
        var lockKey = $"review-round:{scope.SemesterId}:{scope.ReviewType}:{scope.WeekStart:yyyy-MM-dd}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        TResult? result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<ReviewPublicationSnapshot> LoadSnapshotAsync(
        ReviewPublicationScope scope, int requiredSlotCount, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var round = await dbContext.ReviewRounds
            .Where(x => x.SemesterId == scope.SemesterId && x.Type == scope.ReviewType &&
                        x.WeekStartDate == scope.WeekStart)
            .Select(x => new { x.Id, x.Status }).SingleOrDefaultAsync(cancellationToken);
        if (round is null)
        {
            return EmptySnapshot();
        }
        var alreadyPublished = await dbContext.ReviewSchedulePublications.AnyAsync(x =>
            x.SemesterId == scope.SemesterId && x.ReviewType == scope.ReviewType &&
            x.WeekStartDate == scope.WeekStart, cancellationToken);
        var weekStartDate = DateTime.SpecifyKind(
            scope.WeekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        DateTime weekEndDate = weekStartDate.AddDays(7);
        var sessionRows = await dbContext.ReviewSessions
            .Where(x => x.SemesterId == scope.SemesterId && x.Type == scope.ReviewType &&
                        x.SessionDate >= weekStartDate && x.SessionDate < weekEndDate &&
                        x.Status == ReviewSessionStatus.Draft)
            .OrderBy(x => x.SessionDate).ThenBy(x => x.Slot)
            .Select(x => new
            {
                x.Id, x.Code, x.Type, x.SessionDate, x.Slot, x.Room, x.Reviewer1Id, x.Reviewer2Id
            }).ToListAsync(cancellationToken);
        PublicationSession[] sessions = sessionRows.Select(x => new PublicationSession(
            x.Id, x.Code, x.Type, x.SessionDate, x.Slot, x.Room,
            new[] { x.Reviewer1Id, x.Reviewer2Id }.Where(id => id.HasValue).Select(id => id!.Value).ToArray()))
            .ToArray();
        List<int> activeGroupIds = await dbContext.CapstoneGroups
            .Where(x => x.SemesterId == scope.SemesterId && x.Status == GroupStatus.Active)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        var fullyRegisteredCount = await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == round.Id && activeGroupIds.Contains(x.GroupId))
            .GroupBy(x => x.GroupId)
            .CountAsync(x => x.Count() == requiredSlotCount, cancellationToken);
        var sessionIds = sessions.Select(x => x.Id).ToArray();
        var groupSessionRows = await dbContext.GroupReviewSlots.Where(x => sessionIds.Contains(x.SessionId))
            .Select(x => new { x.GroupId, x.SessionId }).ToListAsync(cancellationToken);
        var scheduledGroupIds = groupSessionRows.Select(x => x.GroupId).Distinct().ToArray();

        var reviewerIds = sessions.SelectMany(x => x.ReviewerIds).Distinct().ToArray();
        var reviewerRows = await dbContext.Lecturers.Where(x => reviewerIds.Contains(x.Id))
            .Join(dbContext.Users, lecturer => lecturer.UserId, user => user.Id,
                (lecturer, user) => new
                {
                    lecturer.Id, lecturer.FullName, UserId = user.Id, user.Email
                })
            .ToListAsync(cancellationToken);
        PublicationRecipient[] reviewerRecipients = reviewerRows.Select(reviewer => new PublicationRecipient(
            reviewer.UserId,
            reviewer.Email,
            reviewer.FullName,
            null,
            null,
            sessions.Where(x => x.ReviewerIds.Contains(reviewer.Id)).Select(x => x.Id).ToArray()))
            .ToArray();

        var groupRows = await dbContext.CapstoneGroups.Where(x => scheduledGroupIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.Code,
                ProjectName = dbContext.Topics.Where(topic => topic.Id == x.TopicId)
                    .Select(topic => topic.NameVn).FirstOrDefault() ?? "Capstone Project"
            }).ToListAsync(cancellationToken);
        var studentRows = await dbContext.Students.Where(x => x.GroupId.HasValue && scheduledGroupIds.Contains(x.GroupId.Value))
            .Join(dbContext.Users, student => student.UserId, user => user.Id,
                (student, user) => new
                {
                    GroupId = student.GroupId!.Value,
                    student.Code,
                    student.FullName,
                    student.IsLeader,
                    UserId = user.Id,
                    user.Email
                })
            .ToListAsync(cancellationToken);
        var studentRecipients = new List<PublicationRecipient>();
        foreach (var group in groupRows)
        {
            int[] groupSessionIds = groupSessionRows.Where(x => x.GroupId == group.Id)
                .Select(x => x.SessionId).Distinct().ToArray();
            foreach (var student in studentRows.Where(x => x.GroupId == group.Id && !string.IsNullOrWhiteSpace(x.Email))
                         .OrderByDescending(x => x.IsLeader).ThenBy(x => x.Code))
            {
                studentRecipients.Add(new PublicationRecipient(
                    student.UserId,
                    student.Email,
                    student.FullName,
                    group.Code,
                    group.ProjectName,
                    groupSessionIds));
            }
        }

        return new ReviewPublicationSnapshot(
            round.Id, round.Status, alreadyPublished, sessions, activeGroupIds.Count,
            fullyRegisteredCount, scheduledGroupIds.Length, reviewerRecipients, studentRecipients);
    }

    public async Task<int> PersistAsync(
        ReviewPublicationScope scope,
        PersistReviewPublication input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(input);
        var publication = new ReviewSchedulePublication
        {
            SemesterId = scope.SemesterId,
            ReviewType = scope.ReviewType,
            WeekStartDate = scope.WeekStart,
            PublishedByTrainingDepartmentId = input.ModeratorId,
            Subject = input.Subject,
            Body = input.Body
        };
        dbContext.ReviewSchedulePublications.Add(publication);
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (PublicationEmail email in input.Emails)
        {
            dbContext.EmailDeliveryLogs.Add(new EmailDeliveryLog
            {
                PublicationId = publication.Id,
                RecipientUserId = email.RecipientUserId,
                RecipientEmail = email.RecipientEmail,
                Subject = email.Subject,
                Body = email.Body,
                Status = EmailDeliveryStatus.Pending
            });
        }
        foreach (PublicationEmail email in input.Emails.DistinctBy(x => x.RecipientUserId))
        {
            dbContext.Notifications.Add(new Notification
            {
                RecipientId = email.RecipientUserId,
                SenderId = input.UserId,
                Type = NotificationType.ReviewSchedulePublished,
                Title = email.Subject,
                Message = email.Body,
                CreatedAt = input.PublishedAt
            });
        }
        List<ReviewSession> sessions = await dbContext.ReviewSessions.Where(x => input.SessionIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        foreach (ReviewSession? session in sessions)
        {
            session.Status = ReviewSessionStatus.Published;
            session.PublishedAt = input.PublishedAt;
            session.PublishedByTrainingDepartmentId = input.ModeratorId;
        }
        ReviewRound round = await dbContext.ReviewRounds.SingleAsync(x => x.Id == input.RoundId, cancellationToken);
        round.Status = ReviewRoundStatus.Published;
        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = input.UserId,
            Action = "PUBLISH_REVIEW_SCHEDULE",
            EntityType = nameof(ReviewSchedulePublication),
            EntityId = publication.Id,
            NewValue = $"{scope.ReviewType}:{scope.WeekStart:yyyy-MM-dd}",
            IpAddress = input.IpAddress,
            UserAgent = input.UserAgent
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return publication.Id;
    }

    public async Task<(int Sent, int Failed)> DispatchEmailsAsync(
        int publicationId, CancellationToken cancellationToken)
    {
        List<EmailDeliveryLog> logs = await dbContext.EmailDeliveryLogs
            .Where(x => x.PublicationId == publicationId && x.Status == EmailDeliveryStatus.Pending)
            .ToListAsync(cancellationToken);
        var sent = 0;
        var failed = 0;
        foreach (EmailDeliveryLog? log in logs)
        {
            try
            {
                await emailSender.SendAsync(log.RecipientEmail, log.Subject, log.Body, cancellationToken);
                log.Status = EmailDeliveryStatus.Sent;
                log.SentAt = DateTime.UtcNow;
                log.ErrorMessage = null;
                sent++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                log.Status = EmailDeliveryStatus.Failed;
                log.ErrorMessage = exception.Message.Length <= 1000 ? exception.Message : exception.Message[..1000];
                failed++;
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return (sent, failed);
    }

    public async Task<IReadOnlyList<int>> GetPendingPublicationIdsAsync(CancellationToken cancellationToken) =>
        await dbContext.EmailDeliveryLogs
            .Where(x => x.Status == EmailDeliveryStatus.Pending && x.PublicationId.HasValue)
            .Select(x => x.PublicationId!.Value)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

    private static ReviewPublicationSnapshot EmptySnapshot() =>
        new(null, null, false, [], 0, 0, 0, [], []);
}

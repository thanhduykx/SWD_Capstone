using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Services;

public sealed class ReviewAssignmentEmailNotifier(
    CpmsDbContext dbContext,
    IReviewEmailSender emailSender)
{
    public async Task<ReviewAssignmentEmailSummary> SendAssignedAsync(
        IReadOnlyCollection<int> sessionIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);

        if (sessionIds.Count == 0)
        {
            return new ReviewAssignmentEmailSummary(0, 0);
        }

        var sessionIdSet = sessionIds.ToHashSet();
        var sessions = await dbContext.ReviewSessions
            .Where(session => sessionIdSet.Contains(session.Id))
            .OrderBy(session => session.SessionDate)
            .ThenBy(session => session.Slot)
            .Select(session => new ReviewAssignmentEmailSession(
                session.Id,
                session.Code,
                session.Type,
                session.Status,
                session.SessionDate,
                session.Slot,
                session.Room,
                session.Reviewer1Id,
                session.Reviewer2Id,
                dbContext.GroupReviewSlots
                    .Where(slot => slot.SessionId == session.Id)
                    .Select(slot => slot.GroupId)
                    .Single(),
                dbContext.GroupReviewSlots
                    .Where(slot => slot.SessionId == session.Id)
                    .Select(slot => dbContext.CapstoneGroups
                        .Where(group => group.Id == slot.GroupId)
                        .Select(group => group.Code)
                        .Single())
                    .Single()))
            .ToListAsync(cancellationToken);

        var reviewerIds = sessions
            .SelectMany(session => new[] { session.Reviewer1Id, session.Reviewer2Id })
            .Where(reviewerId => reviewerId.HasValue)
            .Select(reviewerId => reviewerId!.Value)
            .Distinct()
            .ToArray();

        if (reviewerIds.Length == 0)
        {
            return new ReviewAssignmentEmailSummary(0, 0);
        }

        var reviewers = await dbContext.Lecturers
            .Where(lecturer => reviewerIds.Contains(lecturer.Id))
            .Select(lecturer => new ReviewAssignmentEmailLecturer(
                lecturer.Id,
                lecturer.Code,
                lecturer.FullName,
                dbContext.Users.Where(user => user.Id == lecturer.UserId).Select(user => user.Id).Single(),
                dbContext.Users.Where(user => user.Id == lecturer.UserId).Select(user => user.Email).Single()))
            .ToListAsync(cancellationToken);

        var sent = 0;
        var failed = 0;
        foreach (var reviewer in reviewers)
        {
            var reviewerSessions = sessions
                .Where(session => session.Reviewer1Id == reviewer.Id || session.Reviewer2Id == reviewer.Id)
                .ToArray();
            if (reviewerSessions.Length == 0)
            {
                continue;
            }

            var subject = "CPMS review assignment notification";
            var log = new EmailDeliveryLog
            {
                RecipientUserId = reviewer.UserId,
                RecipientEmail = reviewer.Email,
                Subject = subject,
                Status = EmailDeliveryStatus.Pending
            };
            dbContext.EmailDeliveryLogs.Add(log);

            try
            {
                await emailSender.SendAsync(
                    reviewer.Email,
                    subject,
                    BuildEmailBody(reviewer, reviewerSessions),
                    cancellationToken);
                log.Status = EmailDeliveryStatus.Sent;
                log.SentAt = DateTime.UtcNow;
                sent++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                log.Status = EmailDeliveryStatus.Failed;
                log.ErrorMessage = Truncate(exception.Message, 1000);
                failed++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new ReviewAssignmentEmailSummary(sent, failed);
    }

    private static string BuildEmailBody(
        ReviewAssignmentEmailLecturer reviewer,
        IReadOnlyCollection<ReviewAssignmentEmailSession> sessions)
    {
        var lines = new List<string>
        {
            $"Dear {reviewer.FullName},",
            string.Empty,
            "You have been assigned to review the following capstone group(s).",
            string.Empty
        };

        foreach (var session in sessions.OrderBy(x => x.SessionDate).ThenBy(x => x.Slot))
        {
            lines.Add($"- {session.Type} | {session.Code} | Group {session.GroupCode} | {session.SessionDate:yyyy-MM-dd} | {SlotTimeLabel(session.Slot)} | Room {session.Room} | Status {session.Status}");
        }

        lines.Add(string.Empty);
        lines.Add("Please sign in to CPMS to check the assigned project checklist.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string SlotTimeLabel(int slot) =>
        slot switch
        {
            1 => "Slot 1 (07:00-09:15)",
            2 => "Slot 2 (09:30-11:45)",
            3 => "Slot 3 (12:30-14:45)",
            4 => "Slot 4 (15:00-17:15)",
            5 => "Slot 5 (17:30-19:45)",
            6 => "Slot 6 (20:00-22:15)",
            7 => "Slot 7 (22:30-00:45)",
            8 => "Slot 8 (01:00-03:15)",
            _ => $"Slot {slot}"
        };

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

public sealed record ReviewAssignmentEmailSummary(int SentEmailCount, int FailedEmailCount);

internal sealed record ReviewAssignmentEmailSession(
    int Id,
    string Code,
    ReviewType Type,
    ReviewSessionStatus Status,
    DateTime SessionDate,
    int Slot,
    string Room,
    int? Reviewer1Id,
    int? Reviewer2Id,
    int GroupId,
    string GroupCode);

internal sealed record ReviewAssignmentEmailLecturer(
    int Id,
    string Code,
    string FullName,
    int UserId,
    string Email);

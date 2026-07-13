using CPMS.BLL.Common.Exceptions;
using CPMS.Core.Enums;
using CPMS.Core.Exceptions;
using CPMS.BLL.Reviews.Scheduling;
using CPMS.Core.Services;

namespace CPMS.BLL.Reviews.Publication;

public sealed class ReviewPublicationService(IReviewPublicationStore store) : IReviewPublicationService
{
    public async Task<ReviewSchedulePublicationResult> PublishAsync(
        PublishReviewScheduleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var subject = string.IsNullOrWhiteSpace(command.Subject)
            ? $"CPMS review schedule - {command.ReviewType} - {ReviewWeek.NormalizeStart(command.WeekStart):yyyy-MM-dd}"
            : command.Subject.Trim();
        if (subject.Length > 250)
        {
            throw new BusinessRuleException("Email subject cannot exceed 250 characters.");
        }
        var moderatorId = await store.FindModeratorIdAsync(
            command.UserId, command.AllowModeratorFallback, cancellationToken)
            ?? throw new UnauthorizedAccessException(
                "A Training Department moderator profile is required for publishing review schedules.");
        var scope = new ReviewPublicationScope(
            command.SemesterId, command.ReviewType, ReviewWeek.NormalizeStart(command.WeekStart));
        var persisted = await store.ExecuteLockedAsync(
            scope,
            token => PrepareAndPersistAsync(scope, command, moderatorId, subject, token),
            cancellationToken);
        var delivery = await store.DispatchEmailsAsync(persisted.PublicationId, CancellationToken.None);
        return new ReviewSchedulePublicationResult(
            persisted.PublicationId, persisted.SessionCount, delivery.Sent, delivery.Failed);
    }

    private async Task<(int PublicationId, int SessionCount)> PrepareAndPersistAsync(
        ReviewPublicationScope scope,
        PublishReviewScheduleCommand command,
        int moderatorId,
        string subject,
        CancellationToken cancellationToken)
    {
        var snapshot = await store.LoadSnapshotAsync(
            scope, AssignmentRules.RequiredStudentAvailabilityCount, cancellationToken);
        if (snapshot.RoundId is null)
        {
            throw new BusinessRuleException("The selected review round does not exist.");
        }
        if (snapshot.RoundStatus != ReviewRoundStatus.Closed)
        {
            throw new ConflictException("Only a Closed review round can be published.");
        }
        if (snapshot.AlreadyPublished)
        {
            throw new ConflictException("This review schedule has already been published.");
        }
        if (snapshot.Sessions.Count == 0)
        {
            throw new BusinessRuleException(
                "No review sessions exist for the selected week and review round.");
        }
        if (snapshot.FullyRegisteredGroupCount != snapshot.ActiveGroupCount ||
            snapshot.ScheduledGroupCount != snapshot.ActiveGroupCount)
        {
            throw new IncompleteReviewScheduleException(
                "Every active group must submit five slots and receive an assignment before publication.",
                snapshot.ActiveGroupCount,
                snapshot.FullyRegisteredGroupCount,
                snapshot.ScheduledGroupCount);
        }

        var emails = BuildEmails(command.Message, subject, snapshot);
        var publicationId = await store.PersistAsync(
            scope,
            new PersistReviewPublication(
                snapshot.RoundId.Value,
                moderatorId,
                subject,
                command.Message?.Trim() ?? string.Empty,
                snapshot.Sessions.Select(x => x.Id).ToArray(),
                emails,
                command.UserId,
                command.IpAddress,
                command.UserAgent,
                DateTime.UtcNow),
            cancellationToken);
        return (publicationId, snapshot.Sessions.Count);
    }

    private static IReadOnlyList<PublicationEmail> BuildEmails(
        string? moderatorMessage,
        string reviewerSubject,
        ReviewPublicationSnapshot snapshot)
    {
        var sessions = snapshot.Sessions.ToDictionary(x => x.Id);
        var emails = new List<PublicationEmail>();
        foreach (var recipient in snapshot.ReviewerRecipients)
        {
            emails.Add(new PublicationEmail(
                recipient.UserId,
                recipient.Email,
                reviewerSubject,
                BuildReviewerBody(moderatorMessage, recipient.DisplayName,
                    recipient.SessionIds.Select(id => sessions[id]))));
        }
        foreach (var recipient in snapshot.GroupLeaderRecipients)
        {
            emails.Add(new PublicationEmail(
                recipient.UserId,
                recipient.Email,
                $"[CPMS] Review Schedule Notification for Group {recipient.GroupCode}",
                BuildLeaderBody(moderatorMessage, recipient,
                    recipient.SessionIds.Select(id => sessions[id]))));
        }
        return emails;
    }

    private static string BuildReviewerBody(
        string? moderatorMessage, string name, IEnumerable<PublicationSession> sessions)
    {
        var lines = new List<string> { $"Dear {name},", "", "Your capstone review schedule has been published." };
        AddMessageAndSessions(lines, moderatorMessage, sessions);
        lines.Add("");
        lines.Add("Please sign in to CPMS to review the assigned project checklist.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildLeaderBody(
        string? moderatorMessage, PublicationRecipient recipient, IEnumerable<PublicationSession> sessions)
    {
        var lines = new List<string>
        {
            $"Dear {recipient.DisplayName} (Leader of Group {recipient.GroupCode} - {recipient.ProjectName}),",
            "",
            "Your group's capstone review schedule has been published.",
            "As the group leader, you are automatically notified. Please inform all team members of this schedule."
        };
        AddMessageAndSessions(lines, moderatorMessage, sessions);
        lines.Add("");
        lines.Add("Please sign in to CPMS to view the details.");
        return string.Join(Environment.NewLine, lines);
    }

    private static void AddMessageAndSessions(
        ICollection<string> lines, string? moderatorMessage, IEnumerable<PublicationSession> sessions)
    {
        if (!string.IsNullOrWhiteSpace(moderatorMessage))
        {
            lines.Add("");
            lines.Add(moderatorMessage.Trim());
        }
        lines.Add("");
        foreach (var session in sessions.OrderBy(x => x.SessionDate).ThenBy(x => x.Slot))
        {
            lines.Add($"- {session.Type} | {session.Code} | {session.SessionDate:yyyy-MM-dd} | {SlotTimeLabel(session.Slot)} | Room {session.Room}");
        }
    }

    private static string SlotTimeLabel(int slot) => slot switch
    {
        1 => "Slot 1 (07:00-09:15)",
        2 => "Slot 2 (09:30-11:45)",
        3 => "Slot 3 (12:30-14:45)",
        4 => "Slot 4 (15:00-17:15)",
        5 => "Slot 5 (17:30-19:45)",
        _ => $"Slot {slot}"
    };
}

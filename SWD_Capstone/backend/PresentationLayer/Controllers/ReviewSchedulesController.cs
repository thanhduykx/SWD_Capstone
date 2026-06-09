using System.Globalization;
using System.Net.Mail;
using System.Security.Claims;
using CPMS.Api.Services;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Core.Exceptions;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/review-schedules")]
[Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
public sealed class ReviewSchedulesController(
    CpmsDbContext dbContext,
    IReviewEmailSender emailSender) : ControllerBase
{
    [HttpPost("publish")]
    public async Task<ActionResult<ReviewSchedulePublishResponse>> Publish(
        PublishReviewScheduleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trainingDepartmentId = await CurrentTrainingDepartmentIdAsync(cancellationToken);
        var weekStart = NormalizeWeekStart(request.WeekStart);
        var weekStartDate = DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var weekEndDate = weekStartDate.AddDays(7);

        var sessions = await dbContext.ReviewSessions
            .Where(x => x.SemesterId == request.SemesterId &&
                        x.Type == request.ReviewType &&
                        x.SessionDate >= weekStartDate &&
                        x.SessionDate < weekEndDate &&
                        x.Status != ReviewSessionStatus.Cancelled)
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.Slot)
            .ToListAsync(cancellationToken);
        if (sessions.Count == 0)
        {
            return BadRequest(new { error = "No review sessions exist for the selected week and review round." });
        }

        var reviewerIds = sessions
            .SelectMany(x => new[] { x.Reviewer1Id, x.Reviewer2Id })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();

        var reviewers = await dbContext.Lecturers
            .Where(x => reviewerIds.Contains(x.Id))
            .Select(x => new ReviewPublishLecturer(
                x.Id,
                x.Code,
                x.FullName,
                dbContext.Users.Where(user => user.Id == x.UserId).Select(user => user.Id).Single(),
                dbContext.Users.Where(user => user.Id == x.UserId).Select(user => user.Email).Single()))
            .ToListAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var subject = string.IsNullOrWhiteSpace(request.Subject)
            ? $"CPMS review schedule - {request.ReviewType} - {weekStart:yyyy-MM-dd}"
            : request.Subject.Trim();
        var publication = new ReviewSchedulePublication
        {
            SemesterId = request.SemesterId,
            ReviewType = request.ReviewType,
            WeekStartDate = weekStart,
            PublishedByTrainingDepartmentId = trainingDepartmentId,
            Subject = subject,
            Body = request.Message?.Trim() ?? string.Empty
        };
        dbContext.ReviewSchedulePublications.Add(publication);
        await dbContext.SaveChangesAsync(cancellationToken);

        var sent = 0;
        var failed = 0;
        foreach (var reviewer in reviewers)
        {
            var reviewerSessions = sessions
                .Where(x => x.Reviewer1Id == reviewer.Id || x.Reviewer2Id == reviewer.Id)
                .ToArray();
            var body = BuildEmailBody(request.Message, reviewer, reviewerSessions);
            var log = new EmailDeliveryLog
            {
                PublicationId = publication.Id,
                RecipientUserId = reviewer.UserId,
                RecipientEmail = reviewer.Email,
                Subject = subject,
                Status = EmailDeliveryStatus.Pending
            };
            dbContext.EmailDeliveryLogs.Add(log);

            try
            {
                await emailSender.SendAsync(reviewer.Email, subject, body, cancellationToken);
                log.Status = EmailDeliveryStatus.Sent;
                log.SentAt = DateTime.UtcNow;
                sent++;
            }
            catch (BusinessRuleException)
            {
                throw;
            }
            catch (SmtpException exception)
            {
                log.Status = EmailDeliveryStatus.Failed;
                log.ErrorMessage = exception.Message;
                failed++;
            }
            catch (InvalidOperationException exception)
            {
                log.Status = EmailDeliveryStatus.Failed;
                log.ErrorMessage = exception.Message;
                failed++;
            }
        }

        foreach (var session in sessions)
        {
            session.Status = ReviewSessionStatus.Published;
            session.PublishedAt = DateTime.UtcNow;
            session.PublishedByTrainingDepartmentId = trainingDepartmentId;
        }

        dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = CurrentUserId(),
            Action = "PUBLISH_REVIEW_SCHEDULE",
            EntityType = nameof(ReviewSchedulePublication),
            EntityId = publication.Id,
            NewValue = $"{request.ReviewType}:{weekStart:yyyy-MM-dd}",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Ok(new ReviewSchedulePublishResponse(publication.Id, sessions.Count, sent, failed));
    }

    private async Task<int> CurrentTrainingDepartmentIdAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var trainingDepartmentId = await dbContext.TrainingDepartments
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (trainingDepartmentId is null && User.IsInRole(nameof(UserRole.SystemAdministrator)))
        {
            trainingDepartmentId = await dbContext.TrainingDepartments
                .OrderBy(x => x.Id)
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return trainingDepartmentId
            ?? throw new UnauthorizedAccessException("A Training Department moderator profile is required for publishing review schedules.");
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);

    private static string BuildEmailBody(
        string? moderatorMessage,
        ReviewPublishLecturer reviewer,
        IReadOnlyCollection<ReviewSession> sessions)
    {
        var lines = new List<string>
        {
            $"Dear {reviewer.FullName},",
            string.Empty,
            "Your capstone review schedule has been published.",
        };

        if (!string.IsNullOrWhiteSpace(moderatorMessage))
        {
            lines.Add(string.Empty);
            lines.Add(moderatorMessage.Trim());
        }

        lines.Add(string.Empty);
        foreach (var session in sessions.OrderBy(x => x.SessionDate).ThenBy(x => x.Slot))
        {
            lines.Add($"- {session.Type} | {session.Code} | {session.SessionDate:yyyy-MM-dd} | {SlotTimeLabel(session.Slot)} | Room {session.Room}");
        }

        lines.Add(string.Empty);
        lines.Add("Please sign in to CPMS to review the assigned project checklist.");
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

    private static DateOnly NormalizeWeekStart(DateOnly date)
    {
        var day = (int)date.DayOfWeek;
        var daysFromMonday = day == 0 ? 6 : day - 1;
        return date.AddDays(-daysFromMonday);
    }
}

public sealed record PublishReviewScheduleRequest(
    int SemesterId,
    ReviewType ReviewType,
    DateOnly WeekStart,
    string? Subject,
    string? Message);

public sealed record ReviewSchedulePublishResponse(
    int PublicationId,
    int PublishedSessionCount,
    int SentEmailCount,
    int FailedEmailCount);

internal sealed record ReviewPublishLecturer(
    int Id,
    string Code,
    string FullName,
    int UserId,
    string Email);

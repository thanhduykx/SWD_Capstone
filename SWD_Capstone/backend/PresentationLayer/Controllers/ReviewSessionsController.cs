using System.Globalization;
using System.Security.Claims;
using CPMS.Api.Services;
using CPMS.Core.Entities;
using CPMS.Core.Enums;
using CPMS.Core.Exceptions;
using CPMS.Core.Services;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/review-sessions")]
[Authorize]
public sealed class ReviewSessionsController(
    CpmsDbContext dbContext,
    AssignmentRules rules,
    ReviewAssignmentEmailNotifier assignmentEmailNotifier) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<ReviewSessionResponse>> Create(
        CreateReviewSessionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await CreateSessionAsync(
            new BulkAssignReviewSessionRequest(
                request.Code,
                request.GroupId,
                request.GroupPosition,
                request.Type,
                [request.Reviewer1Id, request.Reviewer2Id],
                request.PreviousReviewerIds,
                request.Slot,
                request.Room,
                request.SessionDate),
            cancellationToken);

        await assignmentEmailNotifier.SendAssignedAsync([response.Id], cancellationToken);
        return CreatedAtAction(nameof(Create), new { response.Id }, response);
    }

    [HttpPost("bulk-assign")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<BulkAssignReviewSessionsResponse>> BulkAssign(
        BulkAssignReviewSessionsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Sessions.Count == 0)
        {
            return BadRequest(new { error = "At least one review session assignment is required." });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var responses = new List<ReviewSessionResponse>();
        foreach (var item in request.Sessions)
        {
            responses.Add(await CreateSessionAsync(item, cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        var emailSummary = await assignmentEmailNotifier.SendAssignedAsync(
            responses.Select(response => response.Id).ToArray(),
            cancellationToken);

        return Ok(new BulkAssignReviewSessionsResponse(
            emailSummary.SentEmailCount,
            emailSummary.FailedEmailCount,
            responses));
    }

    [HttpPatch("{sessionId:int}")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<ReviewSessionResponse>> Update(
        int sessionId,
        UpdateReviewSessionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await dbContext.ReviewSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }

        rules.ValidateReviewSlot(request.Slot);
        var sessionDate = NormalizeSessionDate(request.SessionDate);
        var reviewerIds = request.ReviewerIds.Distinct().ToArray();
        var groupId = await dbContext.GroupReviewSlots
            .Where(x => x.SessionId == sessionId)
            .Select(x => x.GroupId)
            .SingleAsync(cancellationToken);
        var supervisorId = await dbContext.CapstoneGroups
            .Where(x => x.Id == groupId)
            .Select(x => x.LecturerId)
            .SingleAsync(cancellationToken);

        rules.ValidateReviewAssignment(supervisorId, reviewerIds, request.PreviousReviewerIds);
        await EnsureLecturersExistAsync(reviewerIds, cancellationToken);
        await EnsureReviewersSubmittedAvailabilityAsync(session.SemesterId, sessionDate, request.Slot, reviewerIds, cancellationToken);
        await EnsureNoSlotConflictAsync(session.Id, sessionDate, request.Slot, reviewerIds, cancellationToken);

        session.Code = request.Code.Trim();
        session.Room = request.Room.Trim();
        session.SessionDate = sessionDate;
        session.DayOfWeek = IsoDayOfWeek(sessionDate);
        session.Slot = request.Slot;
        session.Reviewer1Id = reviewerIds.ElementAtOrDefault(0);
        session.Reviewer2Id = reviewerIds.ElementAtOrDefault(1) == 0 ? null : reviewerIds.ElementAtOrDefault(1);
        session.Status = request.Status;

        var existingSubmissions = await dbContext.ReviewChecklistSubmissions
            .Where(x => x.SessionId == session.Id)
            .ToListAsync(cancellationToken);
        foreach (var staleSubmission in existingSubmissions.Where(x => !reviewerIds.Contains(x.ReviewerId)))
        {
            dbContext.ReviewChecklistSubmissions.Remove(staleSubmission);
        }

        foreach (var reviewerId in reviewerIds.Where(id => existingSubmissions.All(x => x.ReviewerId != id)))
        {
            dbContext.ReviewChecklistSubmissions.Add(NewSubmission(session, groupId, reviewerId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(await MapSessionAsync(session.Id, cancellationToken));
    }

    [HttpGet("my")]
    [Authorize(Roles = "Lecturer")]
    public async Task<IReadOnlyList<MyReviewSessionResponse>> GetMySessions(CancellationToken cancellationToken)
    {
        var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
        if (lecturerId is null)
        {
            return [];
        }

        return await dbContext.ReviewChecklistSubmissions
            .Where(submission => submission.ReviewerId == lecturerId.Value)
            .OrderBy(submission => dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId).Select(session => session.SessionDate).Single())
            .ThenBy(submission => dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId).Select(session => session.Slot).Single())
            .Select(submission => new MyReviewSessionResponse(
                submission.SessionId,
                submission.Id,
                dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId).Select(session => session.Code).Single(),
                submission.Type,
                dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId).Select(session => session.Status).Single(),
                dbContext.CapstoneGroups.Where(group => group.Id == submission.GroupId).Select(group => group.Code).Single(),
                dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId).Select(session => session.SessionDate).Single(),
                dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId).Select(session => session.Slot).Single(),
                dbContext.ReviewSessions.Where(session => session.Id == submission.SessionId).Select(session => session.Room).Single(),
                submission.Status,
                submission.LastSavedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<ReviewSessionResponse> CreateSessionAsync(
        BulkAssignReviewSessionRequest request,
        CancellationToken cancellationToken)
    {
        rules.ValidateReviewSlot(request.Slot);
        var sessionDate = NormalizeSessionDate(request.SessionDate);

        var reviewerIds = request.ReviewerIds.Distinct().ToArray();
        await EnsureLecturersExistAsync(reviewerIds, cancellationToken);

        var group = await dbContext.CapstoneGroups.SingleOrDefaultAsync(x => x.Id == request.GroupId, cancellationToken);
        if (group is null)
        {
            throw new KeyNotFoundException("Capstone group does not exist.");
        }

        rules.ValidateReviewAssignment(
            group.LecturerId,
            reviewerIds,
            request.Type == ReviewType.Review2 ? request.PreviousReviewerIds : null);
        await EnsureReviewersSubmittedAvailabilityAsync(group.SemesterId, sessionDate, request.Slot, reviewerIds, cancellationToken);

        var alreadyScheduled = await dbContext.GroupReviewSlots.AnyAsync(slot =>
            slot.GroupId == group.Id &&
            dbContext.ReviewSessions.Any(session => session.Id == slot.SessionId && session.Type == request.Type),
            cancellationToken);
        rules.EnsureGroupCanBeScheduledForReviewType(alreadyScheduled);
        await EnsureNoSlotConflictAsync(null, sessionDate, request.Slot, reviewerIds, cancellationToken);

        var session = new ReviewSession
        {
            Code = request.Code.Trim(),
            SemesterId = group.SemesterId,
            DayOfWeek = IsoDayOfWeek(sessionDate),
            Slot = request.Slot,
            Room = request.Room.Trim(),
            Type = request.Type,
            Reviewer1Id = reviewerIds.ElementAtOrDefault(0),
            Reviewer2Id = reviewerIds.ElementAtOrDefault(1) == 0 ? null : reviewerIds.ElementAtOrDefault(1),
            SessionDate = sessionDate,
            Status = ReviewSessionStatus.Draft
        };

        dbContext.ReviewSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.GroupReviewSlots.Add(new GroupReviewSlot
        {
            SessionId = session.Id,
            GroupId = group.Id,
            GroupPosition = request.GroupPosition,
            ConflictFlag = false
        });

        foreach (var reviewerId in reviewerIds)
        {
            dbContext.ReviewChecklistSubmissions.Add(NewSubmission(session, group.Id, reviewerId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapSessionAsync(session.Id, cancellationToken);
    }

    private async Task EnsureLecturersExistAsync(IReadOnlyCollection<int> reviewerIds, CancellationToken cancellationToken)
    {
        if (reviewerIds.Count == 0)
        {
            throw new BusinessRuleException("At least one reviewer is required.");
        }

        var existingCount = await dbContext.Lecturers.CountAsync(x => reviewerIds.Contains(x.Id), cancellationToken);
        if (existingCount != reviewerIds.Count)
        {
            throw new KeyNotFoundException("One or more reviewers do not exist.");
        }
    }

    private async Task EnsureReviewersSubmittedAvailabilityAsync(
        int semesterId,
        DateTime sessionDate,
        int slot,
        IReadOnlyCollection<int> reviewerIds,
        CancellationToken cancellationToken)
    {
        var weekStart = NormalizeWeekStart(DateOnly.FromDateTime(sessionDate));
        var dayOfWeek = IsoDayOfWeek(sessionDate);
        var availableReviewerCount = await dbContext.ReviewAvailabilities
            .Where(x => x.SemesterId == semesterId &&
                        x.WeekStartDate == weekStart &&
                        x.DayOfWeek == dayOfWeek &&
                        x.Slot == slot &&
                        reviewerIds.Contains(x.LecturerId) &&
                        dbContext.ReviewAvailabilitySubmissions.Any(submission =>
                            submission.SemesterId == x.SemesterId &&
                            submission.LecturerId == x.LecturerId &&
                            submission.WeekStartDate == x.WeekStartDate &&
                            submission.SubmittedAt != null))
            .Select(x => x.LecturerId)
            .Distinct()
            .CountAsync(cancellationToken);
        if (availableReviewerCount != reviewerIds.Count)
        {
            throw new BusinessRuleException("All reviewers must submit availability for the selected date and slot before scheduling.");
        }
    }

    private async Task EnsureNoSlotConflictAsync(
        int? currentSessionId,
        DateTime sessionDate,
        int slot,
        IReadOnlyCollection<int> reviewerIds,
        CancellationToken cancellationToken)
    {
        var hasConflict = await dbContext.ReviewSessions.AnyAsync(session =>
            (!currentSessionId.HasValue || session.Id != currentSessionId.Value) &&
            session.SessionDate.Date == sessionDate.Date &&
            session.Slot == slot &&
            session.Status != ReviewSessionStatus.Cancelled &&
            ((session.Reviewer1Id.HasValue && reviewerIds.Contains(session.Reviewer1Id.Value)) ||
             (session.Reviewer2Id.HasValue && reviewerIds.Contains(session.Reviewer2Id.Value))),
            cancellationToken);
        rules.EnsureLecturerAvailableForReviewSlot(hasConflict);
    }

    private async Task<ReviewSessionResponse> MapSessionAsync(int sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.ReviewSessions.SingleAsync(x => x.Id == sessionId, cancellationToken);
        var slot = await dbContext.GroupReviewSlots.SingleAsync(x => x.SessionId == sessionId, cancellationToken);
        var groupCode = await dbContext.CapstoneGroups
            .Where(x => x.Id == slot.GroupId)
            .Select(x => x.Code)
            .SingleAsync(cancellationToken);
        var reviewerIds = new[] { session.Reviewer1Id, session.Reviewer2Id }
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();

        return new ReviewSessionResponse(
            session.Id,
            session.Code,
            session.SemesterId,
            slot.GroupId,
            groupCode,
            slot.GroupPosition,
            session.Type,
            session.Status,
            reviewerIds,
            session.SessionDate,
            session.Slot,
            session.Room);
    }

    private static ReviewChecklistSubmission NewSubmission(ReviewSession session, int groupId, int reviewerId) =>
        new()
        {
            SessionId = session.Id,
            GroupId = groupId,
            ReviewerId = reviewerId,
            Type = session.Type,
            Status = ReviewSubmissionStatus.Draft,
            LastSavedAt = DateTime.UtcNow
        };

    private async Task<int?> CurrentLecturerIdAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        return await dbContext.Lecturers
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);

    private static int IsoDayOfWeek(DateTime date)
    {
        var value = (int)date.DayOfWeek;
        return value == 0 ? 7 : value;
    }

    private static DateTime NormalizeSessionDate(DateTime date) =>
        DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

    private static DateOnly NormalizeWeekStart(DateOnly date)
    {
        var day = (int)date.DayOfWeek;
        var daysFromMonday = day == 0 ? 6 : day - 1;
        return date.AddDays(-daysFromMonday);
    }
}

public sealed record CreateReviewSessionRequest(
    string Code,
    int GroupId,
    int GroupPosition,
    ReviewType Type,
    int Reviewer1Id,
    int Reviewer2Id,
    int[] PreviousReviewerIds,
    int Slot,
    string Room,
    DateTime SessionDate);

public sealed record BulkAssignReviewSessionsRequest(IReadOnlyCollection<BulkAssignReviewSessionRequest> Sessions);

public sealed record BulkAssignReviewSessionsResponse(
    int SentEmailCount,
    int FailedEmailCount,
    IReadOnlyCollection<ReviewSessionResponse> Sessions);

public sealed record BulkAssignReviewSessionRequest(
    string Code,
    int GroupId,
    int GroupPosition,
    ReviewType Type,
    IReadOnlyCollection<int> ReviewerIds,
    IReadOnlyCollection<int> PreviousReviewerIds,
    int Slot,
    string Room,
    DateTime SessionDate);

public sealed record UpdateReviewSessionRequest(
    string Code,
    IReadOnlyCollection<int> ReviewerIds,
    IReadOnlyCollection<int> PreviousReviewerIds,
    int Slot,
    string Room,
    DateTime SessionDate,
    ReviewSessionStatus Status);

public sealed record ReviewSessionResponse(
    int Id,
    string Code,
    int SemesterId,
    int GroupId,
    string GroupCode,
    int GroupPosition,
    ReviewType Type,
    ReviewSessionStatus Status,
    IReadOnlyCollection<int> ReviewerIds,
    DateTime SessionDate,
    int Slot,
    string Room);

public sealed record MyReviewSessionResponse(
    int SessionId,
    int SubmissionId,
    string Code,
    ReviewType Type,
    ReviewSessionStatus SessionStatus,
    string GroupCode,
    DateTime SessionDate,
    int Slot,
    string Room,
    ReviewSubmissionStatus SubmissionStatus,
    DateTime LastSavedAt);

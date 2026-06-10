using System.Globalization;
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
[Route("api/review-scheduling")]
[Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
public sealed class ReviewSchedulingController(
    CpmsDbContext dbContext,
    AssignmentRules rules,
    ReviewAssignmentEmailNotifier assignmentEmailNotifier) : ControllerBase
{
    [HttpGet("board")]
    public async Task<ActionResult<ReviewSchedulingBoardResponse>> GetBoard(
        int semesterId,
        ReviewType reviewType,
        DateOnly weekStart,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Semesters.AnyAsync(x => x.Id == semesterId, cancellationToken))
        {
            return BadRequest(new { error = "Semester does not exist." });
        }

        weekStart = NormalizeWeekStart(weekStart);
        var weekStartDate = DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var weekEndDate = weekStartDate.AddDays(7);

        var lecturers = await dbContext.Lecturers
            .OrderBy(x => x.Code)
            .Select(x => new ReviewSchedulingLecturerResponse(
                x.Id,
                x.Code,
                x.FullName,
                x.Department,
                dbContext.Users.Where(user => user.Id == x.UserId).Select(user => user.Email).Single()))
            .ToListAsync(cancellationToken);

        var availability = await dbContext.ReviewAvailabilities
            .Where(x => x.SemesterId == semesterId && x.WeekStartDate == weekStart &&
                        dbContext.ReviewAvailabilitySubmissions.Any(submission =>
                            submission.SemesterId == x.SemesterId &&
                            submission.LecturerId == x.LecturerId &&
                            submission.WeekStartDate == x.WeekStartDate &&
                            submission.SubmittedAt != null))
            .OrderBy(x => x.LecturerId)
            .ThenBy(x => x.DayOfWeek)
            .ThenBy(x => x.Slot)
            .Select(x => new ReviewSchedulingAvailabilityResponse(x.LecturerId, x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);

        var availabilitySubmissions = await dbContext.ReviewAvailabilitySubmissions
            .Where(x => x.SemesterId == semesterId && x.WeekStartDate == weekStart)
            .OrderBy(x => x.LecturerId)
            .Select(x => new ReviewSchedulingAvailabilitySubmissionResponse(
                x.LecturerId,
                x.SubmittedAt != null,
                x.SubmittedAt,
                dbContext.ReviewAvailabilities.Count(slot =>
                    slot.SemesterId == x.SemesterId &&
                    slot.LecturerId == x.LecturerId &&
                    slot.WeekStartDate == x.WeekStartDate)))
            .ToListAsync(cancellationToken);

        var groups = await dbContext.CapstoneGroups
            .Where(x => x.SemesterId == semesterId)
            .OrderBy(x => x.Code)
            .Select(x => new ReviewSchedulingGroupResponse(
                x.Id,
                x.Code,
                dbContext.Topics.Where(topic => topic.Id == x.TopicId).Select(topic => topic.NameEn).Single(),
                x.LecturerId,
                dbContext.Lecturers.Where(lecturer => lecturer.Id == x.LecturerId).Select(lecturer => lecturer.Code).Single(),
                dbContext.Students.Count(student => student.GroupId == x.Id)))
            .ToListAsync(cancellationToken);

        var sessions = await dbContext.ReviewSessions
            .Where(x => x.SemesterId == semesterId &&
                        x.Type == reviewType &&
                        x.SessionDate >= weekStartDate &&
                        x.SessionDate < weekEndDate)
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.Slot)
            .Select(x => new ReviewSchedulingSessionResponse(
                x.Id,
                x.Code,
                dbContext.GroupReviewSlots.Where(slot => slot.SessionId == x.Id).Select(slot => slot.GroupId).Single(),
                dbContext.GroupReviewSlots.Where(slot => slot.SessionId == x.Id)
                    .Select(slot => dbContext.CapstoneGroups.Where(group => group.Id == slot.GroupId).Select(group => group.Code).Single())
                    .Single(),
                x.Type,
                x.Status,
                new[] { x.Reviewer1Id, x.Reviewer2Id }.Where(id => id.HasValue).Select(id => id!.Value).ToArray(),
                x.SessionDate,
                x.DayOfWeek,
                x.Slot,
                x.Room))
            .ToListAsync(cancellationToken);

        return Ok(new ReviewSchedulingBoardResponse(semesterId, reviewType, weekStart, lecturers, availability, availabilitySubmissions, groups, sessions));
    }

    [HttpPost("random-assign")]
    public async Task<ActionResult<RandomAssignReviewSessionsResponse>> RandomAssign(
        RandomAssignReviewSessionsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var semester = await dbContext.Semesters.SingleOrDefaultAsync(x => x.Id == request.SemesterId, cancellationToken);
        if (semester is null)
        {
            return BadRequest(new { error = "Semester does not exist." });
        }

        var reviewersPerSession = request.ReviewersPerSession == 0 ? 2 : request.ReviewersPerSession;
        if (reviewersPerSession is < 1 or > 2)
        {
            return BadRequest(new { error = "Reviewers per session must be 1 or 2." });
        }

        var weekStart = NormalizeWeekStart(request.WeekStart);
        var weekStartDate = DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var weekEndDate = weekStartDate.AddDays(7);
        var submittedAvailability = await dbContext.ReviewAvailabilities
            .Where(x => x.SemesterId == request.SemesterId && x.WeekStartDate == weekStart &&
                        dbContext.ReviewAvailabilitySubmissions.Any(submission =>
                            submission.SemesterId == x.SemesterId &&
                            submission.LecturerId == x.LecturerId &&
                            submission.WeekStartDate == x.WeekStartDate &&
                            submission.SubmittedAt != null))
            .Select(x => new AvailabilityCandidate(x.LecturerId, x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);
        if (submittedAvailability.Count == 0)
        {
            return BadRequest(new { error = "No submitted lecturer availability exists for this week." });
        }

        var scheduledGroupIds = await dbContext.GroupReviewSlots
            .Where(slot => dbContext.ReviewSessions.Any(session =>
                session.Id == slot.SessionId &&
                session.SemesterId == request.SemesterId &&
                session.Type == request.ReviewType &&
                session.Status != ReviewSessionStatus.Cancelled))
            .Select(slot => slot.GroupId)
            .ToListAsync(cancellationToken);
        var scheduledGroupIdSet = scheduledGroupIds.ToHashSet();

        var groups = await dbContext.CapstoneGroups
            .Where(x => x.SemesterId == request.SemesterId &&
                        x.Status == GroupStatus.Active &&
                        dbContext.Students.Any(student => student.GroupId == x.Id) &&
                        !scheduledGroupIdSet.Contains(x.Id))
            .OrderBy(x => x.Code)
            .Select(x => new RandomAssignGroupCandidate(
                x.Id,
                x.Code,
                x.LecturerId))
            .ToListAsync(cancellationToken);
        if (groups.Count == 0)
        {
            return Ok(new RandomAssignReviewSessionsResponse(0, 0, 0, 0, [], []));
        }

        var previousReviewerIdsByGroup = await LoadPreviousReviewersByGroupAsync(
            request.SemesterId,
            request.ReviewType,
            groups.Select(x => x.Id).ToArray(),
            cancellationToken);
        var occupiedReviewerSlots = await LoadOccupiedReviewerSlotsAsync(
            request.SemesterId,
            weekStartDate,
            weekEndDate,
            cancellationToken);
        var usedSessionCodes = await dbContext.ReviewSessions
            .Where(x => x.SemesterId == request.SemesterId)
            .Select(x => x.Code)
            .ToListAsync(cancellationToken);
        var usedSessionCodeSet = usedSessionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var random = request.Seed.HasValue ? new Random(request.Seed.Value) : new Random();
        var assignedSessions = new List<ReviewSchedulingSessionResponse>();
        var unassignedGroups = new List<RandomAssignUnassignedGroupResponse>();
        var randomGroups = Shuffle(groups, random);
        var roomPrefix = string.IsNullOrWhiteSpace(request.RoomPrefix) ? "AUTO" : request.RoomPrefix.Trim();

        foreach (var group in randomGroups)
        {
            var previousReviewerIds = previousReviewerIdsByGroup.TryGetValue(group.Id, out var previous)
                ? previous
                : [];
            var eligibleSlotGroups = submittedAvailability
                .Where(slot => slot.LecturerId != group.SupervisorId &&
                               !previousReviewerIds.Contains(slot.LecturerId) &&
                               !occupiedReviewerSlots.Contains(OccupiedKey(slot.LecturerId, SessionDateFromWeekStart(weekStart, slot.DayOfWeek), slot.Slot)))
                .GroupBy(slot => new { slot.DayOfWeek, slot.Slot })
                .Where(slotGroup => slotGroup.Select(slot => slot.LecturerId).Distinct().Count() >= reviewersPerSession)
                .ToArray();

            if (eligibleSlotGroups.Length == 0)
            {
                unassignedGroups.Add(new RandomAssignUnassignedGroupResponse(
                    group.Id,
                    group.Code,
                    "No submitted available reviewer slot satisfies supervisor, previous reviewer, and conflict rules."));
                continue;
            }

            var selectedSlotGroup = Shuffle(eligibleSlotGroups, random)[0];
            var reviewerIds = Shuffle(selectedSlotGroup.Select(slot => slot.LecturerId).Distinct().ToArray(), random)
                .Take(reviewersPerSession)
                .ToArray();
            rules.ValidateReviewAssignment(group.SupervisorId, reviewerIds, previousReviewerIds);

            var sessionDate = SessionDateFromWeekStart(weekStart, selectedSlotGroup.Key.DayOfWeek);
            var session = new ReviewSession
            {
                Code = NextSessionCode(semester.Code, request.ReviewType, group.Code, sessionDate, selectedSlotGroup.Key.Slot, usedSessionCodeSet),
                SemesterId = request.SemesterId,
                DayOfWeek = selectedSlotGroup.Key.DayOfWeek,
                Slot = selectedSlotGroup.Key.Slot,
                Room = $"{roomPrefix}-{selectedSlotGroup.Key.DayOfWeek}{selectedSlotGroup.Key.Slot}-{assignedSessions.Count + 1:D2}",
                Type = request.ReviewType,
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
                GroupPosition = assignedSessions.Count + 1,
                ConflictFlag = false
            });

            foreach (var reviewerId in reviewerIds)
            {
                dbContext.ReviewChecklistSubmissions.Add(new ReviewChecklistSubmission
                {
                    SessionId = session.Id,
                    GroupId = group.Id,
                    ReviewerId = reviewerId,
                    Type = request.ReviewType,
                    Status = ReviewSubmissionStatus.Draft,
                    LastSavedAt = DateTime.UtcNow
                });
                occupiedReviewerSlots.Add(OccupiedKey(reviewerId, sessionDate, selectedSlotGroup.Key.Slot));
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            assignedSessions.Add(new ReviewSchedulingSessionResponse(
                session.Id,
                session.Code,
                group.Id,
                group.Code,
                session.Type,
                session.Status,
                reviewerIds,
                session.SessionDate,
                session.DayOfWeek,
                session.Slot,
                session.Room));
        }

        await transaction.CommitAsync(cancellationToken);
        var emailSummary = await assignmentEmailNotifier.SendAssignedAsync(
            assignedSessions.Select(session => session.Id).ToArray(),
            cancellationToken);

        return Ok(new RandomAssignReviewSessionsResponse(
            groups.Count,
            assignedSessions.Count,
            emailSummary.SentEmailCount,
            emailSummary.FailedEmailCount,
            unassignedGroups,
            assignedSessions));
    }

    private async Task<Dictionary<int, HashSet<int>>> LoadPreviousReviewersByGroupAsync(
        int semesterId,
        ReviewType reviewType,
        IReadOnlyCollection<int> groupIds,
        CancellationToken cancellationToken)
    {
        var result = groupIds.ToDictionary(groupId => groupId, _ => new HashSet<int>());
        if (reviewType != ReviewType.Review2)
        {
            return result;
        }

        var previous = await dbContext.GroupReviewSlots
            .Where(slot => groupIds.Contains(slot.GroupId))
            .Join(
                dbContext.ReviewSessions.Where(session => session.SemesterId == semesterId && session.Type == ReviewType.Review1),
                slot => slot.SessionId,
                session => session.Id,
                (slot, session) => new
                {
                    slot.GroupId,
                    session.Reviewer1Id,
                    session.Reviewer2Id
                })
            .ToListAsync(cancellationToken);
        foreach (var item in previous)
        {
            if (item.Reviewer1Id.HasValue)
            {
                result[item.GroupId].Add(item.Reviewer1Id.Value);
            }

            if (item.Reviewer2Id.HasValue)
            {
                result[item.GroupId].Add(item.Reviewer2Id.Value);
            }
        }

        return result;
    }

    private async Task<HashSet<string>> LoadOccupiedReviewerSlotsAsync(
        int semesterId,
        DateTime weekStartDate,
        DateTime weekEndDate,
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.ReviewSessions
            .Where(session => session.SemesterId == semesterId &&
                              session.SessionDate >= weekStartDate &&
                              session.SessionDate < weekEndDate &&
                              session.Status != ReviewSessionStatus.Cancelled)
            .Select(session => new
            {
                session.SessionDate,
                session.Slot,
                session.Reviewer1Id,
                session.Reviewer2Id
            })
            .ToListAsync(cancellationToken);
        return sessions
            .SelectMany(session => new[] { session.Reviewer1Id, session.Reviewer2Id }
                .Where(reviewerId => reviewerId.HasValue)
                .Select(reviewerId => OccupiedKey(reviewerId!.Value, session.SessionDate, session.Slot)))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static DateOnly NormalizeWeekStart(DateOnly date)
    {
        var day = (int)date.DayOfWeek;
        var daysFromMonday = day == 0 ? 6 : day - 1;
        return date.AddDays(-daysFromMonday);
    }

    private static DateTime SessionDateFromWeekStart(DateOnly weekStart, int dayOfWeek) =>
        DateTime.SpecifyKind(weekStart.AddDays(dayOfWeek - 1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static string OccupiedKey(int lecturerId, DateTime sessionDate, int slot) =>
        $"{lecturerId}:{sessionDate.Date:yyyy-MM-dd}:{slot}";

    private static string NextSessionCode(
        string semesterCode,
        ReviewType reviewType,
        string groupCode,
        DateTime sessionDate,
        int slot,
        HashSet<string> usedCodes)
    {
        var baseCode = $"{reviewType}-{semesterCode}-{SanitizeCode(groupCode)}-{sessionDate:yyyyMMdd}-S{slot}";
        var code = baseCode;
        var index = 2;
        while (!usedCodes.Add(code))
        {
            code = $"{baseCode}-{index.ToString(CultureInfo.InvariantCulture)}";
            index++;
        }

        return code;
    }

    private static string SanitizeCode(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit));

    private static T[] Shuffle<T>(IEnumerable<T> source, Random random)
    {
        var items = source.ToArray();
        for (var index = items.Length - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }

        return items;
    }
}

public sealed record ReviewSchedulingBoardResponse(
    int SemesterId,
    ReviewType ReviewType,
    DateOnly WeekStart,
    IReadOnlyCollection<ReviewSchedulingLecturerResponse> Lecturers,
    IReadOnlyCollection<ReviewSchedulingAvailabilityResponse> Availability,
    IReadOnlyCollection<ReviewSchedulingAvailabilitySubmissionResponse> AvailabilitySubmissions,
    IReadOnlyCollection<ReviewSchedulingGroupResponse> Groups,
    IReadOnlyCollection<ReviewSchedulingSessionResponse> Sessions);

public sealed record ReviewSchedulingLecturerResponse(
    int Id,
    string Code,
    string FullName,
    string Department,
    string Email);

public sealed record ReviewSchedulingAvailabilityResponse(int LecturerId, int DayOfWeek, int Slot);

public sealed record ReviewSchedulingAvailabilitySubmissionResponse(
    int LecturerId,
    bool IsSubmitted,
    DateTime? SubmittedAt,
    int SlotCount);

public sealed record ReviewSchedulingGroupResponse(
    int Id,
    string Code,
    string ProjectName,
    int SupervisorId,
    string SupervisorCode,
    int StudentCount);

public sealed record ReviewSchedulingSessionResponse(
    int Id,
    string Code,
    int GroupId,
    string GroupCode,
    ReviewType Type,
    ReviewSessionStatus Status,
    IReadOnlyCollection<int> ReviewerIds,
    DateTime SessionDate,
    int DayOfWeek,
    int Slot,
    string Room);

public sealed record RandomAssignReviewSessionsRequest(
    int SemesterId,
    ReviewType ReviewType,
    DateOnly WeekStart,
    int ReviewersPerSession,
    string? RoomPrefix,
    int? Seed);

public sealed record RandomAssignReviewSessionsResponse(
    int TotalCandidateGroups,
    int AssignedCount,
    int SentEmailCount,
    int FailedEmailCount,
    IReadOnlyCollection<RandomAssignUnassignedGroupResponse> UnassignedGroups,
    IReadOnlyCollection<ReviewSchedulingSessionResponse> Sessions);

public sealed record RandomAssignUnassignedGroupResponse(
    int GroupId,
    string GroupCode,
    string Reason);

internal sealed record AvailabilityCandidate(int LecturerId, int DayOfWeek, int Slot);

internal sealed record RandomAssignGroupCandidate(int Id, string Code, int SupervisorId);

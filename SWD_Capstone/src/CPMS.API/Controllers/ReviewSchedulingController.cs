using System.Globalization;
using System.Data;
using System.Security.Claims;
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
[Authorize]
public sealed class ReviewSchedulingController(
    CpmsDbContext dbContext,
    AssignmentRules rules) : ControllerBase
{
    [HttpGet("board")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
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
        var round = await dbContext.ReviewRounds.SingleOrDefaultAsync(x =>
            x.SemesterId == semesterId && x.Type == reviewType && x.WeekStartDate == weekStart,
            cancellationToken);
        if (round is null)
        {
            return NotFound(new { error = "Review round not found." });
        }
        var weekStartDate = DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var weekEndDate = weekStartDate.AddDays(7);

        var lecturers = await dbContext.Lecturers
            .OrderBy(x => x.Code)
            .Select(x => new ReviewSchedulingLecturerResponse(
                x.Id,
                x.Code,
                x.FullName,
                x.Department,
                dbContext.Users.Where(user => user.Id == x.UserId).Select(user => user.Email).FirstOrDefault() ?? "unknown@fpt.edu.vn"))
            .ToListAsync(cancellationToken);

        var availability = await dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == round.Id &&
                        dbContext.ReviewAvailabilitySubmissions.Any(submission =>
                            submission.ReviewRoundId == x.ReviewRoundId &&
                            submission.LecturerId == x.LecturerId &&
                            submission.SubmittedAt != null))
            .OrderBy(x => x.LecturerId)
            .ThenBy(x => x.DayOfWeek)
            .ThenBy(x => x.Slot)
            .Select(x => new ReviewSchedulingAvailabilityResponse(x.LecturerId, x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);

        var availabilitySubmissions = await dbContext.ReviewAvailabilitySubmissions
            .Where(x => x.ReviewRoundId == round.Id)
            .OrderBy(x => x.LecturerId)
            .Select(x => new ReviewSchedulingAvailabilitySubmissionResponse(
                x.LecturerId,
                x.SubmittedAt != null,
                x.SubmittedAt,
                dbContext.ReviewAvailabilities.Count(slot =>
                    slot.ReviewRoundId == x.ReviewRoundId &&
                    slot.LecturerId == x.LecturerId)))
            .ToListAsync(cancellationToken);

        var groups = await dbContext.CapstoneGroups
            .Where(x => x.SemesterId == semesterId)
            .OrderBy(x => x.Code)
            .Select(x => new ReviewSchedulingGroupResponse(
                x.Id,
                x.Code,
                dbContext.Topics.Where(topic => topic.Id == x.TopicId).Select(topic => topic.NameEn).FirstOrDefault() ?? "Chưa đăng ký đề tài",
                x.LecturerId,
                dbContext.Lecturers.Where(lecturer => lecturer.Id == x.LecturerId).Select(lecturer => lecturer.Code).FirstOrDefault() ?? "Chưa có GVHD",
                dbContext.Students.Count(student => student.GroupId == x.Id)))
            .ToListAsync(cancellationToken);

        var dbSessions = await dbContext.GroupReviewSlots
            .Join(
                dbContext.ReviewSessions.Where(x => x.SemesterId == semesterId &&
                    x.Type == reviewType && x.SessionDate >= weekStartDate && x.SessionDate < weekEndDate),
                groupSlot => groupSlot.SessionId,
                session => session.Id,
                (groupSlot, session) => new
                {
                    session.Id,
                    session.Code,
                    groupSlot.GroupId,
                    GroupCode = dbContext.CapstoneGroups.Where(group => group.Id == groupSlot.GroupId).Select(group => group.Code).Single(),
                    session.Type,
                    session.Status,
                    session.Reviewer1Id,
                    session.Reviewer2Id,
                    session.SessionDate,
                    session.DayOfWeek,
                    session.Slot,
                    session.Room,
                    groupSlot.GroupPosition
                })
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.Slot)
            .ThenBy(x => x.GroupPosition)
            .ToListAsync(cancellationToken);

        var sessions = dbSessions.Select(x => {
            var reviewerIds = new List<int>();
            if (x.Reviewer1Id.HasValue) reviewerIds.Add(x.Reviewer1Id.Value);
            if (x.Reviewer2Id.HasValue) reviewerIds.Add(x.Reviewer2Id.Value);

            return new ReviewSchedulingSessionResponse(
                x.Id,
                x.Code,
                x.GroupId,
                x.GroupCode,
                x.Type,
                x.Status,
                reviewerIds,
                x.SessionDate,
                x.DayOfWeek,
                x.Slot,
                x.Room ?? ""
            );
        }).ToList();

        return Ok(new ReviewSchedulingBoardResponse(semesterId, reviewType, weekStart, lecturers, availability, availabilitySubmissions, groups, sessions));
    }

    [HttpPost("random-assign")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var assignmentLockKey = $"review-round:{request.SemesterId}:{request.ReviewType}:{weekStart:yyyy-MM-dd}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({assignmentLockKey}, 0))",
            cancellationToken);
        var round = await dbContext.ReviewRounds.SingleOrDefaultAsync(x =>
            x.SemesterId == request.SemesterId &&
            x.Type == request.ReviewType &&
            x.WeekStartDate == weekStart, cancellationToken);
        if (round is null)
        {
            return BadRequest(new { error = "The selected review round does not exist." });
        }

        if (round.Status != ReviewRoundStatus.Closed)
        {
            return Conflict(new { error = "The review round must be Closed before assignments can be generated." });
        }

        var weekStartDate = DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var weekEndDate = weekStartDate.AddDays(7);
        var submittedAvailability = await dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == round.Id &&
                        dbContext.ReviewAvailabilitySubmissions.Any(submission =>
                            submission.ReviewRoundId == x.ReviewRoundId &&
                            submission.LecturerId == x.LecturerId &&
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
                session.SessionDate >= weekStartDate &&
                session.SessionDate < weekEndDate &&
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
        var studentAvailability = (await dbContext.StudentSlotRegistrations
                .Where(x => x.ReviewRoundId == round.Id)
                .Select(x => new { x.GroupId, x.DayOfWeek, x.Slot })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.GroupId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(x => SlotKey(x.DayOfWeek, x.Slot)).ToHashSet(StringComparer.Ordinal));

        var existingPanelData = await dbContext.ReviewSessions
            .Where(x => x.SemesterId == request.SemesterId &&
                        x.Type == request.ReviewType &&
                        x.SessionDate >= weekStartDate &&
                        x.SessionDate < weekEndDate &&
                        x.Status == ReviewSessionStatus.Draft)
            .Select(x => new
            {
                Session = x,
                GroupCount = dbContext.GroupReviewSlots.Count(slot => slot.SessionId == x.Id)
            })
            .ToListAsync(cancellationToken);
        var panels = existingPanelData
            .Where(x => x.GroupCount < 3)
            .Select(x => new ReviewPanelCandidate(
                x.Session,
                new[] { x.Session.Reviewer1Id, x.Session.Reviewer2Id }
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .ToArray(),
                x.GroupCount))
            .ToList();

        var random = request.Seed.HasValue ? new Random(request.Seed.Value) : new Random();
        var assignedSessions = new List<ReviewSchedulingSessionResponse>();
        var unassignedGroups = new List<RandomAssignUnassignedGroupResponse>();
        var randomGroups = Shuffle(groups, random);
        var roomPrefix = string.IsNullOrWhiteSpace(request.RoomPrefix) ? "AUTO" : request.RoomPrefix.Trim();

        foreach (var group in randomGroups)
        {
            if (!studentAvailability.TryGetValue(group.Id, out var groupAvailability) || groupAvailability.Count != 5)
            {
                unassignedGroups.Add(new RandomAssignUnassignedGroupResponse(
                    group.Id,
                    group.Code,
                    "The group leader did not submit exactly five available slots."));
                continue;
            }

            var previousReviewerIds = previousReviewerIdsByGroup.TryGetValue(group.Id, out var previous)
                ? previous
                : [];
            var reusablePanels = panels
                .Where(panel => panel.GroupCount < 3 &&
                                panel.ReviewerIds.Length == reviewersPerSession &&
                                groupAvailability.Contains(SlotKey(panel.Session.DayOfWeek, panel.Session.Slot)) &&
                                !panel.ReviewerIds.Contains(group.SupervisorId) &&
                                !panel.ReviewerIds.Any(previousReviewerIds.Contains))
                .OrderByDescending(panel => panel.GroupCount)
                .ToArray();

            ReviewPanelCandidate? selectedPanel = reusablePanels.FirstOrDefault();
            if (selectedPanel is null)
            {
                var eligibleSlotGroups = submittedAvailability
                .Where(slot => slot.LecturerId != group.SupervisorId &&
                               groupAvailability.Contains(SlotKey(slot.DayOfWeek, slot.Slot)) &&
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
                        "No shared student/lecturer slot satisfies reviewer conflict rules."));
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
                    Room = $"{roomPrefix}-{selectedSlotGroup.Key.DayOfWeek}{selectedSlotGroup.Key.Slot}-{panels.Count + 1:D2}",
                    Type = request.ReviewType,
                    Reviewer1Id = reviewerIds.ElementAtOrDefault(0),
                    Reviewer2Id = reviewerIds.ElementAtOrDefault(1) == 0 ? null : reviewerIds.ElementAtOrDefault(1),
                    SessionDate = sessionDate,
                    Status = ReviewSessionStatus.Draft
                };

                dbContext.ReviewSessions.Add(session);
                await dbContext.SaveChangesAsync(cancellationToken);
                selectedPanel = new ReviewPanelCandidate(session, reviewerIds, 0);
                panels.Add(selectedPanel);
                foreach (var reviewerId in reviewerIds)
                {
                    occupiedReviewerSlots.Add(OccupiedKey(reviewerId, sessionDate, session.Slot));
                }
            }

            rules.EnsureReviewSessionCapacity(selectedPanel.GroupCount);
            dbContext.GroupReviewSlots.Add(new GroupReviewSlot
            {
                SessionId = selectedPanel.Session.Id,
                GroupId = group.Id,
                GroupPosition = selectedPanel.GroupCount + 1,
                ConflictFlag = false
            });

            foreach (var reviewerId in selectedPanel.ReviewerIds)
            {
                dbContext.ReviewChecklistSubmissions.Add(new ReviewChecklistSubmission
                {
                    SessionId = selectedPanel.Session.Id,
                    GroupId = group.Id,
                    ReviewerId = reviewerId,
                    Type = request.ReviewType,
                    Status = ReviewSubmissionStatus.Draft,
                    LastSavedAt = DateTime.UtcNow
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            selectedPanel.GroupCount++;

            assignedSessions.Add(new ReviewSchedulingSessionResponse(
                selectedPanel.Session.Id,
                selectedPanel.Session.Code,
                group.Id,
                group.Code,
                selectedPanel.Session.Type,
                selectedPanel.Session.Status,
                selectedPanel.ReviewerIds,
                selectedPanel.Session.SessionDate,
                selectedPanel.Session.DayOfWeek,
                selectedPanel.Session.Slot,
                selectedPanel.Session.Room));
        }

        await transaction.CommitAsync(cancellationToken);
        return Ok(new RandomAssignReviewSessionsResponse(
            groups.Count,
            assignedSessions.Count,
            0,
            0,
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

    private static string SlotKey(int dayOfWeek, int slot) => $"{dayOfWeek}:{slot}";

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

    [HttpGet("rounds")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<IReadOnlyList<ReviewRoundManagementResponse>> GetRounds(
        int semesterId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ReviewRounds
            .Where(x => x.SemesterId == semesterId)
            .OrderByDescending(x => x.WeekStartDate)
            .Select(x => new ReviewRoundManagementResponse(
                x.Id,
                x.SemesterId,
                x.Type,
                x.Status,
                x.WeekStartDate,
                x.WeekEndDate,
                x.CreatedAt,
                x.OpenedAt,
                x.ClosedAt,
                dbContext.StudentSlotRegistrations
                    .Where(r => r.ReviewRoundId == x.Id)
                    .Select(r => r.GroupId)
                    .Distinct()
                    .Count()))
            .ToListAsync(cancellationToken);
    }

    [HttpPost("rounds")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<ReviewRoundManagementResponse>> CreateRound(
        CreateReviewRoundRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var semester = await dbContext.Semesters.SingleOrDefaultAsync(x => x.Id == request.SemesterId, cancellationToken);
        if (semester is null)
        {
            return BadRequest(new { error = "Semester does not exist." });
        }

        var moderatorId = await CurrentModeratorIdAsync(cancellationToken);
        if (moderatorId is null)
        {
            return Forbid();
        }

        var weekStart = NormalizeWeekStart(request.WeekStartDate);
        var weekEnd = weekStart.AddDays(5); // Mon-Sat

        var exists = await dbContext.ReviewRounds.AnyAsync(x =>
            x.SemesterId == request.SemesterId &&
            x.Type == request.ReviewType &&
            x.WeekStartDate == weekStart, cancellationToken);
        if (exists)
        {
            return BadRequest(new { error = "A review round already exists for this semester, type, and week." });
        }

        var round = new ReviewRound
        {
            SemesterId = request.SemesterId,
            Type = request.ReviewType,
            WeekStartDate = weekStart,
            WeekEndDate = weekEnd,
            Status = ReviewRoundStatus.Draft,
            CreatedByModeratorId = moderatorId.Value
        };

        dbContext.ReviewRounds.Add(round);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ReviewRoundManagementResponse(
            round.Id, round.SemesterId, round.Type, round.Status,
            round.WeekStartDate, round.WeekEndDate,
            round.CreatedAt, round.OpenedAt, round.ClosedAt, 0));
    }

    [HttpPatch("rounds/{roundId:int}/status")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<ActionResult<ReviewRoundManagementResponse>> UpdateRoundStatus(
        int roundId,
        UpdateRoundStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var round = await dbContext.ReviewRounds.SingleOrDefaultAsync(x => x.Id == roundId, cancellationToken);
        if (round is null)
        {
            return NotFound(new { error = "Review round not found." });
        }

        try
        {
            rules.EnsureReviewRoundTransition(round.Status, request.Status);
        }
        catch (BusinessRuleException exception)
        {
            return Conflict(new { error = exception.Message });
        }

        round.Status = request.Status;
        if (request.Status == ReviewRoundStatus.Open && round.OpenedAt is null)
        {
            round.OpenedAt = DateTime.UtcNow;
        }

        if (request.Status == ReviewRoundStatus.Closed && round.ClosedAt is null)
        {
            var activeGroupIds = await dbContext.CapstoneGroups
                .Where(x => x.SemesterId == round.SemesterId && x.Status == GroupStatus.Active)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            var completedRegistrationCount = await dbContext.StudentSlotRegistrations
                .Where(x => x.ReviewRoundId == round.Id && activeGroupIds.Contains(x.GroupId))
                .GroupBy(x => x.GroupId)
                .CountAsync(x => x.Count() == AssignmentRules.RequiredStudentAvailabilityCount, cancellationToken);
            if (activeGroupIds.Count == 0 || completedRegistrationCount != activeGroupIds.Count)
            {
                return Conflict(new
                {
                    error = "Every active capstone group must submit exactly five slots before registration closes.",
                    activeGroups = activeGroupIds.Count,
                    completedRegistrations = completedRegistrationCount
                });
            }

            round.ClosedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var registrationCount = await dbContext.StudentSlotRegistrations
            .Where(r => r.ReviewRoundId == roundId)
            .Select(r => r.GroupId)
            .Distinct()
            .CountAsync(cancellationToken);

        return Ok(new ReviewRoundManagementResponse(
            round.Id, round.SemesterId, round.Type, round.Status,
            round.WeekStartDate, round.WeekEndDate,
            round.CreatedAt, round.OpenedAt, round.ClosedAt,
            registrationCount));
    }

    [HttpGet("student-registrations")]
    [Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
    public async Task<IReadOnlyList<StudentSlotRegistrationResponse>> GetStudentRegistrations(
        int roundId,
        CancellationToken cancellationToken)
    {
        return await dbContext.StudentSlotRegistrations
            .Where(x => x.ReviewRoundId == roundId)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.Slot)
            .Select(x => new StudentSlotRegistrationResponse(
                x.Id,
                x.GroupId,
                dbContext.CapstoneGroups.Where(g => g.Id == x.GroupId).Select(g => g.Code).Single(),
                x.DayOfWeek,
                x.Slot))
            .ToListAsync(cancellationToken);
    }

    private async Task<int?> CurrentModeratorIdAsync(CancellationToken cancellationToken)
    {
        var userId = int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0",
            CultureInfo.InvariantCulture);
        var moderatorId = await dbContext.TrainingDepartments
            .Where(x => x.UserId == userId)
            .Select(x => (int?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (moderatorId is null && User.IsInRole(nameof(UserRole.SystemAdministrator)))
        {
            moderatorId = await dbContext.TrainingDepartments
                .Select(x => (int?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        return moderatorId;
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

internal sealed class ReviewPanelCandidate(ReviewSession session, int[] reviewerIds, int groupCount)
{
    public ReviewSession Session { get; } = session;
    public int[] ReviewerIds { get; } = reviewerIds;
    public int GroupCount { get; set; } = groupCount;
}

public sealed record ReviewRoundManagementResponse(
    int Id,
    int SemesterId,
    ReviewType Type,
    ReviewRoundStatus Status,
    DateOnly WeekStartDate,
    DateOnly WeekEndDate,
    DateTime CreatedAt,
    DateTime? OpenedAt,
    DateTime? ClosedAt,
    int RegistrationCount);

public sealed record CreateReviewRoundRequest(
    int SemesterId,
    ReviewType ReviewType,
    DateOnly WeekStartDate);

public sealed record UpdateRoundStatusRequest(ReviewRoundStatus Status);

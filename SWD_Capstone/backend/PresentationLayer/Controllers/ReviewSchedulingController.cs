using CPMS.Core.Enums;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/review-scheduling")]
[Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
public sealed class ReviewSchedulingController(CpmsDbContext dbContext) : ControllerBase
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
            .Where(x => x.SemesterId == semesterId && x.WeekStartDate == weekStart)
            .OrderBy(x => x.LecturerId)
            .ThenBy(x => x.DayOfWeek)
            .ThenBy(x => x.Slot)
            .Select(x => new ReviewSchedulingAvailabilityResponse(x.LecturerId, x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);

        var groups = await dbContext.CapstoneGroups
            .Where(x => x.SemesterId == semesterId)
            .OrderBy(x => x.Code)
            .Select(x => new ReviewSchedulingGroupResponse(
                x.Id,
                x.Code,
                dbContext.Topics.Where(topic => topic.Id == x.TopicId).Select(topic => topic.NameEn).Single(),
                x.LecturerId,
                dbContext.Lecturers.Where(lecturer => lecturer.Id == x.LecturerId).Select(lecturer => lecturer.Code).Single()))
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

        return Ok(new ReviewSchedulingBoardResponse(semesterId, reviewType, weekStart, lecturers, availability, groups, sessions));
    }

    private static DateOnly NormalizeWeekStart(DateOnly date)
    {
        var day = (int)date.DayOfWeek;
        var daysFromMonday = day == 0 ? 6 : day - 1;
        return date.AddDays(-daysFromMonday);
    }
}

public sealed record ReviewSchedulingBoardResponse(
    int SemesterId,
    ReviewType ReviewType,
    DateOnly WeekStart,
    IReadOnlyCollection<ReviewSchedulingLecturerResponse> Lecturers,
    IReadOnlyCollection<ReviewSchedulingAvailabilityResponse> Availability,
    IReadOnlyCollection<ReviewSchedulingGroupResponse> Groups,
    IReadOnlyCollection<ReviewSchedulingSessionResponse> Sessions);

public sealed record ReviewSchedulingLecturerResponse(
    int Id,
    string Code,
    string FullName,
    string Department,
    string Email);

public sealed record ReviewSchedulingAvailabilityResponse(int LecturerId, int DayOfWeek, int Slot);

public sealed record ReviewSchedulingGroupResponse(
    int Id,
    string Code,
    string ProjectName,
    int SupervisorId,
    string SupervisorCode);

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

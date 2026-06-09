using System.Globalization;
using System.Security.Claims;
using CPMS.Api.Services;
using CPMS.Core.Entities;
using CPMS.Core.Exceptions;
using CPMS.Core.Services;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Controllers;

[ApiController]
[Route("api/review-availability")]
[Authorize]
public sealed class ReviewAvailabilityController(
    CpmsDbContext dbContext,
    AssignmentRules rules,
    SemesterResolverService semesterResolver) : ControllerBase
{
    [HttpGet("week")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewAvailabilityWeekResponse>> GetWeek(
        int semesterId,
        DateOnly weekStart,
        CancellationToken cancellationToken)
    {
        var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
        if (lecturerId is null)
        {
            return Forbid();
        }

        weekStart = NormalizeWeekStart(weekStart);
        var semester = await semesterResolver.ResolveForDateAsync(weekStart, cancellationToken);
        var slots = await dbContext.ReviewAvailabilities
            .Where(x => x.SemesterId == semester.Id && x.LecturerId == lecturerId.Value && x.WeekStartDate == weekStart)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.Slot)
            .Select(x => new ReviewAvailabilitySlotResponse(x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);

        return Ok(new ReviewAvailabilityWeekResponse(semester.Id, lecturerId.Value, weekStart, slots));
    }

    [HttpPut("week")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewAvailabilityWeekResponse>> SaveWeek(
        int semesterId,
        DateOnly weekStart,
        SaveReviewAvailabilityWeekRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
        if (lecturerId is null)
        {
            return Forbid();
        }

        weekStart = NormalizeWeekStart(weekStart);
        var semester = await semesterResolver.ResolveForDateAsync(weekStart, cancellationToken);
        var distinctSlots = request.Slots
            .Distinct()
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.Slot)
            .ToArray();

        foreach (var slot in distinctSlots)
        {
            if (slot.DayOfWeek is < 1 or > 7)
            {
                throw new BusinessRuleException("Review availability dayOfWeek must be between 1 and 7.");
            }

            rules.ValidateReviewSlot(slot.Slot);
        }

        var existing = await dbContext.ReviewAvailabilities
            .Where(x => x.SemesterId == semester.Id && x.LecturerId == lecturerId.Value && x.WeekStartDate == weekStart)
            .ToListAsync(cancellationToken);
        dbContext.ReviewAvailabilities.RemoveRange(existing);

        foreach (var slot in distinctSlots)
        {
            dbContext.ReviewAvailabilities.Add(new ReviewAvailability
            {
                SemesterId = semester.Id,
                LecturerId = lecturerId.Value,
                WeekStartDate = weekStart,
                DayOfWeek = slot.DayOfWeek,
                Slot = slot.Slot
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ReviewAvailabilityWeekResponse(
            semester.Id,
            lecturerId.Value,
            weekStart,
            distinctSlots.Select(x => new ReviewAvailabilitySlotResponse(x.DayOfWeek, x.Slot)).ToArray()));
    }

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

    private static DateOnly NormalizeWeekStart(DateOnly date)
    {
        var day = (int)date.DayOfWeek;
        var daysFromMonday = day == 0 ? 6 : day - 1;
        return date.AddDays(-daysFromMonday);
    }
}

public sealed record SaveReviewAvailabilityWeekRequest(IReadOnlyCollection<ReviewAvailabilitySlotRequest> Slots);
public sealed record ReviewAvailabilitySlotRequest(int DayOfWeek, int Slot);
public sealed record ReviewAvailabilitySlotResponse(int DayOfWeek, int Slot);
public sealed record ReviewAvailabilityWeekResponse(
    int SemesterId,
    int LecturerId,
    DateOnly WeekStart,
    IReadOnlyCollection<ReviewAvailabilitySlotResponse> Slots);

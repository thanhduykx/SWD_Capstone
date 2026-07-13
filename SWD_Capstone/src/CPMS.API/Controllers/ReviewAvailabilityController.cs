using System.Globalization;
using System.Security.Claims;
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
    AssignmentRules rules) : ControllerBase
{
    [HttpGet("week")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewAvailabilityWeekResponse>> GetWeek(
        int roundId,
        CancellationToken cancellationToken)
    {
        var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
        if (lecturerId is null)
        {
            return Forbid();
        }

        var round = await dbContext.ReviewRounds.SingleOrDefaultAsync(x => x.Id == roundId, cancellationToken);
        if (round is null)
        {
            return NotFound(new { error = "Review round not found." });
        }
        var weekStart = round.WeekStartDate;
        var slots = await dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == round.Id && x.LecturerId == lecturerId.Value)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.Slot)
            .Select(x => new ReviewAvailabilitySlotResponse(x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);
        var submission = await dbContext.ReviewAvailabilitySubmissions
            .Where(x => x.ReviewRoundId == round.Id && x.LecturerId == lecturerId.Value)
            .Select(x => new { x.SubmittedAt })
            .SingleOrDefaultAsync(cancellationToken);

        return Ok(new ReviewAvailabilityWeekResponse(
            round.Id,
            round.SemesterId,
            lecturerId.Value,
            weekStart,
            submission?.SubmittedAt is not null,
            submission?.SubmittedAt,
            slots));
    }

    [HttpPut("week")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewAvailabilityWeekResponse>> SaveWeek(
        int roundId,
        SaveReviewAvailabilityWeekRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Slots is null)
        {
            return BadRequest(new { error = "Slots is required." });
        }

        var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
        if (lecturerId is null)
        {
            return Forbid();
        }

        var round = await dbContext.ReviewRounds.SingleOrDefaultAsync(x => x.Id == roundId, cancellationToken);
        if (round is null)
        {
            return NotFound(new { error = "Review round not found." });
        }

        if (round.Status != CPMS.Core.Enums.ReviewRoundStatus.Open)
        {
            return Conflict(new { error = "Lecturer availability can only be changed while registration is Open." });
        }
        var weekStart = round.WeekStartDate;
        var distinctSlots = request.Slots
            .Distinct()
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.Slot)
            .ToArray();
        if (distinctSlots.Length > 30)
        {
            return BadRequest(new { error = "A review round contains at most 30 availability slots." });
        }

        foreach (var slot in distinctSlots)
        {
            if (slot.DayOfWeek is < 1 or > 6)
            {
                throw new BusinessRuleException("Review availability dayOfWeek must be between 1 and 6.");
            }

            rules.ValidateReviewSlot(slot.Slot);
        }

        var existing = await dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == round.Id && x.LecturerId == lecturerId.Value)
            .ToListAsync(cancellationToken);
        dbContext.ReviewAvailabilities.RemoveRange(existing);
        await UpsertSubmissionDraftAsync(round, lecturerId.Value, cancellationToken);

        foreach (var slot in distinctSlots)
        {
            dbContext.ReviewAvailabilities.Add(new ReviewAvailability
            {
                ReviewRoundId = round.Id,
                SemesterId = round.SemesterId,
                LecturerId = lecturerId.Value,
                WeekStartDate = weekStart,
                DayOfWeek = slot.DayOfWeek,
                Slot = slot.Slot
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ReviewAvailabilityWeekResponse(
            round.Id,
            round.SemesterId,
            lecturerId.Value,
            weekStart,
            false,
            null,
            distinctSlots.Select(x => new ReviewAvailabilitySlotResponse(x.DayOfWeek, x.Slot)).ToArray()));
    }

    [HttpPost("week/submit")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewAvailabilityWeekResponse>> SubmitWeek(
        int roundId,
        CancellationToken cancellationToken)
    {
        var lecturerId = await CurrentLecturerIdAsync(cancellationToken);
        if (lecturerId is null)
        {
            return Forbid();
        }

        var round = await dbContext.ReviewRounds.SingleOrDefaultAsync(x => x.Id == roundId, cancellationToken);
        if (round is null)
        {
            return NotFound(new { error = "Review round not found." });
        }

        if (round.Status != CPMS.Core.Enums.ReviewRoundStatus.Open)
        {
            return Conflict(new { error = "Lecturer availability can only be submitted while registration is Open." });
        }
        var weekStart = round.WeekStartDate;
        var slots = await dbContext.ReviewAvailabilities
            .Where(x => x.ReviewRoundId == round.Id && x.LecturerId == lecturerId.Value)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.Slot)
            .Select(x => new ReviewAvailabilitySlotResponse(x.DayOfWeek, x.Slot))
            .ToListAsync(cancellationToken);
        if (slots.Count == 0)
        {
            return BadRequest(new { error = "At least one availability slot is required before submitting to moderator." });
        }

        var submittedAt = DateTime.UtcNow;
        var submission = await dbContext.ReviewAvailabilitySubmissions
            .SingleOrDefaultAsync(x => x.ReviewRoundId == round.Id && x.LecturerId == lecturerId.Value, cancellationToken);
        if (submission is null)
        {
            dbContext.ReviewAvailabilitySubmissions.Add(new ReviewAvailabilitySubmission
            {
                ReviewRoundId = round.Id,
                SemesterId = round.SemesterId,
                LecturerId = lecturerId.Value,
                WeekStartDate = weekStart,
                UpdatedAt = submittedAt,
                SubmittedAt = submittedAt
            });
        }
        else
        {
            submission.UpdatedAt = submittedAt;
            submission.SubmittedAt = submittedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ReviewAvailabilityWeekResponse(
            round.Id,
            round.SemesterId,
            lecturerId.Value,
            weekStart,
            true,
            submittedAt,
            slots));
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

    private async Task UpsertSubmissionDraftAsync(
        ReviewRound round,
        int lecturerId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var submission = await dbContext.ReviewAvailabilitySubmissions
            .SingleOrDefaultAsync(x => x.ReviewRoundId == round.Id && x.LecturerId == lecturerId, cancellationToken);
        if (submission is null)
        {
            dbContext.ReviewAvailabilitySubmissions.Add(new ReviewAvailabilitySubmission
            {
                ReviewRoundId = round.Id,
                SemesterId = round.SemesterId,
                LecturerId = lecturerId,
                WeekStartDate = round.WeekStartDate,
                UpdatedAt = now
            });
            return;
        }

        submission.UpdatedAt = now;
        submission.SubmittedAt = null;
    }

}

public sealed record SaveReviewAvailabilityWeekRequest(IReadOnlyCollection<ReviewAvailabilitySlotRequest> Slots);
public sealed record ReviewAvailabilitySlotRequest(int DayOfWeek, int Slot);
public sealed record ReviewAvailabilitySlotResponse(int DayOfWeek, int Slot);
public sealed record ReviewAvailabilityWeekResponse(
    int RoundId,
    int SemesterId,
    int LecturerId,
    DateOnly WeekStart,
    bool IsSubmitted,
    DateTime? SubmittedAt,
    IReadOnlyCollection<ReviewAvailabilitySlotResponse> Slots);

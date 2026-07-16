using System.Globalization;
using System.Security.Claims;
using CPMS.API.Contracts.Reviews;
using CPMS.BLL.Reviews.Registration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/review-availability")]
[Authorize]
public sealed class ReviewAvailabilityController(IReviewRegistrationService registrationService) : ControllerBase
{
    [HttpGet("week")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewAvailabilityWeekResponse>> GetWeek(
        int roundId,
        CancellationToken cancellationToken)
    {
        ReviewAvailabilityWeekResult result = await registrationService.GetLecturerAvailabilityAsync(
            CurrentUserId(), roundId, cancellationToken);
        return Ok(Map(result));
    }

    [HttpPut("week")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewAvailabilityWeekResponse>> SaveWeek(
        int roundId,
        SaveReviewAvailabilityWeekRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ReviewAvailabilityWeekResult result = await registrationService.SaveLecturerAvailabilityAsync(
            CurrentUserId(),
            roundId,
            request.Slots?.Select(x => new ReviewAvailabilitySlot(x.DayOfWeek, x.Slot)).ToArray(),
            cancellationToken);
        return Ok(Map(result));
    }

    [HttpPost("week/submit")]
    [Authorize(Roles = "Lecturer")]
    public async Task<ActionResult<ReviewAvailabilityWeekResponse>> SubmitWeek(
        int roundId,
        CancellationToken cancellationToken)
    {
        ReviewAvailabilityWeekResult result = await registrationService.SubmitLecturerAvailabilityAsync(
            CurrentUserId(), roundId, cancellationToken);
        return Ok(Map(result));
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);

    private static ReviewAvailabilityWeekResponse Map(ReviewAvailabilityWeekResult result) =>
        new(
            result.RoundId,
            result.SemesterId,
            result.LecturerId,
            result.WeekStart,
            result.IsSubmitted,
            result.SubmittedAt,
            result.Slots.Select(x => new ReviewAvailabilitySlotResponse(x.DayOfWeek, x.Slot)).ToArray());
}

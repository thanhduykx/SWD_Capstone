using System.Globalization;
using System.Security.Claims;
using CPMS.API.Contracts.Reviews;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Reviews.Publication;
using CPMS.BLL.Enums;
using CPMS.BLL.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/review-schedules")]
[Authorize(Roles = "SystemAdministrator,TrainingDepartment")]
public sealed class ReviewSchedulesController(IReviewPublicationService publicationService) : ControllerBase
{
    [HttpPost("publish")]
    public async Task<ActionResult<ReviewSchedulePublishResponse>> Publish(
        PublishReviewScheduleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ReviewSchedulePublicationResult result = await publicationService.PublishAsync(
                new PublishReviewScheduleCommand(
                    CurrentUserId(),
                    User.IsInRole(nameof(UserRole.SystemAdministrator)),
                    request.SemesterId,
                    request.ReviewType,
                    request.WeekStart,
                    request.Subject,
                    request.Message,
                    HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers.UserAgent),
                cancellationToken);
            return Ok(new ReviewSchedulePublishResponse(
                result.PublicationId,
                result.PublishedSessionCount,
                result.SentEmailCount,
                result.FailedEmailCount));
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ConflictException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (IncompleteReviewScheduleException exception)
        {
            return Conflict(new
            {
                error = exception.Message,
                activeGroups = exception.ActiveGroups,
                fullyRegisteredGroups = exception.FullyRegisteredGroups,
                scheduledGroups = exception.ScheduledGroups
            });
        }
    }

    private int CurrentUserId() =>
        int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Missing user identifier."),
            CultureInfo.InvariantCulture);
}

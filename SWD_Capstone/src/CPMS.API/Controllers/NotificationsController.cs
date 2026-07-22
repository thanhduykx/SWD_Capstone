using System.Globalization;
using System.Security.Claims;
using CPMS.BLL.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CPMS.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationListResult>> GetLatest(
        [FromQuery] int take = 20, CancellationToken cancellationToken = default) =>
        Ok(await notificationService.GetLatestAsync(CurrentUserId(), take, cancellationToken));

    [HttpPatch("{notificationId:int}/read")]
    public async Task<IActionResult> MarkRead(
        int notificationId, CancellationToken cancellationToken)
    {
        bool found = await notificationService.MarkReadAsync(
            CurrentUserId(), notificationId, cancellationToken);
        return found ? NoContent() : NotFound();
    }

    [HttpPatch("read-all")]
    public async Task<ActionResult<object>> MarkAllRead(CancellationToken cancellationToken)
    {
        int updated = await notificationService.MarkAllReadAsync(CurrentUserId(), cancellationToken);
        return Ok(new { updated });
    }

    private int CurrentUserId() => int.Parse(
        User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Missing user identifier."),
        CultureInfo.InvariantCulture);
}

using System.Globalization;
using System.Security.Claims;
using CPMS.BLL.Enums;
using CPMS.BLL.Reviews.Assessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CPMS.API.Hubs;

[Authorize]
public sealed class ReviewProgressHub(IReviewAttendanceService attendanceService) : Hub
{
    public const string CommentAdded = "reviewProgressCommentAdded";

    public async Task JoinReviewProgress(int sessionId, int groupId)
    {
        // Reuse the review access rules before allowing a client into a realtime room.
        await attendanceService.GetCommentsAsync(
            sessionId, groupId, CurrentActor(), Context.ConnectionAborted);

        await Groups.AddToGroupAsync(
            Context.ConnectionId, GroupName(sessionId, groupId), Context.ConnectionAborted);
    }

    public Task LeaveReviewProgress(int sessionId, int groupId) =>
        Groups.RemoveFromGroupAsync(
            Context.ConnectionId, GroupName(sessionId, groupId), Context.ConnectionAborted);

    public static string GroupName(int sessionId, int groupId) =>
        $"review-progress:{sessionId}:{groupId}";

    private ReviewActor CurrentActor() => new(CurrentUserId(), CurrentRole());

    private UserRole CurrentRole() => Enum.GetValues<UserRole>()
        .FirstOrDefault(role => Context.User?.IsInRole(role.ToString()) == true);

    private int CurrentUserId() => int.Parse(
        Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new HubException("Missing user identifier."),
        CultureInfo.InvariantCulture);
}

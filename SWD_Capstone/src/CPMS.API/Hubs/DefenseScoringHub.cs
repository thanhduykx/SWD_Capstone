using System.Globalization;
using System.Security.Claims;
using CPMS.BLL.Defense.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CPMS.API.Hubs;

[Authorize(Roles = "Lecturer")]
public sealed class DefenseScoringHub(IDefenseSessionService defenseService) : Hub
{
    public const string SessionStarted = "defenseSessionStarted";
    public const string SessionClosed = "defenseSessionClosed";
    public const string CommentSubmitted = "defenseCommentSubmitted";
    public const string MemberJoined = "memberJoined";

    public async Task JoinDefenseSession(int sessionId)
    {
        DefenseJoinResult result;
        try
        {
            result = await defenseService.JoinAsync(sessionId, CurrentUserId(), Context.ConnectionAborted);
        }
        catch (Exception exception) when (exception is KeyNotFoundException or UnauthorizedAccessException)
        {
            throw new HubException(exception.Message);
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId), Context.ConnectionAborted);
        await Clients.Group(GroupName(sessionId)).SendAsync(MemberJoined, new
        {
            sessionId,
            lecturerId = result.LecturerId,
            Code = result.LecturerCode,
            FullName = result.LecturerName,
            joinedAt = DateTime.UtcNow
        }, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("defenseSessionState", result.Session, Context.ConnectionAborted);
    }

    public static string GroupName(int sessionId) => $"defense-session:{sessionId}";
    private int CurrentUserId() => int.Parse(
        Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new HubException("Missing user identifier."), CultureInfo.InvariantCulture);
}

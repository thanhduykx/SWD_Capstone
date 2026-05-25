using System.Globalization;
using System.Security.Claims;
using CPMS.Core.Services;
using CPMS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CPMS.Api.Hubs;

[Authorize(Roles = "Lecturer,EvaluationPanel")]
public sealed class DefenseScoringHub(CpmsDbContext dbContext, AssignmentRules rules) : Hub
{
    public const string SessionStarted = "defenseSessionStarted";
    public const string SessionClosed = "defenseSessionClosed";
    public const string ScoreSubmitted = "scoreSubmitted";
    public const string MemberJoined = "memberJoined";

    public async Task JoinDefenseSession(int sessionId)
    {
        var userId = CurrentUserId();
        var lecturer = await dbContext.Lecturers
            .Where(x => x.UserId == userId)
            .Select(x => new { x.Id, x.Code, x.FullName })
            .SingleOrDefaultAsync(Context.ConnectionAborted)
            ?? throw new HubException("Only lecturers assigned to a council can join defense scoring.");

        var session = await dbContext.DefenseSessions
            .Where(x => x.Id == sessionId)
            .Select(x => new
            {
                x.Id,
                x.CouncilId,
                x.StartedAt,
                x.EndedAt,
                x.IsLocked,
                ChairmanId = dbContext.Councils
                    .Where(c => c.Id == x.CouncilId)
                    .Select(c => c.ChairmanId)
                    .Single(),
                MemberIds = dbContext.CouncilMembers
                    .Where(m => m.CouncilId == x.CouncilId)
                    .Select(m => m.LecturerId)
                    .ToArray()
            })
            .SingleOrDefaultAsync(Context.ConnectionAborted)
            ?? throw new HubException("Defense session was not found.");

        rules.EnsureCouncilMember(lecturer.Id, session.MemberIds);

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId), Context.ConnectionAborted);
        await Clients.Group(GroupName(sessionId)).SendAsync(MemberJoined, new
        {
            sessionId,
            lecturerId = lecturer.Id,
            lecturer.Code,
            lecturer.FullName,
            joinedAt = DateTime.UtcNow
        }, Context.ConnectionAborted);

        await Clients.Caller.SendAsync("defenseSessionState", new
        {
            sessionId = session.Id,
            session.CouncilId,
            session.StartedAt,
            session.EndedAt,
            session.IsLocked,
            isChairman = lecturer.Id == session.ChairmanId
        }, Context.ConnectionAborted);
    }

    public static string GroupName(int sessionId) => $"defense-session:{sessionId}";

    private int CurrentUserId() =>
        int.Parse(
            Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new HubException("Missing user identifier."),
            CultureInfo.InvariantCulture);
}

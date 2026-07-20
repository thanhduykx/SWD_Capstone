using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CPMS.API.Hubs;

[Authorize]
public sealed class PresenceHub : Hub
{
    private static readonly ConcurrentDictionary<string, PresenceMember> Members = new();
    public const string Snapshot = "presenceSnapshot";
    public const string Changed = "presenceChanged";

    public override async Task OnConnectedAsync()
    {
        var member = new PresenceMember(
            Context.ConnectionId,
            Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
            Context.User?.FindFirstValue(ClaimTypes.Name) ?? Context.User?.Identity?.Name ?? "Người dùng",
            Context.User?.FindFirstValue(ClaimTypes.Role) ?? "User");
        Members[Context.ConnectionId] = member;
        await Clients.Caller.SendAsync(Snapshot, Members.Values, Context.ConnectionAborted);
        await Clients.Others.SendAsync(Changed, new { type = "joined", member }, Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Members.TryRemove(Context.ConnectionId, out var member))
            await Clients.Others.SendAsync(Changed, new { type = "left", member }, CancellationToken.None);
        await base.OnDisconnectedAsync(exception);
    }

    private sealed record PresenceMember(string ConnectionId, string UserId, string DisplayName, string Role);
}

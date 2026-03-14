using Microsoft.AspNetCore.SignalR;

namespace NetClaw.Dashboard.Hubs;

public sealed class DashboardHub : Hub
{
    public Task SubscribeToGroup(string groupFolder)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, $"group:{groupFolder}");
    }

    public Task UnsubscribeFromGroup(string groupFolder)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group:{groupFolder}");
    }
}

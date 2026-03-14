using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetClaw.Application.Execution;
using NetClaw.Application.Observability;
using NetClaw.Dashboard.Hubs;
using NetClaw.Dashboard.Models;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Entities;

namespace NetClaw.Dashboard.Services;

public sealed class DashboardBroadcastService : BackgroundService
{
    private readonly IHubContext<DashboardHub> hubContext;
    private readonly GroupExecutionQueue executionQueue;
    private readonly IAgentEventSink eventSink;
    private readonly IReadOnlyList<IChannel> channels;
    private readonly ILogger<DashboardBroadcastService> logger;

    public DashboardBroadcastService(
        IHubContext<DashboardHub> hubContext,
        GroupExecutionQueue executionQueue,
        IAgentEventSink eventSink,
        IReadOnlyList<IChannel> channels,
        ILogger<DashboardBroadcastService> logger)
    {
        this.hubContext = hubContext;
        this.executionQueue = executionQueue;
        this.eventSink = eventSink;
        this.channels = channels;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        eventSink.SetBroadcastCallback(BroadcastAgentEvent);

        using PeriodicTimer timer = new(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                QueueSnapshot snapshot = executionQueue.GetSnapshot();
                QueueStateDto queueState = MapQueueState(snapshot);

                IReadOnlyList<ChannelStatusDto> channelStatuses = channels
                    .Select(c => new ChannelStatusDto(c.GetType().Name.Replace("Channel", ""), c.IsConnected))
                    .ToArray();

                WorkerHeartbeatDto heartbeat = new(DateTimeOffset.UtcNow, queueState, channelStatuses);
                await hubContext.Clients.All.SendAsync("OnWorkerHeartbeat", heartbeat, stoppingToken);
                await hubContext.Clients.All.SendAsync("OnQueueStateChanged", queueState, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send periodic heartbeat to dashboard clients.");
            }
        }
    }

    private void BroadcastAgentEvent(AgentActivityEvent activityEvent)
    {
        AgentActivityEventDto dto = new(
            activityEvent.Id,
            activityEvent.GroupFolder,
            activityEvent.ChatJid,
            activityEvent.SessionId,
            activityEvent.EventKind.ToString(),
            activityEvent.Content,
            activityEvent.ToolName,
            activityEvent.Error,
            activityEvent.IsScheduledTask,
            activityEvent.TaskId,
            activityEvent.ObservedAt,
            activityEvent.CapturedAt);

        _ = BroadcastAgentEventCoreAsync(dto, activityEvent.GroupFolder);
    }

    private async Task BroadcastAgentEventCoreAsync(AgentActivityEventDto dto, string? groupFolder)
    {
        try
        {
            await hubContext.Clients.All.SendAsync("OnAgentEvent", dto);
            if (!string.IsNullOrWhiteSpace(groupFolder))
            {
                await hubContext.Clients.Group($"group:{groupFolder}").SendAsync("OnAgentEvent", dto);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast agent event to dashboard clients.");
        }
    }

    private static QueueStateDto MapQueueState(QueueSnapshot snapshot)
    {
        return new QueueStateDto(
            snapshot.ActiveExecutions,
            snapshot.MaxConcurrentExecutions,
            snapshot.WaitingGroupCount,
            snapshot.Groups.Select(g => new GroupQueueStateDto(
                g.ChatJid, g.Active, g.IsTaskExecution, g.PendingMessages,
                g.PendingTaskCount, g.IdleWaiting, g.RetryCount, g.RunningTaskIds)).ToArray());
    }
}

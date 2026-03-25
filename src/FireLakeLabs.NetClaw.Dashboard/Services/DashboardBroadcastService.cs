using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FireLakeLabs.NetClaw.Application.Execution;
using FireLakeLabs.NetClaw.Application.Observability;
using FireLakeLabs.NetClaw.Dashboard.Hubs;
using FireLakeLabs.NetClaw.Dashboard.Models;
using FireLakeLabs.NetClaw.Domain.Contracts.Channels;
using FireLakeLabs.NetClaw.Domain.Entities;

namespace FireLakeLabs.NetClaw.Dashboard.Services;

public sealed class DashboardBroadcastService : BackgroundService, IMessageNotifier
{
    private readonly IHubContext<DashboardHub> hubContext;
    private readonly GroupExecutionQueue executionQueue;
    private readonly IAgentEventSink eventSink;
    private readonly IReadOnlyList<IChannel> channels;
    private readonly ILogger<DashboardBroadcastService> logger;
    private readonly Channel<(AgentActivityEventDto Dto, string? GroupFolder)> eventQueue =
        Channel.CreateBounded<(AgentActivityEventDto, string?)>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly Channel<MessageDto> messageQueue =
        Channel.CreateBounded<MessageDto>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

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

    public void NotifyNewMessage(StoredMessage message)
    {
        MessageDto dto = new(
            message.Id,
            message.ChatJid.Value,
            message.Sender,
            message.SenderName,
            message.Content,
            message.Timestamp,
            message.IsFromMe,
            message.IsBotMessage);

        if (!messageQueue.Writer.TryWrite(dto))
        {
            logger.LogDebug("Message broadcast queue is closed; message notification dropped.");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        eventSink.SetBroadcastCallback(BroadcastAgentEvent);
        return Task.WhenAll(RunHeartbeatAsync(stoppingToken), RunEventQueueAsync(stoppingToken), RunMessageQueueAsync(stoppingToken));
    }

    private async Task RunHeartbeatAsync(CancellationToken stoppingToken)
    {
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

    private async Task RunEventQueueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach ((AgentActivityEventDto dto, string? groupFolder) in eventQueue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await hubContext.Clients.All.SendAsync("OnAgentEvent", dto, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to broadcast agent event to dashboard clients.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
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

        if (!eventQueue.Writer.TryWrite((dto, activityEvent.GroupFolder)))
        {
            logger.LogDebug("Agent event broadcast queue is closed; event dropped.");
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

    private async Task RunMessageQueueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (MessageDto dto in messageQueue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await hubContext.Clients.All.SendAsync("OnNewMessage", dto, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to broadcast new message to dashboard clients.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}

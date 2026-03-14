using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Observability;

public interface IAgentEventSink
{
    void Record(ContainerStreamEvent streamEvent, GroupFolder groupFolder, ChatJid chatJid, bool isScheduledTask, string? taskId);

    void SetBroadcastCallback(Action<AgentActivityEvent> callback);
}

public sealed class AgentEventSink : IAgentEventSink, IAsyncDisposable
{
    private readonly Channel<AgentActivityEvent> buffer;
    private readonly IAgentEventRepository repository;
    private readonly ILogger<AgentEventSink> logger;
    private readonly CancellationTokenSource disposalTokenSource;
    private readonly Task consumerTask;
    private Action<AgentActivityEvent>? broadcastCallback;
    private long nextBroadcastId;

    public AgentEventSink(IAgentEventRepository repository, ILogger<AgentEventSink> logger)
    {
        this.repository = repository;
        this.logger = logger;
        disposalTokenSource = new CancellationTokenSource();
        buffer = Channel.CreateBounded<AgentActivityEvent>(new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        consumerTask = Task.Run(() => ConsumeAsync(disposalTokenSource.Token));
    }

    public void Record(ContainerStreamEvent streamEvent, GroupFolder groupFolder, ChatJid chatJid, bool isScheduledTask, string? taskId)
    {
        AgentActivityEvent activityEvent = new(
            id: Interlocked.Increment(ref nextBroadcastId),
            groupFolder: groupFolder.Value,
            chatJid: chatJid.Value,
            sessionId: streamEvent.Output.NewSessionId?.Value,
            eventKind: streamEvent.Kind,
            content: streamEvent.Output.Result,
            toolName: null,
            error: streamEvent.Output.Error,
            isScheduledTask: isScheduledTask,
            taskId: taskId,
            observedAt: streamEvent.ObservedAt,
            capturedAt: DateTimeOffset.UtcNow);

        try
        {
            broadcastCallback?.Invoke(activityEvent);
        }
        catch
        {
            // Broadcast failures must not disrupt container stream event handling.
        }

        buffer.Writer.TryWrite(activityEvent);
    }

    public void SetBroadcastCallback(Action<AgentActivityEvent> callback)
    {
        broadcastCallback = callback;
    }

    public async ValueTask DisposeAsync()
    {
        buffer.Writer.TryComplete();
        await disposalTokenSource.CancelAsync();
        try
        {
            await consumerTask;
        }
        catch (OperationCanceledException)
        {
        }

        disposalTokenSource.Dispose();
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        List<AgentActivityEvent> batch = new(64);

        try
        {
            while (await buffer.Reader.WaitToReadAsync(cancellationToken))
            {
                batch.Clear();
                while (batch.Count < 64 && buffer.Reader.TryRead(out AgentActivityEvent? item))
                {
                    batch.Add(item);
                }

                if (batch.Count > 0)
                {
                    try
                    {
                        await repository.StoreBatchAsync(batch, cancellationToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogWarning(ex, "Failed to persist {Count} agent event(s); events dropped.", batch.Count);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}

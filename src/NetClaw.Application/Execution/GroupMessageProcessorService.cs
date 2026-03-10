using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Execution;

public sealed class GroupMessageProcessorService
{
    private readonly IAgentRuntime agentRuntime;
    private readonly string assistantName;
    private readonly IReadOnlyList<IChannel> channels;
    private readonly IGroupExecutionQueue groupExecutionQueue;
    private readonly IMessageFormatter messageFormatter;
    private readonly IMessageRepository messageRepository;
    private readonly IGroupRepository groupRepository;
    private readonly IOutboundRouter outboundRouter;
    private readonly IRouterStateRepository routerStateRepository;
    private readonly ISenderAuthorizationService senderAuthorizationService;
    private readonly string timezone;

    public GroupMessageProcessorService(
        IMessageRepository messageRepository,
        IGroupRepository groupRepository,
        IRouterStateRepository routerStateRepository,
        ISenderAuthorizationService senderAuthorizationService,
        IMessageFormatter messageFormatter,
        IOutboundRouter outboundRouter,
        IAgentRuntime agentRuntime,
        IGroupExecutionQueue groupExecutionQueue,
        IReadOnlyList<IChannel> channels,
        string assistantName,
        string timezone)
    {
        this.messageRepository = messageRepository;
        this.groupRepository = groupRepository;
        this.routerStateRepository = routerStateRepository;
        this.senderAuthorizationService = senderAuthorizationService;
        this.messageFormatter = messageFormatter;
        this.outboundRouter = outboundRouter;
        this.agentRuntime = agentRuntime;
        this.groupExecutionQueue = groupExecutionQueue;
        this.channels = channels;
        this.assistantName = assistantName;
        this.timezone = timezone;
    }

    public async Task<bool> ProcessAsync(ChatJid groupJid, CancellationToken cancellationToken = default)
    {
        try
        {
            RegisteredGroup? group = await groupRepository.GetByJidAsync(groupJid, cancellationToken);
            if (group is null)
            {
                return true;
            }

            DateTimeOffset? lastAgentTimestamp = await GetLastAgentTimestampAsync(groupJid, cancellationToken);
            IReadOnlyList<StoredMessage> pendingMessages = senderAuthorizationService.ApplyInboundPolicy(
                groupJid,
                await messageRepository.GetMessagesSinceAsync(groupJid, lastAgentTimestamp, assistantName, cancellationToken));
            if (pendingMessages.Count == 0)
            {
                return true;
            }

            if (!group.IsMain && group.RequiresTrigger && !pendingMessages.Any(message => senderAuthorizationService.CanTrigger(groupJid, message) && ContainsTrigger(message.Content, group.Trigger)))
            {
                return true;
            }

            string prompt = messageFormatter.FormatInbound(pendingMessages, timezone);
            bool streamedCompletedMessage = false;
            ContainerExecutionResult executionResult = await agentRuntime.ExecuteAsync(
                new ContainerInput(prompt, null, group.Folder, groupJid, group.IsMain, false, assistantName),
                async (streamEvent, ct) =>
                {
                    switch (streamEvent.Kind)
                    {
                        case ContainerEventKind.MessageCompleted:
                        {
                            string text = messageFormatter.NormalizeOutbound(streamEvent.Output.Result ?? string.Empty);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                await outboundRouter.RouteAsync(channels, groupJid, text, ct);
                                streamedCompletedMessage = true;
                            }

                            break;
                        }
                        case ContainerEventKind.Idle:
                            groupExecutionQueue.NotifyIdle(groupJid);
                            break;
                    }
                },
                cancellationToken: cancellationToken);

            if (executionResult.Status == ContainerRunStatus.Error)
            {
                return false;
            }

            string outboundText = messageFormatter.NormalizeOutbound(executionResult.Result ?? string.Empty);
            if (!streamedCompletedMessage && !string.IsNullOrWhiteSpace(outboundText))
            {
                await outboundRouter.RouteAsync(channels, groupJid, outboundText, cancellationToken);
            }

            await routerStateRepository.SetAsync(
                new RouterStateEntry(GetLastAgentTimestampKey(groupJid), pendingMessages[^1].Timestamp.ToString("O")),
                cancellationToken);

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private async Task<DateTimeOffset?> GetLastAgentTimestampAsync(ChatJid groupJid, CancellationToken cancellationToken)
    {
        RouterStateEntry? state = await routerStateRepository.GetAsync(GetLastAgentTimestampKey(groupJid), cancellationToken);
        return state is not null && DateTimeOffset.TryParse(state.Value, out DateTimeOffset parsed)
            ? parsed
            : null;
    }

    private static string GetLastAgentTimestampKey(ChatJid groupJid)
    {
        return $"last_agent_timestamp:{groupJid.Value}";
    }

    private static bool ContainsTrigger(string content, string trigger)
    {
        return content.Contains(trigger, StringComparison.OrdinalIgnoreCase);
    }
}
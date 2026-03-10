using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;

namespace NetClaw.Application.Execution;

public sealed class InboundMessagePollingService
{
    private const string LastTimestampKey = "last_timestamp";

    private readonly IGroupRepository groupRepository;
    private readonly IGroupExecutionQueue groupExecutionQueue;
    private readonly IMessageFormatter messageFormatter;
    private readonly IMessageRepository messageRepository;
    private readonly IRouterStateRepository routerStateRepository;
    private readonly string assistantName;
    private readonly ISenderAuthorizationService senderAuthorizationService;
    private readonly string timezone;

    public InboundMessagePollingService(
        IMessageRepository messageRepository,
        IGroupRepository groupRepository,
        IRouterStateRepository routerStateRepository,
        ISenderAuthorizationService senderAuthorizationService,
        IMessageFormatter messageFormatter,
        string assistantName,
        string timezone,
        IGroupExecutionQueue groupExecutionQueue)
    {
        this.messageRepository = messageRepository;
        this.groupRepository = groupRepository;
        this.routerStateRepository = routerStateRepository;
        this.senderAuthorizationService = senderAuthorizationService;
        this.messageFormatter = messageFormatter;
        this.assistantName = assistantName;
        this.timezone = timezone;
        this.groupExecutionQueue = groupExecutionQueue;
    }

    public async Task PollOnceAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset since = await GetLastTimestampAsync(cancellationToken);
        IReadOnlyList<StoredMessage> newMessages = await messageRepository.GetNewMessagesAsync(since, cancellationToken);
        if (newMessages.Count == 0)
        {
            return;
        }

        await routerStateRepository.SetAsync(new RouterStateEntry(LastTimestampKey, newMessages[^1].Timestamp.ToString("O")), cancellationToken);

        IReadOnlyDictionary<NetClaw.Domain.ValueObjects.ChatJid, NetClaw.Domain.Entities.RegisteredGroup> groups = await groupRepository.GetAllAsync(cancellationToken);
        foreach (IGrouping<NetClaw.Domain.ValueObjects.ChatJid, StoredMessage> messageGroup in newMessages
                     .Where(message => !message.IsBotMessage)
                     .GroupBy(message => message.ChatJid))
        {
            if (!groups.TryGetValue(messageGroup.Key, out NetClaw.Domain.Entities.RegisteredGroup? group))
            {
                continue;
            }

            IReadOnlyList<StoredMessage> allowedMessages = senderAuthorizationService.ApplyInboundPolicy(messageGroup.Key, messageGroup.ToArray());
            if (allowedMessages.Count == 0)
            {
                continue;
            }

            if (group.IsMain || !group.RequiresTrigger || allowedMessages.Any(message => senderAuthorizationService.CanTrigger(messageGroup.Key, message) && ContainsTrigger(message.Content, group.Trigger)))
            {
                DateTimeOffset? lastAgentTimestamp = await GetLastAgentTimestampAsync(messageGroup.Key, cancellationToken);
                IReadOnlyList<StoredMessage> messagesToSend = senderAuthorizationService.ApplyInboundPolicy(
                    messageGroup.Key,
                    await messageRepository.GetMessagesSinceAsync(messageGroup.Key, lastAgentTimestamp, assistantName, cancellationToken));

                if (messagesToSend.Count == 0)
                {
                    continue;
                }

                string formatted = messageFormatter.FormatInbound(messagesToSend, timezone);
                if (groupExecutionQueue.SendMessage(messageGroup.Key, formatted))
                {
                    await SetLastAgentTimestampAsync(messageGroup.Key, messagesToSend[^1].Timestamp, cancellationToken);
                    continue;
                }

                groupExecutionQueue.EnqueueMessageCheck(messageGroup.Key);
            }
        }
    }

    private async Task<DateTimeOffset?> GetLastAgentTimestampAsync(NetClaw.Domain.ValueObjects.ChatJid chatJid, CancellationToken cancellationToken)
    {
        RouterStateEntry? state = await routerStateRepository.GetAsync(GetLastAgentTimestampKey(chatJid), cancellationToken);
        return state is not null && DateTimeOffset.TryParse(state.Value, out DateTimeOffset parsed)
            ? parsed
            : null;
    }

    private Task SetLastAgentTimestampAsync(NetClaw.Domain.ValueObjects.ChatJid chatJid, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        return routerStateRepository.SetAsync(new RouterStateEntry(GetLastAgentTimestampKey(chatJid), timestamp.ToString("O")), cancellationToken);
    }

    private async Task<DateTimeOffset> GetLastTimestampAsync(CancellationToken cancellationToken)
    {
        RouterStateEntry? state = await routerStateRepository.GetAsync(LastTimestampKey, cancellationToken);
        return state is not null && DateTimeOffset.TryParse(state.Value, out DateTimeOffset parsed)
            ? parsed
            : DateTimeOffset.MinValue;
    }

    private static bool ContainsTrigger(string content, string trigger)
    {
        return content.Contains(trigger, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLastAgentTimestampKey(NetClaw.Domain.ValueObjects.ChatJid chatJid)
    {
        return $"last_agent_timestamp:{chatJid.Value}";
    }
}
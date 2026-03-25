using FireLakeLabs.NetClaw.Domain.Contracts.Channels;
using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Contracts.Services;
using FireLakeLabs.NetClaw.Domain.Entities;

namespace FireLakeLabs.NetClaw.Application.Execution;

public sealed class InboundMessagePollingService
{
    private const string LastTimestampKey = "last_timestamp";

    private readonly IGroupRepository groupRepository;
    private readonly IGroupExecutionQueue groupExecutionQueue;
    private readonly IMessageFormatter messageFormatter;
    private readonly IMessageRepository messageRepository;
    private readonly IRouterStateRepository routerStateRepository;
    private readonly IReadOnlyList<IChannel> channels;
    private readonly string assistantName;
    private readonly ISenderAuthorizationService senderAuthorizationService;
    private readonly string timezone;

    public InboundMessagePollingService(
        IMessageRepository messageRepository,
        IGroupRepository groupRepository,
        IRouterStateRepository routerStateRepository,
        IReadOnlyList<IChannel> channels,
        ISenderAuthorizationService senderAuthorizationService,
        IMessageFormatter messageFormatter,
        string assistantName,
        string timezone,
        IGroupExecutionQueue groupExecutionQueue)
    {
        this.messageRepository = messageRepository;
        this.groupRepository = groupRepository;
        this.routerStateRepository = routerStateRepository;
        this.channels = channels;
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

        IReadOnlyDictionary<FireLakeLabs.NetClaw.Domain.ValueObjects.ChatJid, FireLakeLabs.NetClaw.Domain.Entities.RegisteredGroup> groups = await groupRepository.GetAllAsync(cancellationToken);
        foreach (IGrouping<FireLakeLabs.NetClaw.Domain.ValueObjects.ChatJid, StoredMessage> messageGroup in newMessages
                     .Where(message => !message.IsBotMessage)
                     .GroupBy(message => message.ChatJid))
        {
            if (!groups.TryGetValue(messageGroup.Key, out FireLakeLabs.NetClaw.Domain.Entities.RegisteredGroup? group))
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
                    await SetTypingAsync(messageGroup.Key, isTyping: true, cancellationToken);
                    await SetLastAgentTimestampAsync(messageGroup.Key, messagesToSend[^1].Timestamp, cancellationToken);
                    continue;
                }

                groupExecutionQueue.EnqueueMessageCheck(messageGroup.Key);
            }
        }
    }

    private async Task<DateTimeOffset?> GetLastAgentTimestampAsync(FireLakeLabs.NetClaw.Domain.ValueObjects.ChatJid chatJid, CancellationToken cancellationToken)
    {
        RouterStateEntry? state = await routerStateRepository.GetAsync(GetLastAgentTimestampKey(chatJid), cancellationToken);
        return state is not null && DateTimeOffset.TryParse(state.Value, out DateTimeOffset parsed)
            ? parsed
            : null;
    }

    private Task SetLastAgentTimestampAsync(FireLakeLabs.NetClaw.Domain.ValueObjects.ChatJid chatJid, DateTimeOffset timestamp, CancellationToken cancellationToken)
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

    private static string GetLastAgentTimestampKey(FireLakeLabs.NetClaw.Domain.ValueObjects.ChatJid chatJid)
    {
        return $"last_agent_timestamp:{chatJid.Value}";
    }

    private async Task SetTypingAsync(FireLakeLabs.NetClaw.Domain.ValueObjects.ChatJid chatJid, bool isTyping, CancellationToken cancellationToken)
    {
        IChannel? channel = channels.FirstOrDefault(candidate => candidate.IsConnected && candidate.Owns(chatJid));
        if (channel is null)
        {
            return;
        }

        try
        {
            await channel.SetTypingAsync(chatJid, isTyping, cancellationToken);
        }
        catch
        {
        }
    }
}

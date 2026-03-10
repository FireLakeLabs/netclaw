using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;

namespace NetClaw.Application.Execution;

public sealed class InboundMessagePollingService
{
    private const string LastTimestampKey = "last_timestamp";

    private readonly IGroupRepository groupRepository;
    private readonly IGroupExecutionQueue groupExecutionQueue;
    private readonly IMessageRepository messageRepository;
    private readonly IRouterStateRepository routerStateRepository;
    private readonly ISenderAuthorizationService senderAuthorizationService;

    public InboundMessagePollingService(
        IMessageRepository messageRepository,
        IGroupRepository groupRepository,
        IRouterStateRepository routerStateRepository,
        ISenderAuthorizationService senderAuthorizationService,
        IGroupExecutionQueue groupExecutionQueue)
    {
        this.messageRepository = messageRepository;
        this.groupRepository = groupRepository;
        this.routerStateRepository = routerStateRepository;
        this.senderAuthorizationService = senderAuthorizationService;
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
                groupExecutionQueue.EnqueueMessageCheck(messageGroup.Key);
            }
        }
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
}
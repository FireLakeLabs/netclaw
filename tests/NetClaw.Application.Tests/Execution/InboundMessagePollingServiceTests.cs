using NetClaw.Application.Execution;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Tests.Execution;

public sealed class InboundMessagePollingServiceTests
{
    [Fact]
    public async Task PollOnceAsync_EnqueuesTriggeredRegisteredGroupAndUpdatesCursor()
    {
        ChatJid teamJid = new("team@jid");
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        InMemoryRouterStateRepository routerStateRepository = new();
        RecordingGroupExecutionQueue queue = new();
        InboundMessagePollingService service = new(
            new InMemoryMessageRepository([
                new StoredMessage("message-1", teamJid, "sender-1", "User", "@Andy hello", timestamp)
            ]),
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
            {
                [teamJid] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", timestamp)
            }),
            routerStateRepository,
            queue);

        await service.PollOnceAsync();

        Assert.Single(queue.EnqueuedGroups);
        Assert.Equal(teamJid, queue.EnqueuedGroups[0]);
        Assert.Equal(timestamp.ToString("O"), routerStateRepository.Entries["last_timestamp"].Value);
    }

    [Fact]
    public async Task PollOnceAsync_SkipsNonTriggeredNonMainMessages()
    {
        ChatJid teamJid = new("team@jid");
        RecordingGroupExecutionQueue queue = new();
        InboundMessagePollingService service = new(
            new InMemoryMessageRepository([
                new StoredMessage("message-1", teamJid, "sender-1", "User", "plain hello", DateTimeOffset.UtcNow)
            ]),
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
            {
                [teamJid] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow)
            }),
            new InMemoryRouterStateRepository(),
            queue);

        await service.PollOnceAsync();

        Assert.Empty(queue.EnqueuedGroups);
    }

    private sealed class InMemoryMessageRepository : IMessageRepository
    {
        private readonly IReadOnlyList<StoredMessage> messages;

        public InMemoryMessageRepository(IReadOnlyList<StoredMessage> messages)
        {
            this.messages = messages;
        }

        public Task<IReadOnlyList<ChatInfo>> GetAllChatsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChatInfo>>([]);

        public Task<IReadOnlyList<StoredMessage>> GetMessagesSinceAsync(ChatJid chatJid, DateTimeOffset? since, string assistantName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredMessage>>(messages.Where(message => message.ChatJid == chatJid).ToList());

        public Task<IReadOnlyList<StoredMessage>> GetNewMessagesAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredMessage>>(messages.Where(message => message.Timestamp > since).ToList());

        public Task StoreChatMetadataAsync(ChatInfo chatInfo, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StoreMessageAsync(StoredMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryGroupRepository : IGroupRepository
    {
        private readonly IReadOnlyDictionary<ChatJid, RegisteredGroup> groups;

        public InMemoryGroupRepository(IReadOnlyDictionary<ChatJid, RegisteredGroup> groups)
        {
            this.groups = groups;
        }

        public Task<IReadOnlyDictionary<ChatJid, RegisteredGroup>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(groups);

        public Task<RegisteredGroup?> GetByJidAsync(ChatJid chatJid, CancellationToken cancellationToken = default)
            => Task.FromResult(groups.TryGetValue(chatJid, out RegisteredGroup? group) ? group : null);

        public Task UpsertAsync(ChatJid chatJid, RegisteredGroup group, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryRouterStateRepository : IRouterStateRepository
    {
        public Dictionary<string, RouterStateEntry> Entries { get; } = [];

        public Task<RouterStateEntry?> GetAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.TryGetValue(key, out RouterStateEntry? entry) ? entry : null);

        public Task SetAsync(RouterStateEntry entry, CancellationToken cancellationToken = default)
        {
            Entries[entry.Key] = entry;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingGroupExecutionQueue : IGroupExecutionQueue
    {
        public List<ChatJid> EnqueuedGroups { get; } = [];

        public void CloseInput(ChatJid groupJid)
        {
        }

        public void EnqueueMessageCheck(ChatJid groupJid)
        {
            EnqueuedGroups.Add(groupJid);
        }

        public void EnqueueTask(ChatJid groupJid, TaskId taskId, Func<CancellationToken, Task> workItem)
        {
        }

        public void NotifyIdle(ChatJid groupJid)
        {
        }

        public bool SendMessage(ChatJid groupJid, string text)
        {
            return false;
        }
    }
}
using NetClaw.Application.Execution;
using NetClaw.Domain.Contracts.Channels;
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
            [],
            new PassThroughSenderAuthorizationService(),
            new FakeMessageFormatter(),
            "Andy",
            "UTC",
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
            [],
            new PassThroughSenderAuthorizationService(),
            new FakeMessageFormatter(),
            "Andy",
            "UTC",
            queue);

        await service.PollOnceAsync();

        Assert.Empty(queue.EnqueuedGroups);
    }

    [Fact]
    public async Task PollOnceAsync_DropModeBlocksDeniedSenders()
    {
        ChatJid teamJid = new("team@jid");
        RecordingGroupExecutionQueue queue = new();
        InboundMessagePollingService service = new(
            new InMemoryMessageRepository([
                new StoredMessage("message-1", teamJid, "blocked", "User", "@Andy hello", DateTimeOffset.UtcNow)
            ]),
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
            {
                [teamJid] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow)
            }),
            new InMemoryRouterStateRepository(),
            [],
            new FilteringSenderAuthorizationService(["allowed"]),
            new FakeMessageFormatter(),
            "Andy",
            "UTC",
            queue);

        await service.PollOnceAsync();

        Assert.Empty(queue.EnqueuedGroups);
    }

    [Fact]
    public async Task PollOnceAsync_SendsFollowUpToActiveSessionAndAdvancesAgentCursor()
    {
        ChatJid teamJid = new("team@jid");
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        InMemoryRouterStateRepository routerStateRepository = new();
        RecordingChannel channel = new(teamJid);
        RecordingGroupExecutionQueue queue = new()
        {
            SendMessageResult = true
        };
        InboundMessagePollingService service = new(
            new InMemoryMessageRepository([
                new StoredMessage("message-1", teamJid, "sender-1", "User", "@Andy hello", timestamp)
            ]),
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
            {
                [teamJid] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", timestamp)
            }),
            routerStateRepository,
            [channel],
            new PassThroughSenderAuthorizationService(),
            new FakeMessageFormatter(),
            "Andy",
            "UTC",
            queue);

        await service.PollOnceAsync();

        Assert.Empty(queue.EnqueuedGroups);
        Assert.Equal("formatted:@Andy hello", Assert.Single(queue.SentMessages).Text);
        Assert.Equal([(teamJid, true)], channel.TypingCalls);
        Assert.Equal(timestamp.ToString("O"), routerStateRepository.Entries["last_agent_timestamp:team@jid"].Value);
    }

    [Fact]
    public async Task PollOnceAsync_DoesNotFailWhenTypingIndicatorThrows()
    {
        ChatJid teamJid = new("team@jid");
        RecordingGroupExecutionQueue queue = new()
        {
            SendMessageResult = true
        };
        InboundMessagePollingService service = new(
            new InMemoryMessageRepository([
                new StoredMessage("message-1", teamJid, "sender-1", "User", "@Andy hello", DateTimeOffset.UtcNow)
            ]),
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
            {
                [teamJid] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow)
            }),
            new InMemoryRouterStateRepository(),
            [new ThrowingChannel(teamJid)],
            new PassThroughSenderAuthorizationService(),
            new FakeMessageFormatter(),
            "Andy",
            "UTC",
            queue);

        await service.PollOnceAsync();

        Assert.Single(queue.SentMessages);
    }

    private sealed class FakeMessageFormatter : IMessageFormatter
    {
        public string FormatInbound(IReadOnlyList<StoredMessage> messages, string timezone)
            => $"formatted:{string.Join('|', messages.Select(message => message.Content))}";

        public string NormalizeOutbound(string rawText)
            => rawText;
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

        public Task<IReadOnlyList<StoredMessage>> GetChatHistoryAsync(ChatJid chatJid, int limit, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredMessage>>([]);

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

        public Task<IReadOnlyList<RouterStateEntry>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RouterStateEntry>>(Entries.Values.ToList());

        public Task SetAsync(RouterStateEntry entry, CancellationToken cancellationToken = default)
        {
            Entries[entry.Key] = entry;
            return Task.CompletedTask;
        }
    }

    private sealed class PassThroughSenderAuthorizationService : ISenderAuthorizationService
    {
        public IReadOnlyList<StoredMessage> ApplyInboundPolicy(ChatJid chatJid, IReadOnlyList<StoredMessage> messages) => messages;

        public bool CanTrigger(ChatJid chatJid, StoredMessage message) => true;
    }

    private sealed class FilteringSenderAuthorizationService : ISenderAuthorizationService
    {
        private readonly HashSet<string> allowedSenders;

        public FilteringSenderAuthorizationService(IEnumerable<string> allowedSenders)
        {
            this.allowedSenders = allowedSenders.ToHashSet(StringComparer.Ordinal);
        }

        public IReadOnlyList<StoredMessage> ApplyInboundPolicy(ChatJid chatJid, IReadOnlyList<StoredMessage> messages)
            => messages.Where(message => message.IsFromMe || allowedSenders.Contains(message.Sender)).ToArray();

        public bool CanTrigger(ChatJid chatJid, StoredMessage message)
            => message.IsFromMe || allowedSenders.Contains(message.Sender);
    }

    private sealed class RecordingGroupExecutionQueue : IGroupExecutionQueue
    {
        public List<ChatJid> EnqueuedGroups { get; } = [];

        public List<(ChatJid ChatJid, string Text)> SentMessages { get; } = [];

        public bool SendMessageResult { get; init; }

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
            SentMessages.Add((groupJid, text));
            return SendMessageResult;
        }
    }

    private sealed class RecordingChannel : IChannel
    {
        private readonly ChatJid ownedJid;

        public RecordingChannel(ChatJid ownedJid)
        {
            this.ownedJid = ownedJid;
        }

        public List<(ChatJid ChatJid, bool IsTyping)> TypingCalls { get; } = [];

        public ChannelName Name => new("recording");

        public bool IsConnected => true;

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendMessageAsync(ChatJid chatJid, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool Owns(ChatJid chatJid) => chatJid == ownedJid;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetTypingAsync(ChatJid chatJid, bool isTyping, CancellationToken cancellationToken = default)
        {
            TypingCalls.Add((chatJid, isTyping));
            return Task.CompletedTask;
        }

        public Task SyncGroupsAsync(bool force, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ThrowingChannel : IChannel
    {
        private readonly ChatJid ownedJid;

        public ThrowingChannel(ChatJid ownedJid)
        {
            this.ownedJid = ownedJid;
        }

        public ChannelName Name => new("throwing");

        public bool IsConnected => true;

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendMessageAsync(ChatJid chatJid, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool Owns(ChatJid chatJid) => chatJid == ownedJid;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetTypingAsync(ChatJid chatJid, bool isTyping, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("typing failed");
        }

        public Task SyncGroupsAsync(bool force, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

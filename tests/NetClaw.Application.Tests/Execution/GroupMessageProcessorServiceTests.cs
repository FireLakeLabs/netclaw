using NetClaw.Application.Execution;
using NetClaw.Application.Observability;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Tests.Execution;

public sealed class GroupMessageProcessorServiceTests
{
    [Fact]
    public async Task ProcessAsync_ExecutesRuntimeRoutesReplyAndAdvancesCursor()
    {
        ChatJid groupJid = new("team@jid");
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        InMemoryRouterStateRepository routerStateRepository = new();
        RecordingOutboundRouter outboundRouter = new();
        RecordingAgentRuntime runtime = new(ContainerRunStatus.Success, "<internal>reasoning</internal>assistant reply");
        RecordingChannel channel = new(groupJid);
        GroupMessageProcessorService service = new(
            new InMemoryMessageRepository(new Dictionary<ChatJid, IReadOnlyList<StoredMessage>>
            {
                [groupJid] = [new StoredMessage("message-1", groupJid, "sender-1", "User", "@Andy hello", timestamp)]
            }),
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
            {
                [groupJid] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", timestamp)
            }),
            routerStateRepository,
            new PassThroughSenderAuthorizationService(),
            new FakeMessageFormatter(),
            outboundRouter,
            runtime,
            new RecordingGroupExecutionQueue(),
            new ActiveGroupSessionRegistry(),
            [channel],
            new NullAgentEventSink(),
            new NullFileAttachmentRepository(),
            "Andy",
            "UTC",
            "/tmp/netclaw-test/groups");

        bool result = await service.ProcessAsync(groupJid);

        Assert.True(result);
        Assert.Equal("formatted:@Andy hello", runtime.LastPrompt);
        Assert.Equal("assistant reply", Assert.Single(outboundRouter.Messages).Text);
        Assert.Equal([(groupJid, true), (groupJid, false)], channel.TypingCalls);
        Assert.Equal(timestamp.ToString("O"), routerStateRepository.Entries["last_agent_timestamp:team@jid"].Value);
    }

    [Fact]
    public async Task ProcessAsync_SkipsCursorAdvanceWhenTriggerMissing()
    {
        ChatJid groupJid = new("team@jid");
        InMemoryRouterStateRepository routerStateRepository = new();
        RecordingAgentRuntime runtime = new(ContainerRunStatus.Success, "assistant reply");
        GroupMessageProcessorService service = new(
            new InMemoryMessageRepository(new Dictionary<ChatJid, IReadOnlyList<StoredMessage>>
            {
                [groupJid] = [new StoredMessage("message-1", groupJid, "sender-1", "User", "plain hello", DateTimeOffset.UtcNow)]
            }),
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
            {
                [groupJid] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow)
            }),
            routerStateRepository,
            new PassThroughSenderAuthorizationService(),
            new FakeMessageFormatter(),
            new RecordingOutboundRouter(),
            runtime,
            new RecordingGroupExecutionQueue(),
            new ActiveGroupSessionRegistry(),
            [],
            new NullAgentEventSink(),
            new NullFileAttachmentRepository(),
            "Andy",
            "UTC",
            "/tmp/netclaw-test/groups");

        bool result = await service.ProcessAsync(groupJid);

        Assert.True(result);
        Assert.Equal(string.Empty, runtime.LastPrompt);
        Assert.Empty(routerStateRepository.Entries);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsFalseWhenRuntimeFails()
    {
        ChatJid groupJid = new("team@jid");
        InMemoryRouterStateRepository routerStateRepository = new();
        RecordingChannel channel = new(groupJid);
        GroupMessageProcessorService service = new(
            new InMemoryMessageRepository(new Dictionary<ChatJid, IReadOnlyList<StoredMessage>>
            {
                [groupJid] = [new StoredMessage("message-1", groupJid, "sender-1", "User", "@Andy hello", DateTimeOffset.UtcNow)]
            }),
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
            {
                [groupJid] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow)
            }),
            routerStateRepository,
            new PassThroughSenderAuthorizationService(),
            new FakeMessageFormatter(),
            new RecordingOutboundRouter(),
            new RecordingAgentRuntime(ContainerRunStatus.Error, null),
            new RecordingGroupExecutionQueue(),
            new ActiveGroupSessionRegistry(),
            [channel],
            new NullAgentEventSink(),
            new NullFileAttachmentRepository(),
            "Andy",
            "UTC",
            "/tmp/netclaw-test/groups");

        bool result = await service.ProcessAsync(groupJid);

        Assert.False(result);
        Assert.Equal([(groupJid, true), (groupJid, false)], channel.TypingCalls);
        Assert.Empty(routerStateRepository.Entries);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsFalseWhenRuntimeIsInterrupted()
    {
        ChatJid groupJid = new("team@jid");
        InMemoryRouterStateRepository routerStateRepository = new();
        RecordingChannel channel = new(groupJid);
        GroupMessageProcessorService service = new(
            new InMemoryMessageRepository(new Dictionary<ChatJid, IReadOnlyList<StoredMessage>>
            {
                [groupJid] = [new StoredMessage("message-1", groupJid, "sender-1", "User", "@Andy hello", DateTimeOffset.UtcNow)]
            }),
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
            {
                [groupJid] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow)
            }),
            routerStateRepository,
            new PassThroughSenderAuthorizationService(),
            new FakeMessageFormatter(),
            new RecordingOutboundRouter(),
            new RecordingAgentRuntime(ContainerRunStatus.Interrupted, null),
            new RecordingGroupExecutionQueue(),
            new ActiveGroupSessionRegistry(),
            [channel],
            new NullAgentEventSink(),
            new NullFileAttachmentRepository(),
            "Andy",
            "UTC",
            "/tmp/netclaw-test/groups");

        bool result = await service.ProcessAsync(groupJid);

        Assert.False(result);
        Assert.Equal([(groupJid, true), (groupJid, false)], channel.TypingCalls);
        Assert.Empty(routerStateRepository.Entries);
    }

    [Fact]
    public async Task ProcessAsync_RoutesStreamedCompletedMessageWithoutDuplicatingFinalResult()
    {
        ChatJid groupJid = new("team@jid");
        RecordingOutboundRouter outboundRouter = new();
        RecordingGroupExecutionQueue queue = new();
        GroupMessageProcessorService service = new(
            new InMemoryMessageRepository(new Dictionary<ChatJid, IReadOnlyList<StoredMessage>>
            {
                [groupJid] = [new StoredMessage("message-1", groupJid, "sender-1", "User", "@Andy hello", DateTimeOffset.UtcNow)]
            }),
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
            {
                [groupJid] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow)
            }),
            new InMemoryRouterStateRepository(),
            new PassThroughSenderAuthorizationService(),
            new FakeMessageFormatter(),
            outboundRouter,
            new RecordingAgentRuntime(
                ContainerRunStatus.Success,
                "assistant reply",
                [new ContainerStreamEvent(
                    ContainerEventKind.MessageCompleted,
                    new ContainerOutput(ContainerRunStatus.Running, "assistant reply", new SessionId("session-1"), null),
                    DateTimeOffset.UtcNow),
                 new ContainerStreamEvent(
                    ContainerEventKind.Idle,
                    new ContainerOutput(ContainerRunStatus.Running, null, new SessionId("session-1"), null),
                    DateTimeOffset.UtcNow)]),
            queue,
                    new ActiveGroupSessionRegistry(),
            [],
            new NullAgentEventSink(),
            new NullFileAttachmentRepository(),
            "Andy",
            "UTC",
            "/tmp/netclaw-test/groups");

        bool result = await service.ProcessAsync(groupJid);

        Assert.True(result);
        Assert.Single(outboundRouter.Messages);
        Assert.Equal("assistant reply", outboundRouter.Messages[0].Text);
        Assert.Equal(groupJid, Assert.Single(queue.IdleNotifications));
    }

    [Fact]
    public async Task ProcessAsync_DropModeFiltersDeniedMessagesFromPrompt()
    {
        ChatJid groupJid = new("team@jid");
        RecordingAgentRuntime runtime = new(ContainerRunStatus.Success, "assistant reply");
        RecordingChannel channel = new(groupJid);
        GroupMessageProcessorService service = new(
            new InMemoryMessageRepository(new Dictionary<ChatJid, IReadOnlyList<StoredMessage>>
            {
                [groupJid] =
                [
                    new StoredMessage("message-1", groupJid, "blocked", "Blocked", "@Andy blocked", DateTimeOffset.UtcNow.AddSeconds(-1)),
                    new StoredMessage("message-2", groupJid, "allowed", "Allowed", "@Andy allowed", DateTimeOffset.UtcNow)
                ]
            }),
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
            {
                [groupJid] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow)
            }),
            new InMemoryRouterStateRepository(),
            new FilteringSenderAuthorizationService(["allowed"]),
            new FakeMessageFormatter(),
            new RecordingOutboundRouter(),
            runtime,
            new RecordingGroupExecutionQueue(),
            new ActiveGroupSessionRegistry(),
            [channel],
            new NullAgentEventSink(),
            new NullFileAttachmentRepository(),
            "Andy",
            "UTC",
            "/tmp/netclaw-test/groups");

        bool result = await service.ProcessAsync(groupJid);

        Assert.True(result);
        Assert.Equal("formatted:@Andy allowed", runtime.LastPrompt);
        Assert.Equal([(groupJid, true), (groupJid, false)], channel.TypingCalls);
    }

    [Fact]
    public async Task ProcessAsync_IgnoresTypingIndicatorFailures()
    {
        ChatJid groupJid = new("team@jid");
        GroupMessageProcessorService service = new(
            new InMemoryMessageRepository(new Dictionary<ChatJid, IReadOnlyList<StoredMessage>>
            {
                [groupJid] = [new StoredMessage("message-1", groupJid, "sender-1", "User", "@Andy hello", DateTimeOffset.UtcNow)]
            }),
            new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
            {
                [groupJid] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow)
            }),
            new InMemoryRouterStateRepository(),
            new PassThroughSenderAuthorizationService(),
            new FakeMessageFormatter(),
            new RecordingOutboundRouter(),
            new RecordingAgentRuntime(ContainerRunStatus.Success, "assistant reply"),
            new RecordingGroupExecutionQueue(),
            new ActiveGroupSessionRegistry(),
            [new ThrowingChannel(groupJid)],
            new NullAgentEventSink(),
            new NullFileAttachmentRepository(),
            "Andy",
            "UTC",
            "/tmp/netclaw-test/groups");

        bool result = await service.ProcessAsync(groupJid);

        Assert.True(result);
    }

    private sealed class FakeMessageFormatter : IMessageFormatter
    {
        public string FormatInbound(IReadOnlyList<StoredMessage> messages, string timezone)
            => $"formatted:{string.Join('|', messages.Select(message => message.Content))}";

        public string NormalizeOutbound(string rawText)
            => rawText.Replace("<internal>reasoning</internal>", string.Empty, StringComparison.Ordinal);

        public IReadOnlyList<OutboundFileReference> ExtractFileReferences(string rawText) => [];
    }

    private sealed class RecordingOutboundRouter : IOutboundRouter
    {
        public List<(ChatJid ChatJid, string Text)> Messages { get; } = [];

        public List<(ChatJid ChatJid, string FilePath, string FileName)> Files { get; } = [];

        public Task RouteAsync(IReadOnlyList<IChannel> channels, ChatJid chatJid, string text, CancellationToken cancellationToken = default)
        {
            Messages.Add((chatJid, text));
            return Task.CompletedTask;
        }

        public Task RouteFileAsync(IReadOnlyList<IChannel> channels, ChatJid chatJid, string filePath, string fileName, CancellationToken cancellationToken = default)
        {
            Files.Add((chatJid, filePath, fileName));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAgentRuntime : IAgentRuntime
    {
        private readonly IReadOnlyList<ContainerStreamEvent> streamEvents;
        private readonly string? result;
        private readonly ContainerRunStatus status;

        public RecordingAgentRuntime(ContainerRunStatus status, string? result, IReadOnlyList<ContainerStreamEvent>? streamEvents = null)
        {
            this.status = status;
            this.result = result;
            this.streamEvents = streamEvents ?? [];
        }

        public string LastPrompt { get; private set; } = string.Empty;

        public Task<ContainerExecutionResult> ExecuteAsync(ContainerInput input, Func<ContainerStreamEvent, CancellationToken, Task>? onStreamEvent = null, CancellationToken cancellationToken = default)
        {
            LastPrompt = input.Prompt;

            if (onStreamEvent is not null)
            {
                foreach (ContainerStreamEvent streamEvent in streamEvents)
                {
                    onStreamEvent(streamEvent, cancellationToken).GetAwaiter().GetResult();
                }
            }

            return Task.FromResult(new ContainerExecutionResult(status, result, new SessionId("session-1"), status == ContainerRunStatus.Error ? "runtime failed" : null, new ContainerName("agent-fake-team")));
        }

        public Task<IInteractiveContainerSession> StartInteractiveSessionAsync(ContainerInput input, Func<ContainerStreamEvent, CancellationToken, Task>? onStreamEvent = null, CancellationToken cancellationToken = default)
        {
            LastPrompt = input.Prompt;

            if (onStreamEvent is not null)
            {
                foreach (ContainerStreamEvent streamEvent in streamEvents)
                {
                    onStreamEvent(streamEvent, cancellationToken).GetAwaiter().GetResult();
                }
            }

            return Task.FromResult<IInteractiveContainerSession>(new RecordingInteractiveContainerSession(status, result));
        }
    }

    private sealed class RecordingInteractiveContainerSession : IInteractiveContainerSession
    {
        public RecordingInteractiveContainerSession(ContainerRunStatus status, string? result)
        {
            Completion = Task.FromResult(new ContainerExecutionResult(
                status,
                result,
                new SessionId("session-1"),
                status is ContainerRunStatus.Error or ContainerRunStatus.Interrupted ? "runtime failed" : null,
                new ContainerName("agent-fake-team")));
        }

        public SessionId? SessionId => new("session-1");

        public ContainerName ContainerName => new("agent-fake-team");

        public Task<ContainerExecutionResult> Completion { get; }

        public bool TryPostInput(string text) => true;

        public void RequestClose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingGroupExecutionQueue : IGroupExecutionQueue
    {
        public List<ChatJid> IdleNotifications { get; } = [];

        public void CloseInput(ChatJid groupJid)
        {
        }

        public void EnqueueMessageCheck(ChatJid groupJid)
        {
        }

        public void EnqueueTask(ChatJid groupJid, TaskId taskId, Func<CancellationToken, Task> workItem)
        {
        }

        public void NotifyIdle(ChatJid groupJid)
        {
            IdleNotifications.Add(groupJid);
        }

        public bool SendMessage(ChatJid groupJid, string text)
        {
            return false;
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

        public Task SendFileAsync(ChatJid chatJid, string filePath, string fileName, string? threadTs, CancellationToken cancellationToken = default) => Task.CompletedTask;

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

        public Task SendFileAsync(ChatJid chatJid, string filePath, string fileName, string? threadTs, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool Owns(ChatJid chatJid) => chatJid == ownedJid;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetTypingAsync(ChatJid chatJid, bool isTyping, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("typing failed");
        }

        public Task SyncGroupsAsync(bool force, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryMessageRepository : IMessageRepository
    {
        private readonly IReadOnlyDictionary<ChatJid, IReadOnlyList<StoredMessage>> messages;

        public InMemoryMessageRepository(IReadOnlyDictionary<ChatJid, IReadOnlyList<StoredMessage>> messages)
        {
            this.messages = messages;
        }

        public Task<IReadOnlyList<ChatInfo>> GetAllChatsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChatInfo>>([]);

        public Task<IReadOnlyList<StoredMessage>> GetMessagesSinceAsync(ChatJid chatJid, DateTimeOffset? since, string assistantName, CancellationToken cancellationToken = default)
            => Task.FromResult(messages.TryGetValue(chatJid, out IReadOnlyList<StoredMessage>? groupMessages)
                ? (IReadOnlyList<StoredMessage>)groupMessages.Where(message => since is null || message.Timestamp > since).ToList()
                : []);

        public Task<IReadOnlyList<StoredMessage>> GetChatHistoryAsync(ChatJid chatJid, int limit, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredMessage>>([]);

        public Task<IReadOnlyList<StoredMessage>> GetNewMessagesAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredMessage>>([]);

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

    private sealed class NullAgentEventSink : IAgentEventSink
    {
        public void Record(ContainerStreamEvent streamEvent, GroupFolder groupFolder, ChatJid chatJid, bool isScheduledTask, string? taskId) { }

        public void SetBroadcastCallback(Action<AgentActivityEvent> callback) { }
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

    private sealed class NullFileAttachmentRepository : IFileAttachmentRepository
    {
        public Task StoreAsync(FileAttachment attachment, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<FileAttachment?> GetByFileIdAsync(string fileId, CancellationToken cancellationToken = default) => Task.FromResult<FileAttachment?>(null);

        public Task<IReadOnlyList<FileAttachment>> GetByMessageAsync(string messageId, ChatJid chatJid, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FileAttachment>>([]);

        public Task<IReadOnlyDictionary<string, IReadOnlyList<FileAttachment>>> GetByMessagesAsync(IEnumerable<string> messageIds, ChatJid chatJid, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<FileAttachment>>>(new Dictionary<string, IReadOnlyList<FileAttachment>>());
    }
}

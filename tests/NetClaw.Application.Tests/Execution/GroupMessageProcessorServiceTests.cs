using NetClaw.Application.Execution;
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
            new FakeMessageFormatter(),
            outboundRouter,
            runtime,
            [],
            "Andy",
            "UTC");

        bool result = await service.ProcessAsync(groupJid);

        Assert.True(result);
        Assert.Equal("formatted:@Andy hello", runtime.LastPrompt);
        Assert.Equal("assistant reply", Assert.Single(outboundRouter.Messages).Text);
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
            new FakeMessageFormatter(),
            new RecordingOutboundRouter(),
            runtime,
            [],
            "Andy",
            "UTC");

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
            new FakeMessageFormatter(),
            new RecordingOutboundRouter(),
            new RecordingAgentRuntime(ContainerRunStatus.Error, null),
            [],
            "Andy",
            "UTC");

        bool result = await service.ProcessAsync(groupJid);

        Assert.False(result);
        Assert.Empty(routerStateRepository.Entries);
    }

    private sealed class FakeMessageFormatter : IMessageFormatter
    {
        public string FormatInbound(IReadOnlyList<StoredMessage> messages, string timezone)
            => $"formatted:{string.Join('|', messages.Select(message => message.Content))}";

        public string NormalizeOutbound(string rawText)
            => rawText.Replace("<internal>reasoning</internal>", string.Empty, StringComparison.Ordinal);
    }

    private sealed class RecordingOutboundRouter : IOutboundRouter
    {
        public List<(ChatJid ChatJid, string Text)> Messages { get; } = [];

        public Task RouteAsync(IReadOnlyList<IChannel> channels, ChatJid chatJid, string text, CancellationToken cancellationToken = default)
        {
            Messages.Add((chatJid, text));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAgentRuntime : IAgentRuntime
    {
        private readonly string? result;
        private readonly ContainerRunStatus status;

        public RecordingAgentRuntime(ContainerRunStatus status, string? result)
        {
            this.status = status;
            this.result = result;
        }

        public string LastPrompt { get; private set; } = string.Empty;

        public Task<ContainerExecutionResult> ExecuteAsync(ContainerInput input, Func<ContainerStreamEvent, CancellationToken, Task>? onStreamEvent = null, CancellationToken cancellationToken = default)
        {
            LastPrompt = input.Prompt;
            return Task.FromResult(new ContainerExecutionResult(status, result, new SessionId("session-1"), status == ContainerRunStatus.Error ? "runtime failed" : null, new ContainerName("agent-fake-team")));
        }
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

        public Task SetAsync(RouterStateEntry entry, CancellationToken cancellationToken = default)
        {
            Entries[entry.Key] = entry;
            return Task.CompletedTask;
        }
    }
}
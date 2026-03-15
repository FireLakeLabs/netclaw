using Microsoft.Extensions.Logging.Abstractions;
using NetClaw.Application.Channels;
using NetClaw.Application.Observability;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;
using NetClaw.Host.Services;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Host.Tests;

public sealed class ChannelWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_ConnectsPollsAndDisconnectsInboundChannels()
    {
        RecordingMessageRepository repository = new();
        FakeInboundChannel channel = new();
        ChannelWorker worker = new(
            [channel],
            new ChannelIngressService(repository, new NullMessageNotifier()),
            new ChannelWorkerOptions
            {
                PollInterval = TimeSpan.FromMilliseconds(20),
                InitialSyncOnStart = true
            },
            NullLogger<ChannelWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await repository.MessageStored.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(channel.Connected);
        Assert.True(channel.Disconnected);
        Assert.True(channel.SyncCalled);
        Assert.Equal("hello", Assert.Single(repository.Messages).Content);
        Assert.Equal("Team", Assert.Single(repository.Chats).Name);
    }

    private sealed class FakeInboundChannel : IInboundChannel
    {
        private int pollCount;

        public ChannelName Name => new("fake");

        public bool IsConnected => Connected && !Disconnected;

        public bool Connected { get; private set; }

        public bool Disconnected { get; private set; }

        public bool SyncCalled { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            Connected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            Disconnected = true;
            return Task.CompletedTask;
        }

        public bool Owns(ChatJid chatJid) => true;

        public Task SendMessageAsync(ChatJid chatJid, string text, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetTypingAsync(ChatJid chatJid, bool isTyping, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SyncGroupsAsync(bool force, CancellationToken cancellationToken = default)
        {
            SyncCalled = true;
            return Task.CompletedTask;
        }

        public async Task PollInboundAsync(Func<ChannelMessage, CancellationToken, Task> onMessage, Func<ChannelMetadataEvent, CancellationToken, Task> onMetadata, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref pollCount) != 1)
            {
                return;
            }

            ChatJid chatJid = new("team@jid");
            await onMetadata(new ChannelMetadataEvent(chatJid, DateTimeOffset.UtcNow, "Team", Name, true), cancellationToken);
            await onMessage(new ChannelMessage(chatJid, new StoredMessage("message-1", chatJid, "sender-1", "User", "hello", DateTimeOffset.UtcNow)), cancellationToken);
        }
    }

    private sealed class RecordingMessageRepository : IMessageRepository
    {
        public List<ChatInfo> Chats { get; } = [];

        public TaskCompletionSource<bool> MessageStored { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<StoredMessage> Messages { get; } = [];

        public Task<IReadOnlyList<ChatInfo>> GetAllChatsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChatInfo>>(Chats);

        public Task<IReadOnlyList<StoredMessage>> GetMessagesSinceAsync(ChatJid chatJid, DateTimeOffset? since, string assistantName, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredMessage>>([]);

        public Task<IReadOnlyList<StoredMessage>> GetChatHistoryAsync(ChatJid chatJid, int limit, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredMessage>>([]);

        public Task<IReadOnlyList<StoredMessage>> GetNewMessagesAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StoredMessage>>([]);

        public Task StoreChatMetadataAsync(ChatInfo chatInfo, CancellationToken cancellationToken = default)
        {
            Chats.Add(chatInfo);
            return Task.CompletedTask;
        }

        public Task StoreMessageAsync(StoredMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            MessageStored.TrySetResult(true);
            return Task.CompletedTask;
        }
    }
}

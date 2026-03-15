using NetClaw.Application.Observability;
using NetClaw.Application.Routing;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Tests.Routing;

public sealed class ChannelOutboundRouterTests
{
    [Fact]
    public async Task RouteAsync_SendsViaOwningConnectedChannel()
    {
        FakeMessageRepository messageRepo = new();
        FakeChannel channel = new(new ChannelName("whatsapp"), owns: true, isConnected: true);
        ChannelOutboundRouter router = new(messageRepo, new NullMessageNotifier());

        await router.RouteAsync([channel], new ChatJid("chat@jid"), "hello");

        Assert.Single(channel.SentMessages);
        Assert.Equal("hello", channel.SentMessages[0]);
    }

    [Fact]
    public async Task RouteAsync_ThrowsWhenNoConnectedChannelOwnsTheChat()
    {
        FakeMessageRepository messageRepo = new();
        FakeChannel channel = new(new ChannelName("whatsapp"), owns: false, isConnected: true);
        ChannelOutboundRouter router = new(messageRepo, new NullMessageNotifier());

        await Assert.ThrowsAsync<InvalidOperationException>(() => router.RouteAsync([channel], new ChatJid("chat@jid"), "hello"));
    }

    [Fact]
    public async Task RouteAsync_StoresOutboundMessageInRepository()
    {
        FakeMessageRepository messageRepo = new();
        FakeChannel channel = new(new ChannelName("whatsapp"), owns: true, isConnected: true);
        ChannelOutboundRouter router = new(messageRepo, new NullMessageNotifier());

        await router.RouteAsync([channel], new ChatJid("chat@jid"), "hello");

        Assert.Single(messageRepo.StoredMessages);
        StoredMessage stored = messageRepo.StoredMessages[0];
        Assert.Equal("hello", stored.Content);
        Assert.Equal("agent", stored.Sender);
        Assert.True(stored.IsFromMe);
        Assert.True(stored.IsBotMessage);
    }

    private sealed class FakeChannel : IChannel
    {
        private readonly bool owns;

        public FakeChannel(ChannelName name, bool owns, bool isConnected)
        {
            Name = name;
            this.owns = owns;
            IsConnected = isConnected;
        }

        public List<string> SentMessages { get; } = [];

        public ChannelName Name { get; }

        public bool IsConnected { get; }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool Owns(ChatJid chatJid) => owns;

        public Task SendMessageAsync(ChatJid chatJid, string text, CancellationToken cancellationToken = default)
        {
            SentMessages.Add(text);
            return Task.CompletedTask;
        }

        public Task SetTypingAsync(ChatJid chatJid, bool isTyping, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SyncGroupsAsync(bool force, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMessageRepository : IMessageRepository
    {
        public List<StoredMessage> StoredMessages { get; } = [];

        public Task StoreMessageAsync(StoredMessage message, CancellationToken cancellationToken = default)
        {
            StoredMessages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StoredMessage>> GetNewMessagesAsync(DateTimeOffset since, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StoredMessage>>([]);

        public Task<IReadOnlyList<StoredMessage>> GetMessagesSinceAsync(ChatJid chatJid, DateTimeOffset? since, string assistantName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StoredMessage>>([]);

        public Task<IReadOnlyList<StoredMessage>> GetChatHistoryAsync(ChatJid chatJid, int limit, DateTimeOffset? since = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StoredMessage>>([]);

        public Task<IReadOnlyList<ChatInfo>> GetAllChatsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ChatInfo>>([]);

        public Task StoreChatMetadataAsync(ChatInfo chatInfo, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}

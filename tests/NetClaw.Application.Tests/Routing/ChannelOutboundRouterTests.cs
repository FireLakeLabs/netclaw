using NetClaw.Application.Routing;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Tests.Routing;

public sealed class ChannelOutboundRouterTests
{
    [Fact]
    public async Task RouteAsync_SendsViaOwningConnectedChannel()
    {
        FakeChannel channel = new(new ChannelName("whatsapp"), owns: true, isConnected: true);
        ChannelOutboundRouter router = new();

        await router.RouteAsync([channel], new ChatJid("chat@jid"), "hello");

        Assert.Single(channel.SentMessages);
        Assert.Equal("hello", channel.SentMessages[0]);
    }

    [Fact]
    public async Task RouteAsync_ThrowsWhenNoConnectedChannelOwnsTheChat()
    {
        FakeChannel channel = new(new ChannelName("whatsapp"), owns: false, isConnected: true);
        ChannelOutboundRouter router = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() => router.RouteAsync([channel], new ChatJid("chat@jid"), "hello"));
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
}

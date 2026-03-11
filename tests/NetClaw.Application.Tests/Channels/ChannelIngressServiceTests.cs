using NetClaw.Application.Channels;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Application.Tests.Channels;

public sealed class ChannelIngressServiceTests
{
    [Fact]
    public async Task HandleMetadataAsync_PersistsChatMetadata()
    {
        RecordingMessageRepository repository = new();
        ChannelIngressService service = new(repository);
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        await service.HandleMetadataAsync(new ChannelMetadataEvent(
            new ChatJid("team@jid"),
            timestamp,
            "Team",
            new ChannelName("reference-file"),
            true));

        ChatInfo chat = Assert.Single(repository.Chats);
        Assert.Equal("Team", chat.Name);
        Assert.Equal("reference-file", chat.Channel.Value);
        Assert.True(chat.IsGroup);
    }

    [Fact]
    public async Task HandleMessageAsync_PersistsInboundMessage()
    {
        RecordingMessageRepository repository = new();
        ChannelIngressService service = new(repository);
        StoredMessage message = new("message-1", new ChatJid("team@jid"), "sender-1", "User", "hello", DateTimeOffset.UtcNow);

        await service.HandleMessageAsync(new ChannelName("reference-file"), new ChannelMessage(message.ChatJid, message));

        StoredMessage stored = Assert.Single(repository.Messages);
        Assert.Equal("message-1", stored.Id);
        Assert.Equal("hello", stored.Content);
    }

    private sealed class RecordingMessageRepository : IMessageRepository
    {
        public List<ChatInfo> Chats { get; } = [];

        public List<StoredMessage> Messages { get; } = [];

        public Task<IReadOnlyList<ChatInfo>> GetAllChatsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChatInfo>>(Chats);

        public Task<IReadOnlyList<StoredMessage>> GetMessagesSinceAsync(ChatJid chatJid, DateTimeOffset? since, string assistantName, CancellationToken cancellationToken = default)
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
            return Task.CompletedTask;
        }
    }
}

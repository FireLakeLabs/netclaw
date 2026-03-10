using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Persistence.Sqlite;

namespace NetClaw.Infrastructure.Tests.Persistence.Sqlite;

public sealed class SqliteMessageRepositoryTests
{
    [Fact]
    public async Task StoreAndQueryMessages_RoundTripsExpectedState()
    {
        await using TestSqliteDatabase database = new();
        await database.SchemaInitializer.InitializeAsync();
        SqliteMessageRepository repository = new(database.ConnectionFactory);

        ChatInfo chatInfo = new(new ChatJid("chat@jid"), "Team Chat", DateTimeOffset.UtcNow.AddMinutes(-1), new ChannelName("whatsapp"), true);
        await repository.StoreChatMetadataAsync(chatInfo);

        StoredMessage visibleMessage = new("msg-1", new ChatJid("chat@jid"), "sender-1", "Sender 1", "Hello", DateTimeOffset.UtcNow, false, false);
        StoredMessage botMessage = new("msg-2", new ChatJid("chat@jid"), "sender-2", "Sender 2", "Andy: hidden", DateTimeOffset.UtcNow.AddSeconds(1), false, true);
        await repository.StoreMessageAsync(visibleMessage);
        await repository.StoreMessageAsync(botMessage);

        IReadOnlyList<StoredMessage> messages = await repository.GetMessagesSinceAsync(new ChatJid("chat@jid"), null, "Andy");
        IReadOnlyList<ChatInfo> chats = await repository.GetAllChatsAsync();
        IReadOnlyList<StoredMessage> newMessages = await repository.GetNewMessagesAsync(DateTimeOffset.UtcNow.AddMinutes(-5));

        Assert.Single(messages);
        Assert.Single(chats);
        Assert.Equal(2, newMessages.Count);
        Assert.Equal("Hello", messages[0].Content);
        Assert.Equal("Team Chat", chats[0].Name);
    }
}
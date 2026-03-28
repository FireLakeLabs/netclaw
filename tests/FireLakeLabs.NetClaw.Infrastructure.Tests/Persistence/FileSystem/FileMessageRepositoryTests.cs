using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.FileSystem;

public sealed class FileMessageRepositoryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"netclaw-test-{Guid.NewGuid():N}");
    private FileMessageRepository CreateRepository() => new(new FileStoragePaths(StorageOptions.Create(_tempDir)));

    public FileMessageRepositoryTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static StoredMessage MakeMessage(string id, string jid, string content, DateTimeOffset? ts = null, bool isBot = false) =>
        new(id, new ChatJid(jid), "sender", "Sender Name", content, ts ?? DateTimeOffset.UtcNow, false, isBot);

    [Fact]
    public async Task StoreAndGetNewMessages_RoundTrips()
    {
        FileMessageRepository repo = CreateRepository();
        DateTimeOffset since = DateTimeOffset.UtcNow.AddMinutes(-5);
        StoredMessage msg = MakeMessage("msg-1", "chat@jid", "Hello");

        await repo.StoreMessageAsync(msg);
        IReadOnlyList<StoredMessage> result = await repo.GetNewMessagesAsync(since);

        Assert.Single(result);
        Assert.Equal("Hello", result[0].Content);
    }

    [Fact]
    public async Task GetNewMessages_ExcludesMessagesBeforeSince()
    {
        FileMessageRepository repo = CreateRepository();
        DateTimeOffset since = DateTimeOffset.UtcNow;

        await repo.StoreMessageAsync(MakeMessage("old", "chat@jid", "Old", ts: since.AddMinutes(-10)));
        await repo.StoreMessageAsync(MakeMessage("new", "chat@jid", "New", ts: since.AddSeconds(1)));

        IReadOnlyList<StoredMessage> result = await repo.GetNewMessagesAsync(since);

        Assert.Single(result);
        Assert.Equal("New", result[0].Content);
    }

    [Fact]
    public async Task GetNewMessages_DoesNotReadUnrelatedChats()
    {
        FileMessageRepository repo = CreateRepository();
        DateTimeOffset since = DateTimeOffset.UtcNow;

        // Active chat
        await repo.StoreMessageAsync(MakeMessage("m1", "active@jid", "Hi", ts: since.AddSeconds(1)));
        // Inactive chat (message before since)
        await repo.StoreMessageAsync(MakeMessage("m2", "inactive@jid", "Old", ts: since.AddMinutes(-10)));

        // Only active chat should be read
        IReadOnlyList<StoredMessage> result = await repo.GetNewMessagesAsync(since);

        Assert.Single(result);
        Assert.Equal("active@jid", result[0].ChatJid.Value);
    }

    [Fact]
    public async Task StoreMessage_IsIdempotent_WhenSameMessageStoredTwice()
    {
        FileMessageRepository repo = CreateRepository();
        StoredMessage msg = MakeMessage("msg-1", "chat@jid", "Hello", ts: DateTimeOffset.UtcNow.AddSeconds(1));

        await repo.StoreMessageAsync(msg);
        await repo.StoreMessageAsync(msg);

        IReadOnlyList<StoredMessage> result = await repo.GetNewMessagesAsync(DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.Single(result);
    }

    [Fact]
    public async Task GetMessagesSinceAsync_ExcludesBotMessages()
    {
        FileMessageRepository repo = CreateRepository();
        DateTimeOffset since = DateTimeOffset.UtcNow.AddMinutes(-5);
        ChatJid jid = new("chat@jid");

        await repo.StoreMessageAsync(MakeMessage("m1", jid.Value, "Human message", isBot: false));
        await repo.StoreMessageAsync(MakeMessage("m2", jid.Value, "Bot message", isBot: true));

        IReadOnlyList<StoredMessage> result = await repo.GetMessagesSinceAsync(jid, since, "assistant");

        Assert.Single(result);
        Assert.Equal("Human message", result[0].Content);
    }

    [Fact]
    public async Task GetMessagesSinceAsync_ExcludesAssistantPrefixMessages()
    {
        FileMessageRepository repo = CreateRepository();
        DateTimeOffset since = DateTimeOffset.UtcNow.AddMinutes(-5);
        ChatJid jid = new("chat@jid");

        await repo.StoreMessageAsync(MakeMessage("m1", jid.Value, "Regular message"));
        await repo.StoreMessageAsync(MakeMessage("m2", jid.Value, "assistant: Assistant reply"));

        IReadOnlyList<StoredMessage> result = await repo.GetMessagesSinceAsync(jid, since, "assistant");

        Assert.Single(result);
        Assert.Equal("Regular message", result[0].Content);
    }

    [Fact]
    public async Task GetChatHistoryAsync_ReturnsLastNMessages()
    {
        FileMessageRepository repo = CreateRepository();
        ChatJid jid = new("chat@jid");

        for (int i = 1; i <= 5; i++)
        {
            await repo.StoreMessageAsync(MakeMessage($"msg-{i}", jid.Value, $"Message {i}", ts: DateTimeOffset.UtcNow.AddMinutes(i)));
        }

        IReadOnlyList<StoredMessage> history = await repo.GetChatHistoryAsync(jid, 3);

        Assert.Equal(3, history.Count);
        Assert.Equal("Message 3", history[0].Content);
        Assert.Equal("Message 5", history[2].Content);
    }

    [Fact]
    public async Task GetAllChatsAsync_ReturnsChatMetadata()
    {
        FileMessageRepository repo = CreateRepository();
        await repo.StoreChatMetadataAsync(new ChatInfo(new ChatJid("chat@jid"), "My Chat", DateTimeOffset.UtcNow, new ChannelName("slack"), true));

        IReadOnlyList<ChatInfo> chats = await repo.GetAllChatsAsync();

        Assert.Single(chats);
        Assert.Equal("My Chat", chats[0].Name);
    }

    [Fact]
    public async Task ConcurrentWrites_DoNotCorruptMessages()
    {
        FileMessageRepository repo = CreateRepository();
        ChatJid jid = new("concurrent@jid");
        DateTimeOffset baseTime = DateTimeOffset.UtcNow;

        Task[] tasks = Enumerable.Range(1, 20)
            .Select(i => repo.StoreMessageAsync(
                MakeMessage($"msg-{i}", jid.Value, $"Message {i}", ts: baseTime.AddMilliseconds(i))))
            .ToArray();

        await Task.WhenAll(tasks);

        IReadOnlyList<StoredMessage> result = await repo.GetNewMessagesAsync(baseTime.AddMinutes(-1));
        Assert.Equal(20, result.Count);
    }

    [Fact]
    public async Task MessagesAndMetadata_SurviveRestart()
    {
        DateTimeOffset ts = DateTimeOffset.UtcNow.AddSeconds(1);
        await CreateRepository().StoreMessageAsync(MakeMessage("m1", "chat@jid", "Persisted", ts: ts));

        IReadOnlyList<StoredMessage> result = await CreateRepository().GetNewMessagesAsync(ts.AddSeconds(-1));
        Assert.Single(result);
        Assert.Equal("Persisted", result[0].Content);
    }
}

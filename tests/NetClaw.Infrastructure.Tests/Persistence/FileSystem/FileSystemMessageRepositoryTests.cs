using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Persistence.FileSystem;

namespace NetClaw.Infrastructure.Tests.Persistence.FileSystem;

public sealed class FileSystemMessageRepositoryTests
{
    [Fact]
    public async Task StoreAndQueryMessages_RoundTripsExpectedState()
    {
        using TestFileSystemDirectory dir = new();
        FileSystemMessageRepository repository = new(dir.DataDirectory);

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

    [Fact]
    public async Task StoreMessageAsync_WritesJsonlAndMarkdownFiles()
    {
        using TestFileSystemDirectory dir = new();
        FileSystemMessageRepository repository = new(dir.DataDirectory);

        StoredMessage message = new("msg-1", new ChatJid("alice@example.com"), "alice", "Alice", "Hello world", DateTimeOffset.UtcNow, false, false);
        await repository.StoreMessageAsync(message);

        string sanitized = FileSystemMessageRepository.SanitizeJid("alice@example.com");
        string chatDir = Path.Combine(dir.DataDirectory, "messages", sanitized);

        Assert.True(File.Exists(Path.Combine(chatDir, "messages.jsonl")));
        Assert.True(File.Exists(Path.Combine(chatDir, "history.md")));
        Assert.True(File.Exists(Path.Combine(chatDir, "chat.json")));

        string jsonl = await File.ReadAllTextAsync(Path.Combine(chatDir, "messages.jsonl"));
        Assert.Contains("Hello world", jsonl);
        Assert.Contains("msg-1", jsonl);

        string md = await File.ReadAllTextAsync(Path.Combine(chatDir, "history.md"));
        Assert.Contains("Alice", md);
        Assert.Contains("Hello world", md);
    }

    [Fact]
    public async Task GetChatHistoryAsync_ReturnsLastNMessages_InChronologicalOrder()
    {
        using TestFileSystemDirectory dir = new();
        FileSystemMessageRepository repository = new(dir.DataDirectory);

        DateTimeOffset base_ = DateTimeOffset.UtcNow;
        for (int i = 1; i <= 5; i++)
        {
            StoredMessage msg = new($"msg-{i}", new ChatJid("chat@jid"), "sender", "Sender", $"Message {i}", base_.AddSeconds(i), false, false);
            await repository.StoreMessageAsync(msg);
        }

        IReadOnlyList<StoredMessage> history = await repository.GetChatHistoryAsync(new ChatJid("chat@jid"), 3);

        Assert.Equal(3, history.Count);
        Assert.Equal("Message 3", history[0].Content);
        Assert.Equal("Message 4", history[1].Content);
        Assert.Equal("Message 5", history[2].Content);
    }

    [Fact]
    public async Task GetMessagesSinceAsync_ExcludesBotMessagesAndBotPrefixedContent()
    {
        using TestFileSystemDirectory dir = new();
        FileSystemMessageRepository repository = new(dir.DataDirectory);

        DateTimeOffset base_ = DateTimeOffset.UtcNow;
        await repository.StoreMessageAsync(new("id-1", new ChatJid("chat@jid"), "alice", "Alice", "Visible", base_.AddSeconds(1), false, false));
        await repository.StoreMessageAsync(new("id-2", new ChatJid("chat@jid"), "bot", "Bot", "Bot message", base_.AddSeconds(2), false, true));
        await repository.StoreMessageAsync(new("id-3", new ChatJid("chat@jid"), "alice", "Alice", "Andy: prefixed", base_.AddSeconds(3), false, false));

        IReadOnlyList<StoredMessage> messages = await repository.GetMessagesSinceAsync(new ChatJid("chat@jid"), null, "Andy");

        Assert.Single(messages);
        Assert.Equal("Visible", messages[0].Content);
    }

    [Fact]
    public async Task StoreChatMetadataAsync_PreservesLaterLastMessageTime()
    {
        using TestFileSystemDirectory dir = new();
        FileSystemMessageRepository repository = new(dir.DataDirectory);

        DateTimeOffset later = DateTimeOffset.UtcNow;
        DateTimeOffset earlier = later.AddHours(-1);

        ChatInfo first = new(new ChatJid("chat@jid"), "Chat", later, new ChannelName("slack"), false);
        await repository.StoreChatMetadataAsync(first);

        ChatInfo second = new(new ChatJid("chat@jid"), "Chat Updated", earlier, new ChannelName("slack"), false);
        await repository.StoreChatMetadataAsync(second);

        IReadOnlyList<ChatInfo> chats = await repository.GetAllChatsAsync();

        Assert.Single(chats);
        Assert.Equal("Chat Updated", chats[0].Name);
        Assert.Equal(later, chats[0].LastMessageTime);
    }

    [Fact]
    public async Task GetNewMessagesAsync_ReturnsEmptyForEmptyRepository()
    {
        using TestFileSystemDirectory dir = new();
        FileSystemMessageRepository repository = new(dir.DataDirectory);

        IReadOnlyList<StoredMessage> result = await repository.GetNewMessagesAsync(DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetChatHistoryAsync_ThrowsForZeroOrNegativeLimit()
    {
        using TestFileSystemDirectory dir = new();
        FileSystemMessageRepository repository = new(dir.DataDirectory);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.GetChatHistoryAsync(new ChatJid("chat@jid"), 0));
    }

    [Fact]
    public void SanitizeJid_ReplacesUnsafeCharacters()
    {
        Assert.Equal("alice_at_example.com", FileSystemMessageRepository.SanitizeJid("alice@example.com"));
        Assert.Equal("123456789_at_g.us", FileSystemMessageRepository.SanitizeJid("123456789@g.us"));
    }
}

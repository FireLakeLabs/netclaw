using System.Text.Json;
using System.Text.Json.Serialization;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// Stores chat messages as JSONL files (one per chat) and generates a human-readable
/// <c>history.md</c> alongside each conversation. Chat metadata is stored in a
/// per-chat <c>chat.json</c> file. All files land under
/// <c>{dataDirectory}/messages/{sanitised-jid}/</c>.
/// </summary>
public sealed class FileSystemMessageRepository : IMessageRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string messagesRoot;

    public FileSystemMessageRepository(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException("Data directory is required.", nameof(dataDirectory));
        }

        messagesRoot = Path.Combine(dataDirectory, "messages");
    }

    // -------------------------------------------------------------------------
    // Write operations
    // -------------------------------------------------------------------------

    public async Task StoreMessageAsync(StoredMessage message, CancellationToken cancellationToken = default)
    {
        string chatDir = EnsureChatDirectory(message.ChatJid);

        // Append one JSONL record to messages.jsonl
        MessageRecord record = ToRecord(message);
        string jsonLine = JsonSerializer.Serialize(record, JsonOptions);
        await File.AppendAllTextAsync(Path.Combine(chatDir, "messages.jsonl"), jsonLine + "\n", cancellationToken);

        // Append one human-readable line to history.md
        string label = message.IsBotMessage ? $"[Bot] {message.SenderName}" : message.SenderName;
        string mdLine = $"**{message.Timestamp:u}** {label}: {message.Content}\n";
        await File.AppendAllTextAsync(Path.Combine(chatDir, "history.md"), mdLine, cancellationToken);

        // Update only last_message_time in chat metadata; preserve name, channel, and isGroup from
        // any existing metadata (matching the SQLite ON CONFLICT behaviour for StoreMessageAsync).
        ChatMetadataRecord? existing = await TryReadChatMetadataAsync(chatDir, cancellationToken);
        string name = existing?.Name ?? message.ChatJid.Value;
        string? channel = existing?.Channel;
        bool isGroup = existing?.IsGroup ?? false;
        DateTimeOffset existingLastMessageTime = existing is not null
            ? DateTimeOffset.Parse(existing.LastMessageTime, null, System.Globalization.DateTimeStyles.RoundtripKind)
            : DateTimeOffset.MinValue;
        DateTimeOffset effectiveLastMessageTime = message.Timestamp > existingLastMessageTime
            ? message.Timestamp
            : existingLastMessageTime;

        await UpsertChatMetadataAsync(chatDir, message.ChatJid.Value, name, effectiveLastMessageTime, channel, isGroup, cancellationToken);
    }

    public async Task StoreChatMetadataAsync(ChatInfo chatInfo, CancellationToken cancellationToken = default)
    {
        string chatDir = EnsureChatDirectory(chatInfo.Jid);

        // Read existing metadata so we never overwrite a later last_message_time
        DateTimeOffset existingLastMessageTime = DateTimeOffset.MinValue;
        ChatMetadataRecord? existing = await TryReadChatMetadataAsync(chatDir, cancellationToken);
        if (existing is not null)
        {
            existingLastMessageTime = DateTimeOffset.Parse(existing.LastMessageTime, null, System.Globalization.DateTimeStyles.RoundtripKind);
        }

        DateTimeOffset effectiveLastMessageTime = chatInfo.LastMessageTime > existingLastMessageTime
            ? chatInfo.LastMessageTime
            : existingLastMessageTime;

        await UpsertChatMetadataAsync(chatDir, chatInfo.Jid.Value, chatInfo.Name, effectiveLastMessageTime, chatInfo.Channel.Value, chatInfo.IsGroup, cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Read operations
    // -------------------------------------------------------------------------

    public async Task<IReadOnlyList<ChatInfo>> GetAllChatsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(messagesRoot))
        {
            return [];
        }

        List<ChatInfo> chats = [];
        foreach (string chatDir in Directory.GetDirectories(messagesRoot))
        {
            ChatMetadataRecord? metadata = await TryReadChatMetadataAsync(chatDir, cancellationToken);
            if (metadata is not null)
            {
                chats.Add(ToChatInfo(metadata));
            }
        }

        chats.Sort(static (a, b) => DateTimeOffset.Compare(b.LastMessageTime, a.LastMessageTime));
        return chats;
    }

    public async Task<IReadOnlyList<StoredMessage>> GetMessagesSinceAsync(
        ChatJid chatJid,
        DateTimeOffset? since,
        string assistantName,
        CancellationToken cancellationToken = default)
    {
        string botPrefix = $"{assistantName}:";
        List<StoredMessage> messages = await ReadAllMessagesAsync(chatJid, cancellationToken);
        return messages
            .Where(m => (since is null || m.Timestamp > since)
                        && !m.IsBotMessage
                        && !m.Content.StartsWith(botPrefix, StringComparison.Ordinal))
            .OrderBy(static m => m.Timestamp)
            .ToList();
    }

    public async Task<IReadOnlyList<StoredMessage>> GetChatHistoryAsync(
        ChatJid chatJid,
        int limit,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be greater than zero.");
        }

        List<StoredMessage> messages = await ReadAllMessagesAsync(chatJid, cancellationToken);
        IEnumerable<StoredMessage> filtered = since is null ? messages : messages.Where(m => m.Timestamp > since);
        return filtered
            .OrderByDescending(static m => m.Timestamp)
            .Take(limit)
            .OrderBy(static m => m.Timestamp)
            .ToList();
    }

    public async Task<IReadOnlyList<StoredMessage>> GetNewMessagesAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(messagesRoot))
        {
            return [];
        }

        List<StoredMessage> results = [];
        foreach (string chatDir in Directory.GetDirectories(messagesRoot))
        {
            ChatMetadataRecord? metadata = await TryReadChatMetadataAsync(chatDir, cancellationToken);
            if (metadata is null)
            {
                continue;
            }

            ChatJid chatJid = new(metadata.Jid);
            List<StoredMessage> messages = await ReadAllMessagesAsync(chatJid, cancellationToken);
            results.AddRange(messages.Where(m => m.Timestamp > since));
        }

        results.Sort(static (a, b) => DateTimeOffset.Compare(a.Timestamp, b.Timestamp));
        return results;
    }

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private string EnsureChatDirectory(ChatJid chatJid)
    {
        string chatDir = GetChatDirectory(chatJid);
        Directory.CreateDirectory(chatDir);
        return chatDir;
    }

    private string GetChatDirectory(ChatJid chatJid)
    {
        return Path.Combine(messagesRoot, SanitizeJid(chatJid.Value));
    }

    private async Task<List<StoredMessage>> ReadAllMessagesAsync(ChatJid chatJid, CancellationToken cancellationToken)
    {
        string messagesFile = Path.Combine(GetChatDirectory(chatJid), "messages.jsonl");
        if (!File.Exists(messagesFile))
        {
            return [];
        }

        string raw = await File.ReadAllTextAsync(messagesFile, cancellationToken);
        List<StoredMessage> messages = [];
        foreach (string rawLine in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            MessageRecord record = JsonSerializer.Deserialize<MessageRecord>(line, JsonOptions)
                ?? throw new InvalidOperationException($"Unexpected null when deserialising message record from {messagesFile}.");
            messages.Add(ToStoredMessage(record));
        }

        return messages;
    }

    private async Task UpsertChatMetadataAsync(
        string chatDir,
        string jid,
        string name,
        DateTimeOffset lastMessageTime,
        string? channel,
        bool isGroup,
        CancellationToken cancellationToken)
    {
        ChatMetadataRecord metadata = new(jid, name, lastMessageTime.ToString("O"), channel, isGroup);
        string json = JsonSerializer.Serialize(metadata, JsonOptions);
        await File.WriteAllTextAsync(Path.Combine(chatDir, "chat.json"), json, cancellationToken);
    }

    private static async Task<ChatMetadataRecord?> TryReadChatMetadataAsync(string chatDir, CancellationToken cancellationToken)
    {
        string metadataFile = Path.Combine(chatDir, "chat.json");
        if (!File.Exists(metadataFile))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(metadataFile, cancellationToken);
        return JsonSerializer.Deserialize<ChatMetadataRecord>(json, JsonOptions);
    }

    // Characters not safe in directory names: @ / \ : * ? " < > |
    // Replace @ with _at_ to stay human-readable.
    internal static string SanitizeJid(string jid)
    {
        return jid
            .Replace("@", "_at_", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("\\", "_", StringComparison.Ordinal)
            .Replace(":", "_", StringComparison.Ordinal)
            .Replace("*", "_", StringComparison.Ordinal)
            .Replace("?", "_", StringComparison.Ordinal)
            .Replace("\"", "_", StringComparison.Ordinal)
            .Replace("<", "_", StringComparison.Ordinal)
            .Replace(">", "_", StringComparison.Ordinal)
            .Replace("|", "_", StringComparison.Ordinal);
    }

    private static MessageRecord ToRecord(StoredMessage m) =>
        new(m.Id, m.ChatJid.Value, m.Sender, m.SenderName, m.Content, m.Timestamp.ToString("O"), m.IsFromMe, m.IsBotMessage);

    private static StoredMessage ToStoredMessage(MessageRecord r) =>
        new(r.Id, new ChatJid(r.ChatJid), r.Sender, r.SenderName, r.Content,
            DateTimeOffset.Parse(r.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind),
            r.IsFromMe, r.IsBotMessage);

    private static ChatInfo ToChatInfo(ChatMetadataRecord r) =>
        new(new ChatJid(r.Jid), r.Name,
            DateTimeOffset.Parse(r.LastMessageTime, null, System.Globalization.DateTimeStyles.RoundtripKind),
            new ChannelName(string.IsNullOrWhiteSpace(r.Channel) ? "unknown" : r.Channel),
            r.IsGroup);

    // -------------------------------------------------------------------------
    // Internal record types for serialisation
    // -------------------------------------------------------------------------

    private sealed record MessageRecord(
        string Id,
        string ChatJid,
        string Sender,
        string SenderName,
        string Content,
        string Timestamp,
        bool IsFromMe,
        bool IsBotMessage);

    private sealed record ChatMetadataRecord(
        string Jid,
        string Name,
        string LastMessageTime,
        string? Channel,
        bool IsGroup);
}

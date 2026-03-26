using System.Collections.Concurrent;
using System.Text.Json;
using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// File-based message repository. Messages are stored as JSONL under
/// <c>data/chats/{chatJid}/messages.jsonl</c> with chat metadata in
/// <c>data/chats/{chatJid}/metadata.json</c>.
///
/// An in-memory write-through cache of latest timestamps per chat makes
/// <see cref="GetNewMessagesAsync"/> O(1) in the common case.
/// Per-chat semaphores serialize concurrent writes to the same chat file.
/// </summary>
public sealed class FileMessageRepository : IMessageRepository
{
    private const int RecentIdWindowSize = 100;

    private readonly FileStoragePaths _paths;

    // Populated from metadata.json on startup; updated on every StoreMessageAsync.
    private readonly ConcurrentDictionary<string, DateTimeOffset> _latestTimestamps = new(StringComparer.Ordinal);

    // Per-chat write serialization.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _chatLocks = new(StringComparer.Ordinal);

    // Last RecentIdWindowSize message IDs per chat; used for deduplication.
    private readonly ConcurrentDictionary<string, HashSet<string>> _recentMessageIds = new(StringComparer.Ordinal);

    public FileMessageRepository(FileStoragePaths paths)
    {
        _paths = paths;
        Directory.CreateDirectory(_paths.ChatsDirectory);
        LoadStartupCaches();
    }

    public async Task StoreMessageAsync(StoredMessage message, CancellationToken cancellationToken = default)
    {
        string jid = message.ChatJid.Value;
        SemaphoreSlim chatLock = _chatLocks.GetOrAdd(jid, _ => new SemaphoreSlim(1, 1));

        await chatLock.WaitAsync(cancellationToken);
        try
        {
            // Deduplicate: skip if we've seen this message recently.
            HashSet<string> recentIds = _recentMessageIds.GetOrAdd(jid, _ => new HashSet<string>(StringComparer.Ordinal));
            if (recentIds.Contains(message.Id))
            {
                return;
            }

            string chatDir = _paths.ChatDirectory(jid);
            Directory.CreateDirectory(chatDir);

            // Append message to JSONL.
            await JsonlFileAppender.AppendAsync(
                _paths.MessagesFilePath(jid),
                ToRecord(message),
                FileSystemJsonOptions.Jsonl,
                cancellationToken);

            // Atomic-write metadata.json.
            ChatMetadataRecord metadataRecord = new(
                jid,
                GetOrDefaultChatName(message),
                message.Timestamp,
                message.ChatJid.Value, // channel unknown at this layer; use jid as fallback
                false);

            // Preserve existing metadata if present (don't overwrite known name/channel/isGroup).
            string metaPath = _paths.ChatMetadataFilePath(jid);
            if (File.Exists(metaPath))
            {
                string existing = await File.ReadAllTextAsync(metaPath, cancellationToken);
                ChatMetadataRecord? existing_meta = JsonSerializer.Deserialize<ChatMetadataRecord>(existing, FileSystemJsonOptions.Config);
                if (existing_meta is not null)
                {
                    metadataRecord = existing_meta with { LastMessageTime = message.Timestamp };
                }
            }

            await FileAtomicWriter.WriteJsonAsync(metaPath, metadataRecord, FileSystemJsonOptions.Config, cancellationToken);

            // Update in-memory caches.
            _latestTimestamps[jid] = message.Timestamp;
            recentIds.Add(message.Id);
            if (recentIds.Count > RecentIdWindowSize * 2)
            {
                // Trim to avoid unbounded growth; keep the newest by removing the oldest.
                // Since HashSet doesn't preserve insertion order, we just clear excess above 2x window.
                // A simple approach: rebuild from the tail of the JSONL on next startup.
                while (recentIds.Count > RecentIdWindowSize)
                {
                    recentIds.Remove(recentIds.First());
                }
            }
        }
        finally
        {
            chatLock.Release();
        }
    }

    public async Task<IReadOnlyList<StoredMessage>> GetNewMessagesAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        // Only read from chats that have had activity since the cutoff.
        IEnumerable<string> activeChatJids = _latestTimestamps
            .Where(kvp => kvp.Value > since)
            .Select(kvp => kvp.Key);

        List<StoredMessage> results = [];
        foreach (string jid in activeChatJids)
        {
            IReadOnlyList<MessageRecord> records = await JsonlFileReader.ReadFilteredAsync<MessageRecord>(
                _paths.MessagesFilePath(jid),
                r => r.Timestamp > since,
                FileSystemJsonOptions.Jsonl,
                cancellationToken);

            results.AddRange(records.Select(ToMessage));
        }

        results.Sort(static (a, b) => a.Timestamp.CompareTo(b.Timestamp));
        return results;
    }

    public async Task<IReadOnlyList<StoredMessage>> GetMessagesSinceAsync(ChatJid chatJid, DateTimeOffset? since, string assistantName, CancellationToken cancellationToken = default)
    {
        string prefix = assistantName + ":";
        IReadOnlyList<MessageRecord> records = await JsonlFileReader.ReadFilteredAsync<MessageRecord>(
            _paths.MessagesFilePath(chatJid.Value),
            r => (since is null || r.Timestamp > since)
                 && !r.IsBotMessage
                 && !r.Content.StartsWith(prefix, StringComparison.Ordinal),
            FileSystemJsonOptions.Jsonl,
            cancellationToken);

        // Deduplicate by id (first occurrence wins).
        Dictionary<string, StoredMessage> seen = [];
        foreach (MessageRecord record in records)
        {
            if (!seen.ContainsKey(record.Id))
            {
                seen[record.Id] = ToMessage(record);
            }
        }

        return seen.Values.ToList();
    }

    public async Task<IReadOnlyList<StoredMessage>> GetChatHistoryAsync(ChatJid chatJid, int limit, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MessageRecord> records = await JsonlFileReader.ReadFilteredAsync<MessageRecord>(
            _paths.MessagesFilePath(chatJid.Value),
            r => since is null || r.Timestamp >= since,
            FileSystemJsonOptions.Jsonl,
            cancellationToken);

        // Return last N messages (most recent), maintaining chronological order in the result.
        return records.TakeLast(limit).Select(ToMessage).ToList();
    }

    public Task<IReadOnlyList<ChatInfo>> GetAllChatsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_paths.ChatsDirectory))
        {
            return Task.FromResult<IReadOnlyList<ChatInfo>>([]);
        }

        List<ChatInfo> chats = [];
        foreach (string chatDir in Directory.GetDirectories(_paths.ChatsDirectory))
        {
            string metaPath = Path.Combine(chatDir, "metadata.json");
            if (!File.Exists(metaPath))
            {
                continue;
            }

            string json = File.ReadAllText(metaPath);
            ChatMetadataRecord? record = JsonSerializer.Deserialize<ChatMetadataRecord>(json, FileSystemJsonOptions.Config);
            if (record is not null)
            {
                chats.Add(ToChatInfo(record));
            }
        }

        return Task.FromResult<IReadOnlyList<ChatInfo>>(chats);
    }

    public async Task StoreChatMetadataAsync(ChatInfo chatInfo, CancellationToken cancellationToken = default)
    {
        string jid = chatInfo.Jid.Value;
        Directory.CreateDirectory(_paths.ChatDirectory(jid));

        ChatMetadataRecord record = new(jid, chatInfo.Name, chatInfo.LastMessageTime, chatInfo.Channel.Value, chatInfo.IsGroup);
        await FileAtomicWriter.WriteJsonAsync(_paths.ChatMetadataFilePath(jid), record, FileSystemJsonOptions.Config, cancellationToken);

        _latestTimestamps[jid] = chatInfo.LastMessageTime;
    }

    private void LoadStartupCaches()
    {
        if (!Directory.Exists(_paths.ChatsDirectory))
        {
            return;
        }

        foreach (string chatDir in Directory.GetDirectories(_paths.ChatsDirectory))
        {
            string jid = Path.GetFileName(chatDir);
            string metaPath = Path.Combine(chatDir, "metadata.json");

            if (File.Exists(metaPath))
            {
                string json = File.ReadAllText(metaPath);
                ChatMetadataRecord? meta = JsonSerializer.Deserialize<ChatMetadataRecord>(json, FileSystemJsonOptions.Config);
                if (meta is not null)
                {
                    _latestTimestamps[jid] = meta.LastMessageTime;
                }
            }

            // Seed dedup cache from the tail of messages.jsonl.
            string messagesFile = Path.Combine(chatDir, "messages.jsonl");
            if (File.Exists(messagesFile))
            {
                string[] tailLines = JsonlFileReader.ReadTailLinesAsync(messagesFile, RecentIdWindowSize).GetAwaiter().GetResult();
                HashSet<string> ids = new(StringComparer.Ordinal);
                foreach (string line in tailLines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        MessageRecord? r = JsonSerializer.Deserialize<MessageRecord>(line, FileSystemJsonOptions.Jsonl);
                        if (r is not null)
                        {
                            ids.Add(r.Id);
                        }
                    }
                    catch (JsonException) { }
                }

                _recentMessageIds[jid] = ids;
            }
        }
    }

    private static string GetOrDefaultChatName(StoredMessage message) =>
        message.SenderName ?? message.ChatJid.Value;

    private static StoredMessage ToMessage(MessageRecord r) =>
        StoredMessage.FromStorage(r.Id, new ChatJid(r.ChatJid), r.Sender, r.SenderName, r.Content, r.Timestamp, r.IsFromMe, r.IsBotMessage);

    private static MessageRecord ToRecord(StoredMessage m) =>
        new(m.Id, m.ChatJid.Value, m.Sender, m.SenderName, m.Content, m.Timestamp, m.IsFromMe, m.IsBotMessage);

    private static ChatInfo ToChatInfo(ChatMetadataRecord r) =>
        new(new ChatJid(r.Jid), r.Name, r.LastMessageTime, new ChannelName(string.IsNullOrWhiteSpace(r.Channel) ? "unknown" : r.Channel), r.IsGroup);

    private sealed record MessageRecord(
        string Id,
        string ChatJid,
        string Sender,
        string SenderName,
        string Content,
        DateTimeOffset Timestamp,
        bool IsFromMe,
        bool IsBotMessage);

    private sealed record ChatMetadataRecord(
        string Jid,
        string Name,
        DateTimeOffset LastMessageTime,
        string Channel,
        bool IsGroup);
}

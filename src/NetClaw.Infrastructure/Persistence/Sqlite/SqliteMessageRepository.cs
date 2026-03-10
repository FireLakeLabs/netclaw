using Microsoft.Data.Sqlite;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Infrastructure.Persistence.Sqlite;

public sealed class SqliteMessageRepository : IMessageRepository
{
    private readonly SqliteConnectionFactory connectionFactory;

    public SqliteMessageRepository(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ChatInfo>> GetAllChatsAsync(CancellationToken cancellationToken = default)
    {
        List<ChatInfo> chats = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT jid, name, last_message_time, channel, is_group FROM chats ORDER BY last_message_time DESC;";

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            chats.Add(new ChatInfo(
                new ChatJid(reader.GetString(0)),
                reader.GetString(1),
                DateTimeOffset.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                new ChannelName(reader.IsDBNull(3) ? "unknown" : reader.GetString(3)),
                reader.GetInt64(4) == 1));
        }

        return chats;
    }

    public async Task<IReadOnlyList<StoredMessage>> GetMessagesSinceAsync(ChatJid chatJid, DateTimeOffset? since, string assistantName, CancellationToken cancellationToken = default)
    {
        List<StoredMessage> messages = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, chat_jid, sender, sender_name, content, timestamp, is_from_me, is_bot_message
            FROM messages
            WHERE chat_jid = $chatJid
              AND ($since IS NULL OR timestamp > $since)
              AND is_bot_message = 0
              AND content NOT LIKE $botPrefix
            ORDER BY timestamp ASC;
            """;
        command.Parameters.AddWithValue("$chatJid", chatJid.Value);
        command.Parameters.AddWithValue("$since", since?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$botPrefix", $"{assistantName}:%");

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(ReadStoredMessage(reader));
        }

        return messages;
    }

    public async Task<IReadOnlyList<StoredMessage>> GetNewMessagesAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        List<StoredMessage> messages = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, chat_jid, sender, sender_name, content, timestamp, is_from_me, is_bot_message
            FROM messages
            WHERE timestamp > $since
            ORDER BY timestamp ASC;
            """;
        command.Parameters.AddWithValue("$since", since.ToString("O"));

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(ReadStoredMessage(reader));
        }

        return messages;
    }

    public async Task StoreChatMetadataAsync(ChatInfo chatInfo, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO chats (jid, name, last_message_time, channel, is_group)
            VALUES ($jid, $name, $lastMessageTime, $channel, $isGroup)
            ON CONFLICT(jid) DO UPDATE SET
                name = excluded.name,
                last_message_time = CASE
                    WHEN excluded.last_message_time > chats.last_message_time THEN excluded.last_message_time
                    ELSE chats.last_message_time
                END,
                channel = COALESCE(excluded.channel, chats.channel),
                is_group = excluded.is_group;
            """;
        command.Parameters.AddWithValue("$jid", chatInfo.Jid.Value);
        command.Parameters.AddWithValue("$name", chatInfo.Name);
        command.Parameters.AddWithValue("$lastMessageTime", chatInfo.LastMessageTime.ToString("O"));
        command.Parameters.AddWithValue("$channel", chatInfo.Channel.Value);
        command.Parameters.AddWithValue("$isGroup", chatInfo.IsGroup ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task StoreMessageAsync(StoredMessage message, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteTransaction transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (SqliteCommand chatCommand = connection.CreateCommand())
        {
            chatCommand.Transaction = transaction;
            chatCommand.CommandText =
                """
                INSERT INTO chats (jid, name, last_message_time, channel, is_group)
                VALUES ($jid, $name, $lastMessageTime, $channel, $isGroup)
                ON CONFLICT(jid) DO UPDATE SET
                    last_message_time = CASE
                        WHEN excluded.last_message_time > chats.last_message_time THEN excluded.last_message_time
                        ELSE chats.last_message_time
                    END;
                """;
            chatCommand.Parameters.AddWithValue("$jid", message.ChatJid.Value);
            chatCommand.Parameters.AddWithValue("$name", message.ChatJid.Value);
            chatCommand.Parameters.AddWithValue("$lastMessageTime", message.Timestamp.ToString("O"));
            chatCommand.Parameters.AddWithValue("$channel", DBNull.Value);
            chatCommand.Parameters.AddWithValue("$isGroup", 0);
            await chatCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (SqliteCommand messageCommand = connection.CreateCommand())
        {
            messageCommand.Transaction = transaction;
            messageCommand.CommandText =
                """
                INSERT OR REPLACE INTO messages (id, chat_jid, sender, sender_name, content, timestamp, is_from_me, is_bot_message)
                VALUES ($id, $chatJid, $sender, $senderName, $content, $timestamp, $isFromMe, $isBotMessage);
                """;
            messageCommand.Parameters.AddWithValue("$id", message.Id);
            messageCommand.Parameters.AddWithValue("$chatJid", message.ChatJid.Value);
            messageCommand.Parameters.AddWithValue("$sender", message.Sender);
            messageCommand.Parameters.AddWithValue("$senderName", message.SenderName);
            messageCommand.Parameters.AddWithValue("$content", message.Content);
            messageCommand.Parameters.AddWithValue("$timestamp", message.Timestamp.ToString("O"));
            messageCommand.Parameters.AddWithValue("$isFromMe", message.IsFromMe ? 1 : 0);
            messageCommand.Parameters.AddWithValue("$isBotMessage", message.IsBotMessage ? 1 : 0);
            await messageCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static StoredMessage ReadStoredMessage(SqliteDataReader reader)
    {
        return new StoredMessage(
            reader.GetString(0),
            new ChatJid(reader.GetString(1)),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.GetInt64(6) == 1,
            reader.GetInt64(7) == 1);
    }
}
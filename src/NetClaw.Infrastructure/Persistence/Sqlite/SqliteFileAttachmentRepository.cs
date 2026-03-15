using Microsoft.Data.Sqlite;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Infrastructure.Persistence.Sqlite;

public sealed class SqliteFileAttachmentRepository : IFileAttachmentRepository
{
    private readonly SqliteConnectionFactory connectionFactory;

    public SqliteFileAttachmentRepository(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task StoreAsync(FileAttachment attachment, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR IGNORE INTO file_attachments (file_id, message_id, chat_jid, file_name, mime_type, file_size, local_path, downloaded_at)
            VALUES ($fileId, $messageId, $chatJid, $fileName, $mimeType, $fileSize, $localPath, $downloadedAt);
            """;
        command.Parameters.AddWithValue("$fileId", attachment.FileId);
        command.Parameters.AddWithValue("$messageId", attachment.MessageId);
        command.Parameters.AddWithValue("$chatJid", attachment.ChatJid.Value);
        command.Parameters.AddWithValue("$fileName", attachment.FileName);
        command.Parameters.AddWithValue("$mimeType", (object?)attachment.MimeType ?? DBNull.Value);
        command.Parameters.AddWithValue("$fileSize", attachment.FileSize);
        command.Parameters.AddWithValue("$localPath", attachment.LocalPath);
        command.Parameters.AddWithValue("$downloadedAt", attachment.DownloadedAt.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<FileAttachment?> GetByFileIdAsync(string fileId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT file_id, message_id, chat_jid, file_name, mime_type, file_size, local_path, downloaded_at
            FROM file_attachments
            WHERE file_id = $fileId;
            """;
        command.Parameters.AddWithValue("$fileId", fileId);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadFileAttachment(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<FileAttachment>> GetByMessageAsync(string messageId, ChatJid chatJid, CancellationToken cancellationToken = default)
    {
        List<FileAttachment> attachments = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT file_id, message_id, chat_jid, file_name, mime_type, file_size, local_path, downloaded_at
            FROM file_attachments
            WHERE message_id = $messageId AND chat_jid = $chatJid
            ORDER BY file_name;
            """;
        command.Parameters.AddWithValue("$messageId", messageId);
        command.Parameters.AddWithValue("$chatJid", chatJid.Value);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            attachments.Add(ReadFileAttachment(reader));
        }

        return attachments;
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<FileAttachment>>> GetByMessagesAsync(IEnumerable<string> messageIds, ChatJid chatJid, CancellationToken cancellationToken = default)
    {
        Dictionary<string, List<FileAttachment>> result = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();

        List<string> idList = messageIds.ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<FileAttachment>>();
        }

        List<string> paramNames = [];
        for (int i = 0; i < idList.Count; i++)
        {
            string paramName = $"$id{i}";
            paramNames.Add(paramName);
            command.Parameters.AddWithValue(paramName, idList[i]);
        }

        command.CommandText =
            $"""
            SELECT file_id, message_id, chat_jid, file_name, mime_type, file_size, local_path, downloaded_at
            FROM file_attachments
            WHERE chat_jid = $chatJid AND message_id IN ({string.Join(", ", paramNames)})
            ORDER BY message_id, file_name;
            """;
        command.Parameters.AddWithValue("$chatJid", chatJid.Value);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            FileAttachment attachment = ReadFileAttachment(reader);
            if (!result.TryGetValue(attachment.MessageId, out List<FileAttachment>? list))
            {
                list = [];
                result[attachment.MessageId] = list;
            }

            list.Add(attachment);
        }

        return result.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<FileAttachment>)kvp.Value);
    }

    private static FileAttachment ReadFileAttachment(SqliteDataReader reader)
    {
        return new FileAttachment(
            reader.GetString(0),
            reader.GetString(1),
            new ChatJid(reader.GetString(2)),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetInt64(5),
            reader.GetString(6),
            DateTimeOffset.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind));
    }
}

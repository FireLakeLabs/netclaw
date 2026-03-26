using System.Collections.Concurrent;
using System.Text.Json;
using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// File-based attachment metadata repository. Each attachment is stored as
/// <c>data/chats/{chatJid}/attachments/{fileId}.json</c>.
/// An in-memory index maps fileId → (chatJid, path) for cross-chat lookups.
/// </summary>
public sealed class FileAttachmentRepository : IFileAttachmentRepository
{
    private readonly FileStoragePaths _paths;

    // fileId → (chatJid, full file path)
    private readonly ConcurrentDictionary<string, (string ChatJid, string Path)> _fileIdIndex = new(StringComparer.Ordinal);

    public FileAttachmentRepository(FileStoragePaths paths)
    {
        _paths = paths;
        LoadStartupIndex();
    }

    public async Task StoreAsync(FileAttachment attachment, CancellationToken cancellationToken = default)
    {
        string jid = attachment.ChatJid.Value;
        Directory.CreateDirectory(_paths.AttachmentsDirectory(jid));

        string filePath = _paths.AttachmentFilePath(jid, attachment.FileId);
        AttachmentRecord record = ToRecord(attachment);
        await FileAtomicWriter.WriteJsonAsync(filePath, record, FileSystemJsonOptions.Config, cancellationToken);

        _fileIdIndex[attachment.FileId] = (jid, filePath);
    }

    public async Task<FileAttachment?> GetByFileIdAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (!_fileIdIndex.TryGetValue(fileId, out (string ChatJid, string Path) entry))
        {
            return null;
        }

        if (!File.Exists(entry.Path))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(entry.Path, cancellationToken);
        AttachmentRecord? record = JsonSerializer.Deserialize<AttachmentRecord>(json, FileSystemJsonOptions.Config);
        return record is not null ? ToAttachment(record) : null;
    }

    public async Task<IReadOnlyList<FileAttachment>> GetByMessageAsync(string messageId, ChatJid chatJid, CancellationToken cancellationToken = default)
    {
        string attachmentsDir = _paths.AttachmentsDirectory(chatJid.Value);
        return await LoadAttachmentsFromDirectory(attachmentsDir, r => r.MessageId == messageId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<FileAttachment>>> GetByMessagesAsync(IEnumerable<string> messageIds, ChatJid chatJid, CancellationToken cancellationToken = default)
    {
        HashSet<string> idSet = new(messageIds, StringComparer.Ordinal);
        string attachmentsDir = _paths.AttachmentsDirectory(chatJid.Value);
        IReadOnlyList<FileAttachment> all = await LoadAttachmentsFromDirectory(
            attachmentsDir, r => idSet.Contains(r.MessageId), cancellationToken);

        Dictionary<string, List<FileAttachment>> grouped = [];
        foreach (FileAttachment attachment in all)
        {
            if (!grouped.TryGetValue(attachment.MessageId, out List<FileAttachment>? list))
            {
                list = [];
                grouped[attachment.MessageId] = list;
            }

            list.Add(attachment);
        }

        return grouped.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<FileAttachment>)kvp.Value);
    }

    private void LoadStartupIndex()
    {
        if (!Directory.Exists(_paths.ChatsDirectory))
        {
            return;
        }

        foreach (string chatDir in Directory.GetDirectories(_paths.ChatsDirectory))
        {
            string attachmentsDir = Path.Combine(chatDir, "attachments");
            if (!Directory.Exists(attachmentsDir))
            {
                continue;
            }

            string jid = Path.GetFileName(chatDir);
            foreach (string file in Directory.GetFiles(attachmentsDir, "*.json"))
            {
                string fileId = Path.GetFileNameWithoutExtension(file);
                _fileIdIndex[fileId] = (jid, file);
            }
        }
    }

    private static async Task<IReadOnlyList<FileAttachment>> LoadAttachmentsFromDirectory(
        string attachmentsDir,
        Func<AttachmentRecord, bool> predicate,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(attachmentsDir))
        {
            return [];
        }

        List<FileAttachment> results = [];
        foreach (string file in Directory.GetFiles(attachmentsDir, "*.json"))
        {
            string json = await File.ReadAllTextAsync(file, cancellationToken);
            AttachmentRecord? record = JsonSerializer.Deserialize<AttachmentRecord>(json, FileSystemJsonOptions.Config);
            if (record is not null && predicate(record))
            {
                results.Add(ToAttachment(record));
            }
        }

        return results;
    }

    private static FileAttachment ToAttachment(AttachmentRecord r) =>
        new(r.FileId, r.MessageId, new ChatJid(r.ChatJid), r.FileName, r.MimeType, r.FileSize, r.LocalPath, r.DownloadedAt);

    private static AttachmentRecord ToRecord(FileAttachment a) =>
        new(a.FileId, a.MessageId, a.ChatJid.Value, a.FileName, a.MimeType, a.FileSize, a.LocalPath, a.DownloadedAt);

    private sealed record AttachmentRecord(
        string FileId,
        string MessageId,
        string ChatJid,
        string FileName,
        string? MimeType,
        long FileSize,
        string LocalPath,
        DateTimeOffset DownloadedAt);
}

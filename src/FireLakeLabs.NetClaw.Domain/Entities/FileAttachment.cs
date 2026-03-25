using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Domain.Entities;

public sealed record FileAttachment
{
    public FileAttachment(string fileId, string messageId, ChatJid chatJid, string fileName, string? mimeType, long fileSize, string localPath, DateTimeOffset downloadedAt)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            throw new ArgumentException("File ID is required.", nameof(fileId));
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("Message ID is required.", nameof(messageId));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name is required.", nameof(fileName));
        }

        if (fileSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileSize), fileSize, "File size cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(localPath))
        {
            throw new ArgumentException("Local path is required.", nameof(localPath));
        }

        FileId = fileId.Trim();
        MessageId = messageId.Trim();
        ChatJid = chatJid;
        FileName = fileName.Trim();
        MimeType = mimeType;
        FileSize = fileSize;
        LocalPath = localPath;
        DownloadedAt = downloadedAt;
    }

    public string FileId { get; }

    public string MessageId { get; }

    public ChatJid ChatJid { get; }

    public string FileName { get; }

    public string? MimeType { get; }

    public long FileSize { get; }

    public string LocalPath { get; }

    public DateTimeOffset DownloadedAt { get; }
}

using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.FileSystem;

public sealed class FileAttachmentRepositoryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"netclaw-test-{Guid.NewGuid():N}");
    private FileAttachmentRepository CreateRepository() => new(new FileStoragePaths(StorageOptions.Create(_tempDir)));

    public FileAttachmentRepositoryTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static FileAttachment MakeAttachment(string fileId, string messageId, string chatJid) =>
        new(fileId, messageId, new ChatJid(chatJid), "photo.jpg", "image/jpeg", 1024, $"/data/files/{chatJid}/{fileId}/photo.jpg", DateTimeOffset.UtcNow);

    [Fact]
    public async Task GetByFileIdAsync_ReturnsNull_WhenNotFound()
    {
        FileAttachmentRepository repo = CreateRepository();
        FileAttachment? result = await repo.GetByFileIdAsync("unknown");
        Assert.Null(result);
    }

    [Fact]
    public async Task StoreAndGet_ByFileId_RoundTrips()
    {
        FileAttachmentRepository repo = CreateRepository();
        FileAttachment attachment = MakeAttachment("file-1", "msg-1", "chat@jid");

        await repo.StoreAsync(attachment);
        FileAttachment? result = await repo.GetByFileIdAsync("file-1");

        Assert.NotNull(result);
        Assert.Equal("file-1", result!.FileId);
        Assert.Equal("photo.jpg", result.FileName);
    }

    [Fact]
    public async Task GetByMessageAsync_ReturnsAttachmentsForMessage()
    {
        FileAttachmentRepository repo = CreateRepository();
        ChatJid jid = new("chat@jid");

        await repo.StoreAsync(MakeAttachment("f1", "msg-1", jid.Value));
        await repo.StoreAsync(MakeAttachment("f2", "msg-1", jid.Value));
        await repo.StoreAsync(MakeAttachment("f3", "msg-2", jid.Value));

        IReadOnlyList<FileAttachment> result = await repo.GetByMessageAsync("msg-1", jid);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByMessagesAsync_GroupsByMessageId()
    {
        FileAttachmentRepository repo = CreateRepository();
        ChatJid jid = new("chat@jid");

        await repo.StoreAsync(MakeAttachment("f1", "msg-1", jid.Value));
        await repo.StoreAsync(MakeAttachment("f2", "msg-2", jid.Value));
        await repo.StoreAsync(MakeAttachment("f3", "msg-2", jid.Value));

        IReadOnlyDictionary<string, IReadOnlyList<FileAttachment>> result = await repo.GetByMessagesAsync(["msg-1", "msg-2"], jid);

        Assert.Equal(2, result.Count);
        Assert.Single(result["msg-1"]);
        Assert.Equal(2, result["msg-2"].Count);
    }

    [Fact]
    public async Task FileIdIndex_SurvivesRestart()
    {
        await CreateRepository().StoreAsync(MakeAttachment("file-99", "msg-1", "chat@jid"));

        // New instance reads index from disk
        FileAttachment? result = await CreateRepository().GetByFileIdAsync("file-99");
        Assert.NotNull(result);
        Assert.Equal("file-99", result!.FileId);
    }
}

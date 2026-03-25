using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Domain.Tests.Entities;

public sealed class StoredMessageAndRunLogTests
{
    [Fact]
    public void StoredMessage_RejectsBlankContent()
    {
        Assert.Throws<ArgumentException>(
            () => new StoredMessage(
                "msg-1",
                new ChatJid("chat@jid"),
                "sender",
                "Sender",
                " ",
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void StoredMessage_AllowsBlankContentWhenAttachmentsPresent()
    {
        FileAttachment attachment = new("file-1", "msg-1", new ChatJid("chat@jid"), "photo.png", "image/png", 1024, "/tmp/photo.png", DateTimeOffset.UtcNow);
        StoredMessage message = new("msg-1", new ChatJid("chat@jid"), "sender", "Sender", "", DateTimeOffset.UtcNow, attachments: [attachment]);

        Assert.Equal(string.Empty, message.Content);
        Assert.Single(message.Attachments);
    }

    [Fact]
    public void FileAttachment_RejectsBlankFileId()
    {
        Assert.Throws<ArgumentException>(
            () => new FileAttachment(" ", "msg-1", new ChatJid("chat@jid"), "photo.png", "image/png", 1024, "/tmp/photo.png", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void FileAttachment_RejectsNegativeFileSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FileAttachment("file-1", "msg-1", new ChatJid("chat@jid"), "photo.png", "image/png", -1, "/tmp/photo.png", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void FileAttachment_AcceptsValidParameters()
    {
        FileAttachment attachment = new("file-1", "msg-1", new ChatJid("chat@jid"), "photo.png", "image/png", 1024, "/tmp/photo.png", DateTimeOffset.UtcNow);

        Assert.Equal("file-1", attachment.FileId);
        Assert.Equal("msg-1", attachment.MessageId);
        Assert.Equal("photo.png", attachment.FileName);
        Assert.Equal("image/png", attachment.MimeType);
        Assert.Equal(1024, attachment.FileSize);
    }

    [Fact]
    public void TaskRunLog_RequiresErrorForFailedRuns()
    {
        Assert.Throws<ArgumentException>(
            () => new TaskRunLog(
                new TaskId("task-1"),
                DateTimeOffset.UtcNow,
                TimeSpan.FromSeconds(2),
                ContainerRunStatus.Error,
                null,
                null));
    }

    [Fact]
    public void TaskRunLog_AcceptsSuccessfulRunsWithoutError()
    {
        TaskRunLog log = new(
            new TaskId("task-1"),
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(2),
            ContainerRunStatus.Success,
            "ok",
            null);

        Assert.Equal(ContainerRunStatus.Success, log.Status);
        Assert.Equal("ok", log.Result);
    }

    [Fact]
    public void TaskRunLog_AcceptsInterruptedRunsWithoutError()
    {
        TaskRunLog log = new(
            new TaskId("task-1"),
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(2),
            ContainerRunStatus.Interrupted,
            null,
            null);

        Assert.Equal(ContainerRunStatus.Interrupted, log.Status);
        Assert.Null(log.Error);
    }
}

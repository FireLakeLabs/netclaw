using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Domain.Tests.Entities;

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
}
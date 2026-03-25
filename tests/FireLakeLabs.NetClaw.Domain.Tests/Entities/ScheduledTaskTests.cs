using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using TaskStatusEnum = FireLakeLabs.NetClaw.Domain.Enums.TaskStatus;

namespace FireLakeLabs.NetClaw.Domain.Tests.Entities;

public sealed class ScheduledTaskTests
{
    [Fact]
    public void Constructor_AcceptsValidTask()
    {
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        ScheduledTask task = new(
            new TaskId("task-1"),
            new GroupFolder("team"),
            new ChatJid("team@jid"),
            "Run the task",
            ScheduleType.Cron,
            "0 * * * *",
            TaskContextMode.Group,
            createdAt.AddMinutes(1),
            null,
            null,
            TaskStatusEnum.Active,
            createdAt);

        Assert.Equal("Run the task", task.Prompt);
        Assert.Equal(ScheduleType.Cron, task.ScheduleType);
    }

    [Fact]
    public void Constructor_RejectsBlankPrompt()
    {
        Assert.Throws<ArgumentException>(
            () => new ScheduledTask(
                new TaskId("task-1"),
                new GroupFolder("team"),
                new ChatJid("team@jid"),
                " ",
                ScheduleType.Interval,
                "1000",
                TaskContextMode.Isolated,
                null,
                null,
                null,
                TaskStatusEnum.Active,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_TrimsLastResult()
    {
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        ScheduledTask task = new(
            new TaskId("task-2"),
            new GroupFolder("team"),
            new ChatJid("team@jid"),
            "Prompt",
            ScheduleType.Once,
            createdAt.ToString("O"),
            TaskContextMode.Isolated,
            createdAt,
            null,
            " done ",
            TaskStatusEnum.Completed,
            createdAt);

        Assert.Equal("done", task.LastResult);
    }
}

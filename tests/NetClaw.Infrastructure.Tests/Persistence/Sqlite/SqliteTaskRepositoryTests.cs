using Microsoft.Data.Sqlite;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Persistence.Sqlite;
using TaskStatusEnum = NetClaw.Domain.Enums.TaskStatus;

namespace NetClaw.Infrastructure.Tests.Persistence.Sqlite;

public sealed class SqliteTaskRepositoryTests
{
    [Fact]
    public async Task TaskRepository_CreatesQueriesUpdatesAndLogsTasks()
    {
        await using TestSqliteDatabase database = new();
        await database.SchemaInitializer.InitializeAsync();

        SqliteTaskRepository repository = new(database.ConnectionFactory);
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        ScheduledTask task = new(
            new TaskId("task-1"),
            new GroupFolder("team"),
            new ChatJid("team@jid"),
            "Prompt",
            ScheduleType.Interval,
            "60000",
            TaskContextMode.Group,
            createdAt.AddMinutes(1),
            null,
            null,
            TaskStatusEnum.Active,
            createdAt);

        await repository.CreateAsync(task);

        ScheduledTask? storedTask = await repository.GetByIdAsync(new TaskId("task-1"));
        IReadOnlyList<ScheduledTask> dueTasksBefore = await repository.GetDueTasksAsync(createdAt);

        Assert.NotNull(storedTask);
        Assert.Empty(dueTasksBefore);

        ScheduledTask updatedTask = new(
            storedTask!.Id,
            storedTask.GroupFolder,
            storedTask.ChatJid,
            storedTask.Prompt,
            storedTask.ScheduleType,
            storedTask.ScheduleValue,
            storedTask.ContextMode,
            createdAt.AddMinutes(-1),
            storedTask.LastRun,
            "done",
            TaskStatusEnum.Active,
            storedTask.CreatedAt);

        await repository.UpdateAsync(updatedTask);
        IReadOnlyList<ScheduledTask> dueTasksAfter = await repository.GetDueTasksAsync(createdAt);

        Assert.Single(dueTasksAfter);
        Assert.Equal("done", dueTasksAfter[0].LastResult);

        await repository.AppendRunLogAsync(new TaskRunLog(new TaskId("task-1"), createdAt, TimeSpan.FromSeconds(5), ContainerRunStatus.Success, "ok", null));

        await using SqliteConnection connection = database.ConnectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM task_run_logs WHERE task_id = 'task-1';";
        long runLogCount = (long)(await command.ExecuteScalarAsync() ?? 0L);

        Assert.Equal(1L, runLogCount);
    }
}

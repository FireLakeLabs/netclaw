using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;
using TaskStatusEnum = FireLakeLabs.NetClaw.Domain.Enums.TaskStatus;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.FileSystem;

public sealed class FileTaskRepositoryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"netclaw-test-{Guid.NewGuid():N}");
    private FileTaskRepository CreateRepository() => new(new FileStoragePaths(StorageOptions.Create(_tempDir)));

    public FileTaskRepositoryTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static ScheduledTask MakeTask(string id, DateTimeOffset? nextRun = null, TaskStatusEnum status = TaskStatusEnum.Active) =>
        new(
            new TaskId(id),
            new GroupFolder("team1"),
            new ChatJid("team@jid"),
            "Do something",
            ScheduleType.Interval,
            "60000",
            TaskContextMode.Group,
            nextRun,
            null,
            null,
            status,
            DateTimeOffset.UtcNow);

    [Fact]
    public async Task CreateAndGet_RoundTripsTask()
    {
        FileTaskRepository repo = CreateRepository();
        ScheduledTask task = MakeTask("task-1");
        await repo.CreateAsync(task);

        ScheduledTask? result = await repo.GetByIdAsync(new TaskId("task-1"));
        Assert.NotNull(result);
        Assert.Equal("Do something", result!.Prompt);
    }

    [Fact]
    public async Task GetDueTasks_ReturnsOnlyDueTasks()
    {
        FileTaskRepository repo = CreateRepository();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await repo.CreateAsync(MakeTask("future", nextRun: now.AddMinutes(5)));
        await repo.CreateAsync(MakeTask("past", nextRun: now.AddMinutes(-1)));

        IReadOnlyList<ScheduledTask> due = await repo.GetDueTasksAsync(now);

        Assert.Single(due);
        Assert.Equal("past", due[0].Id.Value);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChange()
    {
        FileTaskRepository repo = CreateRepository();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await repo.CreateAsync(MakeTask("task-1", nextRun: now.AddMinutes(5)));

        ScheduledTask original = (await repo.GetByIdAsync(new TaskId("task-1")))!;
        ScheduledTask updated = new(
            original.Id, original.GroupFolder, original.ChatJid, original.Prompt,
            original.ScheduleType, original.ScheduleValue, original.ContextMode,
            now.AddMinutes(-1), original.LastRun, "done", original.Status, original.CreatedAt);

        await repo.UpdateAsync(updated);
        IReadOnlyList<ScheduledTask> due = await repo.GetDueTasksAsync(now);

        Assert.Single(due);
        Assert.Equal("done", due[0].LastResult);
    }

    [Fact]
    public async Task AppendAndGetRunLogs_RoundTripsLogs()
    {
        FileTaskRepository repo = CreateRepository();
        await repo.CreateAsync(MakeTask("task-1"));

        await repo.AppendRunLogAsync(new TaskRunLog(new TaskId("task-1"), DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5), ContainerRunStatus.Success, "ok", null));
        await repo.AppendRunLogAsync(new TaskRunLog(new TaskId("task-1"), DateTimeOffset.UtcNow.AddMinutes(1), TimeSpan.FromSeconds(3), ContainerRunStatus.Success, "ok2", null));

        IReadOnlyList<TaskRunLog> logs = await repo.GetRunLogsAsync(new TaskId("task-1"), 10);
        Assert.Equal(2, logs.Count);
    }

    [Fact]
    public async Task GetRunLogsAsync_ReturnsLastN_MostRecentFirst()
    {
        FileTaskRepository repo = CreateRepository();
        await repo.CreateAsync(MakeTask("task-1"));

        for (int i = 1; i <= 5; i++)
        {
            await repo.AppendRunLogAsync(new TaskRunLog(new TaskId("task-1"), DateTimeOffset.UtcNow.AddMinutes(i), TimeSpan.FromSeconds(1), ContainerRunStatus.Success, $"run{i}", null));
        }

        IReadOnlyList<TaskRunLog> logs = await repo.GetRunLogsAsync(new TaskId("task-1"), 3);

        Assert.Equal(3, logs.Count);
        // Most recent first — run5 should be first
        Assert.Equal("run5", logs[0].Result);
    }

    [Fact]
    public async Task Tasks_SurviveRestart()
    {
        await CreateRepository().CreateAsync(MakeTask("t1", nextRun: DateTimeOffset.UtcNow.AddMinutes(-1)));

        IReadOnlyList<ScheduledTask> due = await CreateRepository().GetDueTasksAsync(DateTimeOffset.UtcNow);
        Assert.Single(due);
    }
}

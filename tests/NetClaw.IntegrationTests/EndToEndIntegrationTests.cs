using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetClaw.Domain.Contracts.Ipc;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Persistence.Sqlite;
using NetClaw.Setup;

namespace NetClaw.IntegrationTests;

public sealed class EndToEndIntegrationTests
{
    [Fact]
    public async Task SetupRegistration_IsVisibleThroughHostRepositories()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();

        try
        {
            SetupRunner runner = CreateRunner(projectRoot, homeDirectory);
            SetupResult registration = await runner.RunAsync(SetupCommand.Parse([
                "--step", "register",
                "--jid", "team@jid",
                "--name", "Team",
                "--trigger", "@Andy",
                "--folder", "team"
            ]));

            Assert.Equal(0, registration.ExitCode);

            using IHost host = CreateHost(projectRoot);
            await host.StartAsync();

            IGroupRepository repository = host.Services.GetRequiredService<IGroupRepository>();
            RegisteredGroup? group = await repository.GetByJidAsync(new ChatJid("team@jid"));

            Assert.NotNull(group);
            Assert.Equal("Team", group.Name);
            Assert.Equal("team", group.Folder.Value);

            await host.StopAsync();
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    [Fact]
    public async Task IpcSchedulingAndHostScheduler_PersistTaskLifecycle()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();

        try
        {
            using IHost host = CreateHost(projectRoot);
            await host.StartAsync();

            IGroupRepository groupRepository = host.Services.GetRequiredService<IGroupRepository>();
            await groupRepository.UpsertAsync(
                new ChatJid("team@jid"),
                new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow));

            IIpcCommandProcessor processor = host.Services.GetRequiredService<IIpcCommandProcessor>();
            await processor.ProcessAsync(
                new GroupFolder("team"),
                isMainGroup: false,
                new IpcTaskCommand(
                    TaskId: null,
                    Prompt: "Ping",
                    ScheduleType: ScheduleType.Once,
                    ScheduleValue: DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O"),
                    ContextMode: TaskContextMode.Group,
                    TargetJid: new ChatJid("team@jid")));

            ITaskRepository taskRepository = host.Services.GetRequiredService<ITaskRepository>();
            IReadOnlyList<ScheduledTask> createdTasks = await taskRepository.GetAllAsync();
            Assert.Single(createdTasks);

            ITaskSchedulerService schedulerService = host.Services.GetRequiredService<ITaskSchedulerService>();
            await schedulerService.RunDueTasksAsync(DateTimeOffset.UtcNow);

            ScheduledTask? updatedTask = await taskRepository.GetByIdAsync(createdTasks[0].Id);
            Assert.NotNull(updatedTask);
            Assert.Equal(NetClaw.Domain.Enums.TaskStatus.Completed, updatedTask.Status);

            SqliteConnectionFactory connectionFactory = host.Services.GetRequiredService<SqliteConnectionFactory>();
            await using SqliteConnection connection = connectionFactory.OpenConnection();
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM task_run_logs;";
            long logCount = (long)(await command.ExecuteScalarAsync())!;

            Assert.Equal(1L, logCount);

            await host.StopAsync();
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    [Fact]
    public async Task IpcWatcher_PollsTaskFilesAndPersistsScheduledTask()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();

        try
        {
            using IHost host = CreateHost(projectRoot);
            await host.StartAsync();

            IGroupRepository groupRepository = host.Services.GetRequiredService<IGroupRepository>();
            await groupRepository.UpsertAsync(
                new ChatJid("team@jid"),
                new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow));

            string taskDirectory = Path.Combine(projectRoot, "data", "ipc", "team", "tasks");
            Directory.CreateDirectory(taskDirectory);

            string taskFilePath = Path.Combine(taskDirectory, "task.json");
            await File.WriteAllTextAsync(
                taskFilePath,
                """
                {
                  "type": "schedule_task",
                  "prompt": "Ping from IPC",
                  "schedule_type": "once",
                  "schedule_value": "2026-03-10T00:00:00Z",
                  "context_mode": "group",
                  "targetJid": "team@jid"
                }
                """);

            IIpcCommandWatcher watcher = host.Services.GetRequiredService<IIpcCommandWatcher>();
            await watcher.PollOnceAsync();

            ITaskRepository taskRepository = host.Services.GetRequiredService<ITaskRepository>();
            IReadOnlyList<ScheduledTask> tasks = await taskRepository.GetAllAsync();

            Assert.Single(tasks);
            Assert.Equal("Ping from IPC", tasks[0].Prompt);
            Assert.Equal(TaskContextMode.Group, tasks[0].ContextMode);
            Assert.False(File.Exists(taskFilePath));

            await host.StopAsync();
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    [Fact]
    public async Task SetupVerify_ReportsSuccessAfterBootstrapArtifactsAreCreated()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();

        try
        {
            SetupRunner runner = CreateRunner(projectRoot, homeDirectory);
            await File.WriteAllTextAsync(Path.Combine(projectRoot, ".env"), "ANTHROPIC_API_KEY=test-key\n");

            await runner.RunAsync(SetupCommand.Parse([
                "--step", "register",
                "--jid", "team@jid",
                "--name", "Team",
                "--trigger", "@Andy",
                "--folder", "team"
            ]));
            await runner.RunAsync(SetupCommand.Parse(["--step", "mounts", "--empty"]));
            await runner.RunAsync(SetupCommand.Parse(["--step", "service", "--service-mode", "script"]));

            SetupResult verification = await runner.RunAsync(SetupCommand.Parse(["--step", "verify"]));

            Assert.Equal(0, verification.ExitCode);
            Assert.Equal("success", verification.Status["OVERALL_STATUS"]);
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    private static IHost CreateHost(string projectRoot)
    {
        return NetClaw.Host.Program.CreateHostBuilder([])
            .ConfigureAppConfiguration(configurationBuilder =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["NetClaw:ProjectRoot"] = projectRoot,
                    ["NetClaw:Scheduler:PollInterval"] = "00:10:00"
                });
            })
            .Build();
    }

    private static SetupRunner CreateRunner(string projectRoot, string homeDirectory)
    {
        return new SetupRunner(
            SetupPaths.Create(projectRoot, homeDirectory),
            new NetClaw.Infrastructure.FileSystem.PhysicalFileSystem(),
            new NetClaw.Infrastructure.Runtime.ProcessCommandRunner(),
            new NetClaw.Infrastructure.Runtime.PlatformDetectionService());
    }

    private static string CreateTemporaryPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"netclaw-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryPath(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
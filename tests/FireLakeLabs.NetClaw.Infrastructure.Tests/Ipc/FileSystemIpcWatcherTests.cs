using FireLakeLabs.NetClaw.Domain.Contracts.Ipc;
using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Contracts.Services;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.FileSystem;
using FireLakeLabs.NetClaw.Infrastructure.Ipc;
using Microsoft.Extensions.Logging.Abstractions;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Ipc;

public sealed class FileSystemIpcWatcherTests
{
    [Fact]
    public async Task PollOnceAsync_ProcessesMessageAndTaskFiles()
    {
        string projectRoot = CreateTemporaryPath();

        try
        {
            StorageOptions storageOptions = StorageOptions.Create(projectRoot);
            PhysicalFileSystem fileSystem = new();
            string messagePath = Path.Combine(storageOptions.DataDirectory, "ipc", "team", "messages", "message.json");
            string taskPath = Path.Combine(storageOptions.DataDirectory, "ipc", "team", "tasks", "task.json");

            Directory.CreateDirectory(Path.GetDirectoryName(messagePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(taskPath)!);

            await File.WriteAllTextAsync(
                messagePath,
                """
                {
                  "type": "message",
                  "chatJid": "team@jid",
                  "text": "hello"
                }
                """);
            await File.WriteAllTextAsync(
                taskPath,
                """
                {
                  "type": "schedule_task",
                  "task_id": "task-123",
                  "prompt": "Ping",
                  "schedule_type": "interval",
                  "schedule_value": "5000",
                  "context_mode": "group",
                  "target_jid": "team@jid"
                }
                """);

            RecordingIpcCommandProcessor processor = new();
            FileSystemIpcWatcher watcher = new(
                storageOptions,
                fileSystem,
                new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
                {
                    [new ChatJid("team@jid")] = new RegisteredGroup("Team", new GroupFolder("team"), "@Andy", DateTimeOffset.UtcNow)
                }),
                processor,
                NullLogger<FileSystemIpcWatcher>.Instance);

            await watcher.PollOnceAsync();

            Assert.Equal(2, processor.Invocations.Count);
            Assert.All(processor.Invocations, invocation =>
            {
                Assert.Equal("team", invocation.SourceGroup.Value);
                Assert.False(invocation.IsMainGroup);
            });
            Assert.IsType<IpcMessageCommand>(processor.Invocations[0].Command);
            Assert.IsType<IpcTaskCommand>(processor.Invocations[1].Command);
            Assert.False(File.Exists(messagePath));
            Assert.False(File.Exists(taskPath));
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
        }
    }

    [Fact]
    public async Task PollOnceAsync_ParsesRegisterGroupAndContainerConfigForMainGroup()
    {
        string projectRoot = CreateTemporaryPath();

        try
        {
            StorageOptions storageOptions = StorageOptions.Create(projectRoot);
            PhysicalFileSystem fileSystem = new();
            string taskPath = Path.Combine(storageOptions.DataDirectory, "ipc", "main", "tasks", "register.json");
            Directory.CreateDirectory(Path.GetDirectoryName(taskPath)!);

            await File.WriteAllTextAsync(
                taskPath,
                """
                {
                  "type": "register_group",
                  "jid": "child@jid",
                  "name": "Child",
                  "folder": "child",
                  "trigger": "@Andy",
                  "requires_trigger": true,
                  "is_main": false,
                  "containerConfig": {
                    "timeout": 1500,
                    "additionalMounts": [
                      {
                        "hostPath": "/workspace/data",
                        "containerPath": "/workspace/extra/data",
                        "readonly": false
                      }
                    ]
                  }
                }
                """);

            RecordingIpcCommandProcessor processor = new();
            FileSystemIpcWatcher watcher = new(
                storageOptions,
                fileSystem,
                new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>
                {
                    [new ChatJid("main@jid")] = new RegisteredGroup("Main", new GroupFolder("main"), "@Andy", DateTimeOffset.UtcNow, isMain: true)
                }),
                processor,
                NullLogger<FileSystemIpcWatcher>.Instance);

            await watcher.PollOnceAsync();

            IpcRegisterGroupCommand command = Assert.IsType<IpcRegisterGroupCommand>(Assert.Single(processor.Invocations).Command);
            Assert.True(processor.Invocations[0].IsMainGroup);
            Assert.NotNull(command.ContainerConfig);
            Assert.Equal(TimeSpan.FromMilliseconds(1500), command.ContainerConfig!.Timeout);
            Assert.Single(command.ContainerConfig.AdditionalMounts);
            Assert.False(command.ContainerConfig.AdditionalMounts[0].IsReadOnly);
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
        }
    }

    [Fact]
    public async Task PollOnceAsync_QuarantinesInvalidFiles()
    {
        string projectRoot = CreateTemporaryPath();

        try
        {
            StorageOptions storageOptions = StorageOptions.Create(projectRoot);
            PhysicalFileSystem fileSystem = new();
            string messagePath = Path.Combine(storageOptions.DataDirectory, "ipc", "team", "messages", "invalid.json");
            Directory.CreateDirectory(Path.GetDirectoryName(messagePath)!);
            await File.WriteAllTextAsync(messagePath, "{ invalid json }");

            FileSystemIpcWatcher watcher = new(
                storageOptions,
                fileSystem,
                new InMemoryGroupRepository(new Dictionary<ChatJid, RegisteredGroup>()),
                new RecordingIpcCommandProcessor(),
                NullLogger<FileSystemIpcWatcher>.Instance);

            await watcher.PollOnceAsync();

            string errorPath = Path.Combine(storageOptions.DataDirectory, "ipc", "errors", "team-invalid.json");
            Assert.False(File.Exists(messagePath));
            Assert.True(File.Exists(errorPath));
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
        }
    }

    private static string CreateTemporaryPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"netclaw-ipc-{Guid.NewGuid():N}");
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

    private sealed class RecordingIpcCommandProcessor : IIpcCommandProcessor
    {
        public List<Invocation> Invocations { get; } = [];

        public Task ProcessAsync(GroupFolder sourceGroup, bool isMainGroup, IpcCommand command, CancellationToken cancellationToken = default)
        {
            Invocations.Add(new Invocation(sourceGroup, isMainGroup, command));
            return Task.CompletedTask;
        }
    }

    private sealed record Invocation(GroupFolder SourceGroup, bool IsMainGroup, IpcCommand Command);

    private sealed class InMemoryGroupRepository : IGroupRepository
    {
        private readonly IReadOnlyDictionary<ChatJid, RegisteredGroup> groups;

        public InMemoryGroupRepository(IReadOnlyDictionary<ChatJid, RegisteredGroup> groups)
        {
            this.groups = groups;
        }

        public Task<IReadOnlyDictionary<ChatJid, RegisteredGroup>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(groups);

        public Task<RegisteredGroup?> GetByJidAsync(ChatJid chatJid, CancellationToken cancellationToken = default)
            => Task.FromResult(groups.TryGetValue(chatJid, out RegisteredGroup? group) ? group : null);

        public Task UpsertAsync(ChatJid chatJid, RegisteredGroup group, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

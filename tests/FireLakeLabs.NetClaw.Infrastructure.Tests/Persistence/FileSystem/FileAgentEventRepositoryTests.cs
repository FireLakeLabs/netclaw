using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.FileSystem;

public sealed class FileAgentEventRepositoryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"netclaw-test-{Guid.NewGuid():N}");
    private FileStoragePaths Paths => new(StorageOptions.Create(_tempDir));
    private Task<FileAgentEventRepository> CreateRepository() => FileAgentEventRepository.CreateAsync(Paths);

    public FileAgentEventRepositoryTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static AgentActivityEvent MakeEvent(string groupFolder, DateTimeOffset? observedAt = null, string? sessionId = null, string? taskId = null) =>
        new(
            id: 0, // will be replaced by repo
            groupFolder: groupFolder,
            chatJid: "chat@jid",
            sessionId: sessionId,
            eventKind: ContainerEventKind.TextDelta,
            content: "Hello",
            toolName: null,
            error: null,
            isScheduledTask: taskId is not null,
            taskId: taskId,
            observedAt: observedAt ?? DateTimeOffset.UtcNow,
            capturedAt: observedAt ?? DateTimeOffset.UtcNow);

    [Fact]
    public async Task Store_AssignsUniqueIds()
    {
        FileAgentEventRepository repo = await CreateRepository();
        await repo.StoreAsync(MakeEvent("main"));
        await repo.StoreAsync(MakeEvent("main"));

        IReadOnlyList<AgentActivityEvent> events = await repo.GetRecentAsync(10);
        Assert.Equal(2, events.Count);
        Assert.NotEqual(events[0].Id, events[1].Id);
    }

    [Fact]
    public async Task StoreBatch_AssignsContiguousIds()
    {
        FileAgentEventRepository repo = await CreateRepository();
        AgentActivityEvent[] batch =
        [
            MakeEvent("main"),
            MakeEvent("main"),
            MakeEvent("main")
        ];

        await repo.StoreBatchAsync(batch);
        IReadOnlyList<AgentActivityEvent> events = await repo.GetRecentAsync(10, groupFolder: "main");

        long[] ids = events.Select(e => e.Id).OrderBy(x => x).ToArray();
        Assert.Equal(3, ids.Length);
        // All IDs are distinct
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public async Task GetRecentAsync_WithGroupFolder_LimitsToThatGroup()
    {
        FileAgentEventRepository repo = await CreateRepository();
        await repo.StoreAsync(MakeEvent("groupA"));
        await repo.StoreAsync(MakeEvent("groupB"));

        IReadOnlyList<AgentActivityEvent> result = await repo.GetRecentAsync(10, groupFolder: "groupA");
        Assert.Single(result);
        Assert.Equal("groupA", result[0].GroupFolder);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsOrderedByObservedAtDescending()
    {
        FileAgentEventRepository repo = await CreateRepository();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await repo.StoreAsync(MakeEvent("main", now.AddMinutes(-2)));
        await repo.StoreAsync(MakeEvent("main", now.AddMinutes(-1)));
        await repo.StoreAsync(MakeEvent("main", now));

        IReadOnlyList<AgentActivityEvent> events = await repo.GetRecentAsync(10);
        Assert.True(events[0].ObservedAt >= events[1].ObservedAt);
    }

    [Fact]
    public async Task GetBySessionAsync_FiltersCorrectly()
    {
        FileAgentEventRepository repo = await CreateRepository();
        await repo.StoreAsync(MakeEvent("main", sessionId: "sess-1"));
        await repo.StoreAsync(MakeEvent("main", sessionId: "sess-2"));
        await repo.StoreAsync(MakeEvent("main", sessionId: "sess-1"));

        IReadOnlyList<AgentActivityEvent> result = await repo.GetBySessionAsync("sess-1");
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Equal("sess-1", e.SessionId));
    }

    [Fact]
    public async Task GetByTaskRunAsync_FiltersCorrectly()
    {
        FileAgentEventRepository repo = await CreateRepository();
        DateTimeOffset runAt = DateTimeOffset.UtcNow;
        await repo.StoreAsync(MakeEvent("main", runAt.AddSeconds(1), taskId: "task-1"));
        await repo.StoreAsync(MakeEvent("main", runAt.AddSeconds(2), taskId: "task-1"));
        await repo.StoreAsync(MakeEvent("main", runAt.AddSeconds(1), taskId: "task-2"));

        IReadOnlyList<AgentActivityEvent> result = await repo.GetByTaskRunAsync("task-1", runAt);
        Assert.Equal(2, result.Count);
        Assert.All(result, e => Assert.Equal("task-1", e.TaskId));
    }

    [Fact]
    public async Task IdsAreUniqueAcrossRestart()
    {
        // First instance
        FileAgentEventRepository repo1 = await CreateRepository();
        await repo1.StoreAsync(MakeEvent("main"));
        await repo1.StoreAsync(MakeEvent("main"));
        IReadOnlyList<AgentActivityEvent> events1 = await repo1.GetRecentAsync(10);
        long maxId1 = events1.Max(e => e.Id);

        // Second instance (simulates restart)
        FileAgentEventRepository repo2 = await CreateRepository();
        await repo2.StoreAsync(MakeEvent("main"));
        IReadOnlyList<AgentActivityEvent> events2 = await repo2.GetRecentAsync(1);
        long newId = events2[0].Id;

        Assert.True(newId > maxId1, $"Expected newId ({newId}) > maxId1 ({maxId1})");
    }

    [Fact]
    public async Task DailyFileRotation_StoresEventsInCorrectFiles()
    {
        FileStoragePaths paths = Paths;
        FileAgentEventRepository repo = await FileAgentEventRepository.CreateAsync(paths);

        DateTimeOffset today = DateTimeOffset.UtcNow;
        DateTimeOffset yesterday = today.AddDays(-1);

        await repo.StoreAsync(MakeEvent("main", yesterday));
        await repo.StoreAsync(MakeEvent("main", today));

        string groupDir = paths.EventsGroupDirectory("main");
        string[] files = Directory.GetFiles(groupDir, "*.jsonl");
        Assert.Equal(2, files.Length);
    }
}

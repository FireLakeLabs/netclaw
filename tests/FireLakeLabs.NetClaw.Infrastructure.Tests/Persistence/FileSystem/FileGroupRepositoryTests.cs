using System.Text.Json;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.FileSystem;

public sealed class FileGroupRepositoryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"netclaw-test-{Guid.NewGuid():N}");
    private FileGroupRepository CreateRepository() => new(new FileStoragePaths(StorageOptions.Create(_tempDir)));

    public FileGroupRepositoryTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static RegisteredGroup MakeGroup(string folder, string trigger = "hello", bool isMain = false) =>
        new("Test Group", new GroupFolder(folder), trigger, DateTimeOffset.UtcNow, null, true, isMain);

    [Fact]
    public async Task GetByJidAsync_ReturnsNull_WhenNotRegistered()
    {
        FileGroupRepository repo = CreateRepository();
        RegisteredGroup? result = await repo.GetByJidAsync(new ChatJid("unknown@jid"));
        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertAndGet_RoundTripsGroup()
    {
        FileGroupRepository repo = CreateRepository();
        ChatJid jid = new("team@jid");
        RegisteredGroup group = MakeGroup("team");

        await repo.UpsertAsync(jid, group);
        RegisteredGroup? result = await repo.GetByJidAsync(jid);

        Assert.NotNull(result);
        Assert.Equal("team", result!.Folder.Value);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllGroups()
    {
        FileGroupRepository repo = CreateRepository();
        await repo.UpsertAsync(new ChatJid("a@jid"), MakeGroup("groupA"));
        await repo.UpsertAsync(new ChatJid("b@jid"), MakeGroup("groupB"));

        IReadOnlyDictionary<ChatJid, RegisteredGroup> all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Upsert_WritesChatGroupsMappingFile()
    {
        FileStoragePaths paths = new(StorageOptions.Create(_tempDir));
        FileGroupRepository repo = new(paths);

        await repo.UpsertAsync(new ChatJid("team@jid"), MakeGroup("team"));

        string chatGroupsJson = await File.ReadAllTextAsync(paths.ChatGroupsFilePath);
        Dictionary<string, string>? mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(chatGroupsJson);
        Assert.NotNull(mapping);
        Assert.Equal("team", mapping!["team@jid"]);
    }

    [Fact]
    public async Task GroupWithDisabledTriggerSentinel_RoundTripsCorrectly()
    {
        FileGroupRepository repo = CreateRepository();
        ChatJid jid = new("main@jid");
        RegisteredGroup group = new("Main", new GroupFolder("main1"), "__disabled__", DateTimeOffset.UtcNow, null, false, true);

        await repo.UpsertAsync(jid, group);
        RegisteredGroup? result = await repo.GetByJidAsync(jid);

        Assert.NotNull(result);
        Assert.Equal("__disabled__", result!.Trigger);
        Assert.True(result.IsMain);
    }

    [Fact]
    public async Task PersistedGroups_SurviveRestart()
    {
        await CreateRepository().UpsertAsync(new ChatJid("t@jid"), MakeGroup("persist1"));

        IReadOnlyDictionary<ChatJid, RegisteredGroup> all = await CreateRepository().GetAllAsync();
        Assert.Single(all);
    }
}

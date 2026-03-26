using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.FileSystem;

public sealed class FileRouterStateRepositoryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"netclaw-test-{Guid.NewGuid():N}");
    private FileRouterStateRepository CreateRepository() => new(new FileStoragePaths(StorageOptions.Create(_tempDir)));

    public FileRouterStateRepositoryTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenKeyNotFound()
    {
        FileRouterStateRepository repo = CreateRepository();
        RouterStateEntry? result = await repo.GetAsync("missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAndGet_RoundTripsValue()
    {
        FileRouterStateRepository repo = CreateRepository();
        await repo.SetAsync(new RouterStateEntry("key1", "value1"));

        RouterStateEntry? result = await repo.GetAsync("key1");
        Assert.NotNull(result);
        Assert.Equal("value1", result!.Value);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntries()
    {
        FileRouterStateRepository repo = CreateRepository();
        await repo.SetAsync(new RouterStateEntry("a", "1"));
        await repo.SetAsync(new RouterStateEntry("b", "2"));

        IReadOnlyList<RouterStateEntry> all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task PersistedValues_SurvivedRestart()
    {
        await CreateRepository().SetAsync(new RouterStateEntry("key", "persisted"));

        // New instance reads from disk
        RouterStateEntry? result = await CreateRepository().GetAsync("key");
        Assert.NotNull(result);
        Assert.Equal("persisted", result!.Value);
    }

    [Fact]
    public async Task Set_OverwritesExistingKey()
    {
        FileRouterStateRepository repo = CreateRepository();
        await repo.SetAsync(new RouterStateEntry("key", "first"));
        await repo.SetAsync(new RouterStateEntry("key", "second"));

        RouterStateEntry? result = await repo.GetAsync("key");
        Assert.Equal("second", result!.Value);
    }
}

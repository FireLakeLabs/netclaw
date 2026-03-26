using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.FileSystem;

public sealed class FileSessionRepositoryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"netclaw-test-{Guid.NewGuid():N}");
    private FileSessionRepository CreateRepository() => new(new FileStoragePaths(StorageOptions.Create(_tempDir)));

    public FileSessionRepositoryTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task GetByGroupFolderAsync_ReturnsNull_WhenFileAbsent()
    {
        FileSessionRepository repo = CreateRepository();
        SessionId? result = await repo.GetByGroupFolderAsync(new GroupFolder("team1"));
        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertAndGet_RoundTripsSessionId()
    {
        FileSessionRepository repo = CreateRepository();
        SessionState state = new(new GroupFolder("team1"), new SessionId("sess-abc"));

        await repo.UpsertAsync(state);
        SessionId? result = await repo.GetByGroupFolderAsync(new GroupFolder("team1"));

        Assert.NotNull(result);
        Assert.Equal("sess-abc", result!.Value.Value);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllGroups()
    {
        FileSessionRepository repo = CreateRepository();
        await repo.UpsertAsync(new SessionState(new GroupFolder("groupA"), new SessionId("sess-1")));
        await repo.UpsertAsync(new SessionState(new GroupFolder("groupB"), new SessionId("sess-2")));

        IReadOnlyDictionary<GroupFolder, SessionId> all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal("sess-1", all[new GroupFolder("groupA")].Value);
    }

    [Fact]
    public async Task Upsert_OverwritesExistingSession()
    {
        FileSessionRepository repo = CreateRepository();
        await repo.UpsertAsync(new SessionState(new GroupFolder("team1"), new SessionId("old-sess")));
        await repo.UpsertAsync(new SessionState(new GroupFolder("team1"), new SessionId("new-sess")));

        SessionId? result = await repo.GetByGroupFolderAsync(new GroupFolder("team1"));
        Assert.Equal("new-sess", result!.Value.Value);
    }
}

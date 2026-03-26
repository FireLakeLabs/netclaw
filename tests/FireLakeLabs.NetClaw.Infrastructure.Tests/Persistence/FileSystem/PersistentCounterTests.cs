using FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.FileSystem;

public sealed class PersistentCounterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"netclaw-test-{Guid.NewGuid():N}");

    public PersistentCounterTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task Next_ReturnsMonotonicallyIncreasingIds()
    {
        string counterPath = Path.Combine(_tempDir, "next-id.txt");
        PersistentCounter counter = await PersistentCounter.InitializeAsync(counterPath, () => Task.FromResult(0L));

        long id1 = counter.Next();
        long id2 = counter.Next();
        long id3 = counter.Next();

        Assert.True(id1 < id2);
        Assert.True(id2 < id3);
    }

    [Fact]
    public async Task Initialize_CallsComputeMaxExisting_WhenFileAbsent()
    {
        string counterPath = Path.Combine(_tempDir, "next-id.txt");
        bool called = false;

        PersistentCounter counter = await PersistentCounter.InitializeAsync(
            counterPath,
            () => { called = true; return Task.FromResult(42L); });

        Assert.True(called);
        long id = counter.Next();
        Assert.Equal(43L, id);
    }

    [Fact]
    public async Task Initialize_ReadsExistingFile_WithoutCallingCompute()
    {
        string counterPath = Path.Combine(_tempDir, "next-id.txt");
        await File.WriteAllTextAsync(counterPath, "100");

        bool called = false;
        PersistentCounter counter = await PersistentCounter.InitializeAsync(
            counterPath,
            () => { called = true; return Task.FromResult(0L); });

        Assert.False(called);
        long id = counter.Next();
        Assert.Equal(101L, id);
    }

    [Fact]
    public async Task FlushAsync_PersistsCurrentValue()
    {
        string counterPath = Path.Combine(_tempDir, "next-id.txt");
        PersistentCounter counter = await PersistentCounter.InitializeAsync(counterPath, () => Task.FromResult(0L));

        counter.Next(); // 1
        counter.Next(); // 2
        counter.Next(); // 3
        await counter.FlushAsync();

        string content = await File.ReadAllTextAsync(counterPath);
        Assert.Equal("3", content.Trim());
    }

    [Fact]
    public async Task IdsAreUniqueAcrossRestart()
    {
        string counterPath = Path.Combine(_tempDir, "next-id.txt");

        // First instance
        PersistentCounter counter1 = await PersistentCounter.InitializeAsync(counterPath, () => Task.FromResult(0L));
        long id1 = counter1.Next(); // 1
        long id2 = counter1.Next(); // 2
        await counter1.FlushAsync();

        // Second instance (simulates restart)
        PersistentCounter counter2 = await PersistentCounter.InitializeAsync(counterPath, () => Task.FromResult(0L));
        long id3 = counter2.Next();

        Assert.True(id3 > id2, $"Expected id3 ({id3}) > id2 ({id2}) after restart");
    }
}

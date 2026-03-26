using System.Text.Json;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.FileSystem;

public sealed class FileAtomicWriterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"netclaw-test-{Guid.NewGuid():N}");
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public FileAtomicWriterTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task WriteJsonAsync_WritesFileAndCleansTempFile()
    {
        string path = Path.Combine(_tempDir, "test.json");
        await FileAtomicWriter.WriteJsonAsync(path, new { name = "hello" }, Options);

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));

        string content = await File.ReadAllTextAsync(path);
        Assert.Contains("hello", content);
    }

    [Fact]
    public async Task WriteJsonAsync_OverwritesExistingFile()
    {
        string path = Path.Combine(_tempDir, "overwrite.json");
        await FileAtomicWriter.WriteJsonAsync(path, new { value = "first" }, Options);
        await FileAtomicWriter.WriteJsonAsync(path, new { value = "second" }, Options);

        string content = await File.ReadAllTextAsync(path);
        Assert.Contains("second", content);
        Assert.DoesNotContain("first", content);
    }

    [Fact]
    public async Task WriteTextAsync_WritesFileAndCleansTempFile()
    {
        string path = Path.Combine(_tempDir, "counter.txt");
        await FileAtomicWriter.WriteTextAsync(path, "42");

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal("42", await File.ReadAllTextAsync(path));
    }
}

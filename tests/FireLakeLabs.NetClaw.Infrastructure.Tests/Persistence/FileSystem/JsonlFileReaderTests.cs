using System.Text.Json;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.FileSystem;

public sealed class JsonlFileReaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"netclaw-test-{Guid.NewGuid():N}");
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private sealed record Item(string Id, string Name);

    public JsonlFileReaderTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public async Task ReadAllAsync_ReturnsEmptyList_WhenFileDoesNotExist()
    {
        IReadOnlyList<Item> result = await JsonlFileReader.ReadAllAsync<Item>(
            Path.Combine(_tempDir, "missing.jsonl"), Options);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ReadAllAsync_ParsesAllValidLines()
    {
        string path = Path.Combine(_tempDir, "items.jsonl");
        await File.WriteAllTextAsync(path,
            """
            {"id":"1","name":"Alice"}
            {"id":"2","name":"Bob"}
            """);

        IReadOnlyList<Item> result = await JsonlFileReader.ReadAllAsync<Item>(path, Options);

        Assert.Equal(2, result.Count);
        Assert.Equal("Alice", result[0].Name);
        Assert.Equal("Bob", result[1].Name);
    }

    [Fact]
    public async Task ReadAllAsync_SkipsPartialLastLine()
    {
        string path = Path.Combine(_tempDir, "partial.jsonl");
        await File.WriteAllTextAsync(path,
            """
            {"id":"1","name":"Alice"}
            {"id":"2","name":"Bo
            """);

        IReadOnlyList<Item> result = await JsonlFileReader.ReadAllAsync<Item>(path, Options);

        Assert.Single(result);
        Assert.Equal("Alice", result[0].Name);
    }

    [Fact]
    public async Task ReadAllAsync_SkipsEmptyLines()
    {
        string path = Path.Combine(_tempDir, "withempty.jsonl");
        await File.WriteAllTextAsync(path, "{\"id\":\"1\",\"name\":\"Alice\"}\n\n{\"id\":\"2\",\"name\":\"Bob\"}\n");

        IReadOnlyList<Item> result = await JsonlFileReader.ReadAllAsync<Item>(path, Options);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ReadFilteredAsync_AppliesPredicate()
    {
        string path = Path.Combine(_tempDir, "filter.jsonl");
        await File.WriteAllTextAsync(path,
            """
            {"id":"1","name":"Alice"}
            {"id":"2","name":"Bob"}
            """);

        IReadOnlyList<Item> result = await JsonlFileReader.ReadFilteredAsync<Item>(
            path, item => item.Name == "Alice", Options);

        Assert.Single(result);
        Assert.Equal("Alice", result[0].Name);
    }

    [Fact]
    public async Task ReadTailLinesAsync_ReturnsLastNLines()
    {
        string path = Path.Combine(_tempDir, "tail.jsonl");
        string[] lines = Enumerable.Range(1, 10)
            .Select(i => $"{{\"id\":\"{i}\",\"name\":\"item{i}\"}}")
            .ToArray();
        await File.WriteAllLinesAsync(path, lines);

        string[] tail = await JsonlFileReader.ReadTailLinesAsync(path, 3);

        Assert.Equal(3, tail.Length);
        Assert.Contains("item10", tail[^1]);
    }

    [Fact]
    public async Task ReadTailLinesAsync_ReturnsEmptyArray_WhenFileDoesNotExist()
    {
        string[] result = await JsonlFileReader.ReadTailLinesAsync(
            Path.Combine(_tempDir, "missing.jsonl"), 5);

        Assert.Empty(result);
    }
}

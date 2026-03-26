using System.Text.Json;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// Appends JSON lines to a JSONL file.
/// Callers are responsible for acquiring any necessary per-resource lock before calling.
/// </summary>
internal static class JsonlFileAppender
{
    /// <summary>
    /// Appends a single JSON line to <paramref name="path"/>.
    /// Creates the file if it does not exist. The target directory must already exist.
    /// </summary>
    public static async Task AppendAsync<T>(string path, T value, JsonSerializerOptions options, CancellationToken cancellationToken = default)
    {
        string line = JsonSerializer.Serialize(value, options) + "\n";
        await File.AppendAllTextAsync(path, line, cancellationToken);
    }

    /// <summary>
    /// Appends multiple JSON lines to <paramref name="path"/> in a single write.
    /// </summary>
    public static async Task AppendManyAsync<T>(string path, IEnumerable<T> values, JsonSerializerOptions options, CancellationToken cancellationToken = default)
    {
        string lines = string.Concat(values.Select(v => JsonSerializer.Serialize(v, options) + "\n"));
        if (lines.Length > 0)
        {
            await File.AppendAllTextAsync(path, lines, cancellationToken);
        }
    }
}

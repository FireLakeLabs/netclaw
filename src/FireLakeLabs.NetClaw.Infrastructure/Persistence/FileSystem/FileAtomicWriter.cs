using System.Text.Json;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// Provides atomic file write operations via temp-file + rename.
/// Small files (JSON config, metadata) are always written atomically.
/// The rename is atomic on Linux/macOS (POSIX rename(2)) and best-effort on Windows.
/// </summary>
internal static class FileAtomicWriter
{
    /// <summary>
    /// Serializes <paramref name="value"/> to a temp file, then renames it to <paramref name="path"/>.
    /// The target directory must already exist.
    /// </summary>
    public static async Task WriteJsonAsync<T>(string path, T value, JsonSerializerOptions options, CancellationToken cancellationToken = default)
    {
        string tempPath = path + ".tmp";
        string json = JsonSerializer.Serialize(value, options);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>
    /// Writes <paramref name="content"/> to a temp file, then renames it to <paramref name="path"/>.
    /// </summary>
    public static async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        string tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, cancellationToken);
        File.Move(tempPath, path, overwrite: true);
    }
}

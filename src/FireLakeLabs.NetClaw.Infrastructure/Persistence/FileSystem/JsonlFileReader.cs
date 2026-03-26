using System.Text.Json;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// Reads and deserializes JSONL (JSON Lines) files.
/// Tolerates partial last lines — any line that fails JSON deserialization is silently skipped.
/// </summary>
internal static class JsonlFileReader
{
    /// <summary>
    /// Reads all valid lines from <paramref name="path"/> and deserializes each as <typeparamref name="T"/>.
    /// Returns an empty list if the file does not exist. Skips empty lines and lines with invalid JSON.
    /// </summary>
    public static async Task<IReadOnlyList<T>> ReadAllAsync<T>(string path, JsonSerializerOptions options, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        string[] lines = await File.ReadAllLinesAsync(path, cancellationToken);
        List<T> results = new(lines.Length);

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                T? item = JsonSerializer.Deserialize<T>(line, options);
                if (item is not null)
                {
                    results.Add(item);
                }
            }
            catch (JsonException)
            {
                // Tolerate partial last line (crash during append) or corrupted line.
            }
        }

        return results;
    }

    /// <summary>
    /// Reads and filters lines from <paramref name="path"/> using <paramref name="predicate"/>.
    /// </summary>
    public static async Task<IReadOnlyList<T>> ReadFilteredAsync<T>(string path, Func<T, bool> predicate, JsonSerializerOptions options, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<T> all = await ReadAllAsync<T>(path, options, cancellationToken);
        return all.Where(predicate).ToList();
    }

    /// <summary>
    /// Returns the last <paramref name="n"/> non-empty lines of the file, without deserializing.
    /// Used to seed dedup caches and to implement reverse pagination on run logs.
    /// Returns an empty array if the file does not exist.
    /// </summary>
    public static async Task<string[]> ReadTailLinesAsync(string path, int n, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        string[] allLines = await File.ReadAllLinesAsync(path, cancellationToken);
        return allLines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .TakeLast(n)
            .ToArray();
    }
}

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
    /// Uses a backwards seek so only the tail portion of the file is read — O(size of last N lines).
    /// Used to seed dedup caches and to implement reverse pagination on run logs.
    /// Returns an empty array if the file does not exist.
    /// </summary>
    public static async Task<string[]> ReadTailLinesAsync(string path, int n, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path) || n <= 0)
        {
            return [];
        }

        const int bufferSize = 4096;

        using FileStream fs = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        long fileLength = fs.Length;
        if (fileLength == 0)
        {
            return [];
        }


        // Scan backwards to find the start of the last n lines.
        // Each JSONL line ends with \n; to include n lines we must skip n+1 newlines from the end.
        byte[] buffer = new byte[bufferSize];
        long position = fileLength;
        long startPosition = 0;
        int newLineCount = 0;
        bool foundStart = false;

        while (position > 0 && !foundStart)
        {
            int bytesToRead = (int)Math.Min(bufferSize, position);
            position -= bytesToRead;
            fs.Seek(position, SeekOrigin.Begin);

            int bytesRead = await fs.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            for (int i = bytesRead - 1; i >= 0; i--)
            {
                if (buffer[i] == (byte)'\n')
                {
                    newLineCount++;
                    if (newLineCount > n)
                    {
                        // Position right after this newline is where the last n lines begin.
                        startPosition = position + i + 1;
                        foundStart = true;
                        break;
                    }
                }
            }
        }


        // Read forward from the calculated start position and collect the tail lines.
        fs.Seek(startPosition, SeekOrigin.Begin);
        using StreamReader reader = new(fs, leaveOpen: true);

        Queue<string> tailLines = new(n);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            tailLines.Enqueue(line);
            if (tailLines.Count > n)
            {
                tailLines.Dequeue();
            }
        }

        return tailLines.ToArray();
    }
}

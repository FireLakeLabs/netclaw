namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// A monotonic <see cref="long"/> counter backed by a file.
/// IDs are allocated via <see cref="Interlocked.Increment"/> for thread safety.
/// The high-water mark is periodically flushed to disk so IDs remain unique across restarts.
/// </summary>
internal sealed class PersistentCounter
{
    private const int FlushInterval = 100;

    private readonly string _counterFilePath;
    private long _current;
    private int _sinceLastFlush;

    private PersistentCounter(string counterFilePath, long initialValue)
    {
        _counterFilePath = counterFilePath;
        _current = initialValue;
    }

    /// <summary>
    /// Initializes a <see cref="PersistentCounter"/> from the file at <paramref name="counterFilePath"/>.
    /// If the file does not exist, calls <paramref name="computeMaxExisting"/> to determine the starting value.
    /// </summary>
    public static async Task<PersistentCounter> InitializeAsync(
        string counterFilePath,
        Func<Task<long>> computeMaxExisting,
        CancellationToken cancellationToken = default)
    {
        long initialValue;

        if (File.Exists(counterFilePath))
        {
            string text = await File.ReadAllTextAsync(counterFilePath, cancellationToken);
            initialValue = long.TryParse(text.Trim(), out long parsed) ? parsed : 0L;
        }
        else
        {
            long maxExisting = await computeMaxExisting();
            initialValue = maxExisting;
            string? dir = Path.GetDirectoryName(counterFilePath);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }
            await FileAtomicWriter.WriteTextAsync(counterFilePath, initialValue.ToString(), cancellationToken);
        }

        return new PersistentCounter(counterFilePath, initialValue);
    }

    /// <summary>
    /// Allocates the next ID. Thread-safe via <see cref="Interlocked.Increment"/>.
    /// Periodically flushes the high-water mark to disk.
    /// </summary>
    public long Next()
    {
        long id = Interlocked.Increment(ref _current);

        int flushCount = Interlocked.Increment(ref _sinceLastFlush);
        if (flushCount >= FlushInterval)
        {
            Interlocked.Exchange(ref _sinceLastFlush, 0);
            _ = FlushAsync();
        }

        return id;
    }

    /// <summary>
    /// Explicitly flushes the current high-water mark to disk.
    /// </summary>
    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        long current = Interlocked.Read(ref _current);
        return FileAtomicWriter.WriteTextAsync(_counterFilePath, current.ToString(), cancellationToken);
    }
}

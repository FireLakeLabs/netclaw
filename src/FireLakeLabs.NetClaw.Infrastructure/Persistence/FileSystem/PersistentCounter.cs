namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;

/// <summary>
/// A monotonic <see cref="long"/> counter backed by a file.
/// IDs are allocated via <see cref="Interlocked.Increment"/> for thread safety.
/// The high-water mark is periodically flushed to disk so IDs remain unique across restarts.
/// </summary>
internal sealed class PersistentCounter
{
    private readonly string _counterFilePath;
    private long _current;

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
    /// Callers are responsible for calling <see cref="FlushAsync"/> at safe checkpoints.
    /// </summary>
    public long Next()
    {
        return Interlocked.Increment(ref _current);
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

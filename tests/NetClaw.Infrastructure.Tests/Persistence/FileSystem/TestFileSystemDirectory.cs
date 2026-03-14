namespace NetClaw.Infrastructure.Tests.Persistence.FileSystem;

internal sealed class TestFileSystemDirectory : IDisposable
{
    public TestFileSystemDirectory()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), $"netclaw-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(DataDirectory);
    }

    public string DataDirectory { get; }

    public void Dispose()
    {
        if (Directory.Exists(DataDirectory))
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
    }
}

namespace NetClaw.Infrastructure.FileSystem;

public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    void CreateDirectory(string path);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);

    string[] GetDirectories(string path);

    string[] GetFiles(string path, string searchPattern);

    string GetFullPath(string path);

    string GetTempPath();
}
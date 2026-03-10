using NetClaw.Infrastructure.FileSystem;

namespace NetClaw.Infrastructure.Tests.FileSystem;

public sealed class PhysicalFileSystemTests
{
    [Fact]
    public async Task WriteAndReadAllText_RoundTripsContent()
    {
        PhysicalFileSystem fileSystem = new();
        string directory = Path.Combine(fileSystem.GetTempPath(), $"netclaw-fs-{Guid.NewGuid():N}");
        string filePath = Path.Combine(directory, "test.txt");

        try
        {
            fileSystem.CreateDirectory(directory);
            await fileSystem.WriteAllTextAsync(filePath, "hello world");
            string content = await fileSystem.ReadAllTextAsync(filePath);

            Assert.True(fileSystem.FileExists(filePath));
            Assert.Equal("hello world", content);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void MoveAndDeleteFile_UpdatesFileLocation()
    {
        PhysicalFileSystem fileSystem = new();
        string directory = Path.Combine(fileSystem.GetTempPath(), $"netclaw-fs-{Guid.NewGuid():N}");
        string sourcePath = Path.Combine(directory, "source.txt");
        string destinationPath = Path.Combine(directory, "destination.txt");

        try
        {
            fileSystem.CreateDirectory(directory);
            File.WriteAllText(sourcePath, "payload");

            fileSystem.MoveFile(sourcePath, destinationPath);

            Assert.False(fileSystem.FileExists(sourcePath));
            Assert.True(fileSystem.FileExists(destinationPath));

            fileSystem.DeleteFile(destinationPath);

            Assert.False(fileSystem.FileExists(destinationPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
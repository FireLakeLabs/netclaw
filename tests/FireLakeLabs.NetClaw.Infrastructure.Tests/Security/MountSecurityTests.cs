using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Infrastructure.FileSystem;
using FireLakeLabs.NetClaw.Infrastructure.Security;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Security;

public sealed class MountSecurityTests
{
    [Fact]
    public async Task MountAllowlistLoader_ReturnsDefaultAllowlistWhenFileIsMissing()
    {
        MountAllowlistLoader loader = new(new PhysicalFileSystem());
        MountAllowlist allowlist = await loader.LoadAsync(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json"));

        Assert.Empty(allowlist.AllowedRoots);
        Assert.True(allowlist.NonMainReadOnly);
    }

    [Fact]
    public async Task MountAllowlistLoader_ParsesJsonAllowlist()
    {
        PhysicalFileSystem fileSystem = new();
        MountAllowlistLoader loader = new(fileSystem);
        string directory = Path.Combine(fileSystem.GetTempPath(), $"netclaw-allowlist-{Guid.NewGuid():N}");
        string filePath = Path.Combine(directory, "allowlist.json");

        try
        {
            fileSystem.CreateDirectory(directory);
            await fileSystem.WriteAllTextAsync(filePath, """
            {
              "allowedRoots": [{ "path": "/tmp/projects", "allowReadWrite": true, "description": "Projects" }],
              "blockedPatterns": [".ssh"],
              "nonMainReadOnly": true
            }
            """);

            MountAllowlist allowlist = await loader.LoadAsync(filePath);

            Assert.Single(allowlist.AllowedRoots);
            Assert.Single(allowlist.BlockedPatterns);
            Assert.True(allowlist.NonMainReadOnly);
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
    public void MountSecurityValidator_RejectsPathsOutsideAllowlist()
    {
        MountSecurityValidator validator = new();
        MountAllowlist allowlist = new([new AllowedRoot("/tmp/projects", allowReadWrite: true)], [], nonMainReadOnly: true);

        Assert.Throws<InvalidOperationException>(() => validator.Validate([new AdditionalMount("/etc")], allowlist, isMainGroup: true));
    }

    [Fact]
    public void MountSecurityValidator_ForcesReadOnlyForNonMainGroups()
    {
        MountSecurityValidator validator = new();
        MountAllowlist allowlist = new([new AllowedRoot("/tmp/projects", allowReadWrite: true)], [], nonMainReadOnly: true);

        IReadOnlyList<AdditionalMount> validated = validator.Validate([new AdditionalMount("/tmp/projects/repo", isReadOnly: false)], allowlist, isMainGroup: false);

        Assert.Single(validated);
        Assert.True(validated[0].IsReadOnly);
    }
}

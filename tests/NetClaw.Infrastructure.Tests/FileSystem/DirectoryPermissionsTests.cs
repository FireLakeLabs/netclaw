using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NetClaw.Infrastructure.FileSystem;

namespace NetClaw.Infrastructure.Tests.FileSystem;

/// <summary>
/// Skips the test on platforms where <see cref="DirectoryPermissions"/> is a no-op
/// (i.e. anything other than Linux and macOS).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
file sealed class UnixOnlyFactAttribute : FactAttribute
{
    public UnixOnlyFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Skip = "Requires Linux or macOS (matches DirectoryPermissions runtime guard).";
        }
    }
}

/// <summary>
/// Creates a temporary directory and deletes it on disposal.
/// </summary>
file sealed class TempDirectory : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"netclaw-perms-{Guid.NewGuid():N}");

    public TempDirectory() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

public sealed class DirectoryPermissionsTests
{
    private static readonly UnixFileMode GroupAndOther =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    private static readonly UnixFileMode OpenPermissions =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    [UnixOnlyFact]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("osx")]
    public void RestrictToOwner_ClearsGroupAndOtherBits()
    {
        using TempDirectory tmp = new();

        // Start wider than 700 so RestrictToOwner has something to narrow.
        File.SetUnixFileMode(tmp.Path, OpenPermissions);

        DirectoryPermissions.RestrictToOwner(tmp.Path);

        UnixFileMode mode = File.GetUnixFileMode(tmp.Path);
        Assert.Equal(UnixFileMode.None, mode & GroupAndOther);
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            mode & (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute));
    }

    [UnixOnlyFact]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("osx")]
    public void VerifyOwnerOnly_ReturnsFalse_WhenGroupOrOtherBitsAreSet()
    {
        using TempDirectory tmp = new();
        File.SetUnixFileMode(tmp.Path, OpenPermissions);

        bool result = DirectoryPermissions.VerifyOwnerOnly(tmp.Path);

        Assert.False(result);
    }

    [UnixOnlyFact]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("osx")]
    public void VerifyOwnerOnly_ReturnsTrue_WhenPermissionsAreOwnerOnly()
    {
        using TempDirectory tmp = new();
        File.SetUnixFileMode(tmp.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        bool result = DirectoryPermissions.VerifyOwnerOnly(tmp.Path);

        Assert.True(result);
    }
}

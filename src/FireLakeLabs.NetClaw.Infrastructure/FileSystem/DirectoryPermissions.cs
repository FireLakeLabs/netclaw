using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace FireLakeLabs.NetClaw.Infrastructure.FileSystem;

/// <summary>
/// Sets restrictive Unix-style file permissions on sensitive directories during host initialization on Linux and macOS.
/// On other platforms this is a no-op.
/// </summary>
public static class DirectoryPermissions
{
    /// <summary>
    /// Sets owner-only permissions (700) on the given directory if running on Linux or macOS.
    /// </summary>
    public static void RestrictToOwner(string directoryPath, ILogger? logger = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        if (IsSymlink(directoryPath))
        {
            logger?.LogWarning(
                "Refusing to change permissions on symbolic link {DirectoryPath}.",
                directoryPath);
            return;
        }

        try
        {
            File.SetUnixFileMode(
                directoryPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            logger?.LogDebug("Set owner-only permissions (700) on {DirectoryPath}.", directoryPath);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Failed to set restrictive permissions on {DirectoryPath}.", directoryPath);
        }
    }

    private static bool IsSymlink(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            // If attributes cannot be read, treat as not a symlink and let callers handle errors.
            return false;
        }
    }

    /// <summary>
    /// Verifies that a directory has no group or other permissions set on Linux or macOS.
    /// Returns true if permissions are acceptable, false if they are too open.
    /// On other platforms, always returns true.
    /// </summary>
    public static bool VerifyOwnerOnly(string directoryPath, ILogger? logger = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return true;
        }

        if (!Directory.Exists(directoryPath))
        {
            return true;
        }

        if (IsSymlink(directoryPath))
        {
            logger?.LogWarning(
                "Directory {DirectoryPath} is a symbolic link; skipping permission verification.",
                directoryPath);
            return true;
        }

        try
        {
            UnixFileMode mode = File.GetUnixFileMode(directoryPath);
            UnixFileMode groupAndOther =
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

            if ((mode & groupAndOther) != 0)
            {
                logger?.LogWarning(
                    "Directory {DirectoryPath} has permissions {Permissions} which are more open than expected. Expected owner-only (700).",
                    directoryPath,
                    mode);
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Could not verify permissions on {DirectoryPath}.", directoryPath);
            return true;
        }
    }
}

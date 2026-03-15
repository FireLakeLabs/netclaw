using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace NetClaw.Infrastructure.FileSystem;

/// <summary>
/// Sets restrictive Unix file permissions on sensitive directories during host initialization.
/// On non-Unix platforms this is a no-op.
/// </summary>
public static class DirectoryPermissions
{
    /// <summary>
    /// Sets owner-only permissions (700) on the given directory if running on a Unix-like OS.
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

    /// <summary>
    /// Verifies that a directory has no group or other permissions set.
    /// Returns true if permissions are acceptable, false if they are too open.
    /// On non-Unix platforms, always returns true.
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

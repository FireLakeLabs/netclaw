namespace NetClaw.Infrastructure.Runtime;

public sealed class PlatformDetectionService
{
    public PlatformInfo Detect(string osName, bool isWsl, bool hasSystemd, bool isRoot, string homeDirectory)
    {
        PlatformKind kind = osName.ToLowerInvariant() switch
        {
            "linux" => PlatformKind.Linux,
            "macos" => PlatformKind.MacOs,
            "windows" => PlatformKind.Windows,
            _ => PlatformKind.Unknown
        };

        return new PlatformInfo(kind, isWsl, hasSystemd, isRoot, homeDirectory);
    }

    public PlatformInfo DetectCurrent()
    {
        string osName = OperatingSystem.IsLinux()
            ? "linux"
            : OperatingSystem.IsMacOS()
                ? "macos"
                : OperatingSystem.IsWindows()
                    ? "windows"
                    : "unknown";

        bool isWsl = File.Exists("/proc/sys/fs/binfmt_misc/WSLInterop");
        bool hasSystemd = File.Exists("/run/systemd/system");
        bool isRoot = Environment.UserName.Equals("root", StringComparison.Ordinal);
        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return Detect(osName, isWsl, hasSystemd, isRoot, homeDirectory);
    }
}

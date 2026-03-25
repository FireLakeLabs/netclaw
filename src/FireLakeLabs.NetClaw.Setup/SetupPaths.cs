using FireLakeLabs.NetClaw.Infrastructure.Configuration;

namespace FireLakeLabs.NetClaw.Setup;

public sealed record SetupPaths
{
    public required string ProjectRoot { get; init; }

    public required string GroupsDirectory { get; init; }

    public required string DataDirectory { get; init; }

    public required string StoreDirectory { get; init; }

    public required string DatabasePath { get; init; }

    public required string LogsDirectory { get; init; }

    public required string HomeDirectory { get; init; }

    public required string UserConfigDirectory { get; init; }

    public required string MountAllowlistPath { get; init; }

    public required string UserServicePath { get; init; }

    public required string SystemServicePath { get; init; }

    public required string LauncherScriptPath { get; init; }

    public required string PidFilePath { get; init; }

    public required string EnvironmentFilePath { get; init; }

    public required string AppSettingsPath { get; init; }

    public static string DefaultProjectRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".netclaw");

    public static SetupPaths Create(string? projectRootOverride = null, string? homeDirectoryOverride = null)
    {
        string projectRoot = Path.GetFullPath(projectRootOverride ?? DefaultProjectRoot);
        string homeDirectory = Path.GetFullPath(homeDirectoryOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        StorageOptions storageOptions = StorageOptions.Create(projectRoot);

        return new SetupPaths
        {
            ProjectRoot = projectRoot,
            GroupsDirectory = storageOptions.GroupsDirectory,
            DataDirectory = storageOptions.DataDirectory,
            StoreDirectory = storageOptions.StoreDirectory,
            DatabasePath = Path.Combine(storageOptions.DataDirectory, "netclaw.db"),
            LogsDirectory = Path.Combine(projectRoot, "logs"),
            HomeDirectory = homeDirectory,
            UserConfigDirectory = projectRoot,
            MountAllowlistPath = Path.Combine(projectRoot, "mount-allowlist.json"),
            UserServicePath = Path.Combine(homeDirectory, ".config", "systemd", "user", "netclaw.service"),
            SystemServicePath = "/etc/systemd/system/netclaw.service",
            LauncherScriptPath = Path.Combine(projectRoot, "start-netclaw.sh"),
            PidFilePath = Path.Combine(projectRoot, "netclaw.pid"),
            EnvironmentFilePath = Path.Combine(projectRoot, ".env"),
            AppSettingsPath = Path.Combine(projectRoot, "appsettings.json")
        };
    }
}

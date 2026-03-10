using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Setup;

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

    public static SetupPaths Create(string? projectRootOverride = null, string? homeDirectoryOverride = null)
    {
        string projectRoot = Path.GetFullPath(projectRootOverride ?? Directory.GetCurrentDirectory());
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
            UserConfigDirectory = Path.Combine(homeDirectory, ".config", "netclaw"),
            MountAllowlistPath = Path.Combine(homeDirectory, ".config", "netclaw", "mount-allowlist.json"),
            UserServicePath = Path.Combine(homeDirectory, ".config", "systemd", "user", "netclaw.service"),
            SystemServicePath = "/etc/systemd/system/netclaw.service",
            LauncherScriptPath = Path.Combine(projectRoot, "start-netclaw.sh"),
            PidFilePath = Path.Combine(projectRoot, "netclaw.pid"),
            EnvironmentFilePath = Path.Combine(projectRoot, ".env")
        };
    }
}
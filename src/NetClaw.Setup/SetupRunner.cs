using System.Text;
using System.Text.Json;
using NetClaw.Domain.Entities;
using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.FileSystem;
using NetClaw.Infrastructure.Persistence.Sqlite;
using NetClaw.Infrastructure.Runtime;

namespace NetClaw.Setup;

public sealed class SetupRunner
{
    private readonly ICommandRunner commandRunner;
    private readonly IFileSystem fileSystem;
    private readonly PlatformDetectionService platformDetectionService;
    private readonly SetupPaths paths;

    public SetupRunner(SetupPaths paths, IFileSystem fileSystem, ICommandRunner commandRunner, PlatformDetectionService platformDetectionService)
    {
        this.paths = paths;
        this.fileSystem = fileSystem;
        this.commandRunner = commandRunner;
        this.platformDetectionService = platformDetectionService;
    }

    public static SetupRunner CreateDefault()
    {
        string? projectRoot = Environment.GetEnvironmentVariable("NETCLAW_PROJECT_ROOT");
        string? homeDirectory = Environment.GetEnvironmentVariable("HOME");
        return new SetupRunner(SetupPaths.Create(projectRoot, homeDirectory), new PhysicalFileSystem(), new ProcessCommandRunner(), new PlatformDetectionService());
    }

    public async Task<SetupResult> RunAsync(SetupCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Step))
        {
            return CreateFailure("usage", "ERROR", "A --step argument is required.");
        }

        return command.Step.ToLowerInvariant() switch
        {
            "environment" => await RunEnvironmentAsync(cancellationToken),
            "register" => await RunRegisterAsync(command, cancellationToken),
            "mounts" => await RunMountsAsync(command, cancellationToken),
            "service" => await RunServiceAsync(command, cancellationToken),
            "verify" => await RunVerifyAsync(cancellationToken),
            _ => CreateFailure(command.Step, "ERROR", $"Unsupported step '{command.Step}'.")
        };
    }

    private async Task<SetupResult> RunEnvironmentAsync(CancellationToken cancellationToken)
    {
        PlatformInfo platform = platformDetectionService.DetectCurrent();
        bool dockerAvailable = await IsCommandAvailableAsync("docker", cancellationToken);

        Dictionary<string, string> status = new(StringComparer.Ordinal)
        {
            ["PLATFORM"] = platform.Kind.ToString(),
            ["IS_WSL"] = platform.IsWsl.ToString().ToLowerInvariant(),
            ["HAS_SYSTEMD"] = platform.HasSystemd.ToString().ToLowerInvariant(),
            ["IS_ROOT"] = platform.IsRoot.ToString().ToLowerInvariant(),
            ["HOME"] = platform.HomeDirectory,
            ["PROJECT_ROOT"] = paths.ProjectRoot,
            ["DATABASE_EXISTS"] = fileSystem.FileExists(paths.DatabasePath).ToString().ToLowerInvariant(),
            ["ALLOWLIST_EXISTS"] = fileSystem.FileExists(paths.MountAllowlistPath).ToString().ToLowerInvariant(),
            ["DOCKER_AVAILABLE"] = dockerAvailable.ToString().ToLowerInvariant()
        };

        return new SetupResult("environment", 0, status);
    }

    private async Task<SetupResult> RunRegisterAsync(SetupCommand command, CancellationToken cancellationToken)
    {
        string? jid = command.GetOption("jid");
        string? name = command.GetOption("name");
        string? trigger = command.GetOption("trigger");
        string? folder = command.GetOption("folder");

        if (string.IsNullOrWhiteSpace(jid) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(trigger) || string.IsNullOrWhiteSpace(folder))
        {
            return CreateFailure("register", "ERROR", "The register step requires --jid, --name, --trigger, and --folder.");
        }

        await EnsureSchemaAsync(cancellationToken);

        string groupDirectory = Path.Combine(paths.GroupsDirectory, folder);
        string logsDirectory = Path.Combine(groupDirectory, "logs");
        fileSystem.CreateDirectory(paths.GroupsDirectory);
        fileSystem.CreateDirectory(groupDirectory);
        fileSystem.CreateDirectory(logsDirectory);

        SqliteGroupRepository repository = CreateGroupRepository();
        RegisteredGroup group = new(
            name,
            new GroupFolder(folder),
            trigger,
            DateTimeOffset.UtcNow,
            containerConfig: null,
            requiresTrigger: !command.HasFlag("no-trigger-required"),
            isMain: command.HasFlag("is-main"));

        await repository.UpsertAsync(new ChatJid(jid), group, cancellationToken);

        Dictionary<string, string> status = new(StringComparer.Ordinal)
        {
            ["STATUS"] = "registered",
            ["JID"] = jid,
            ["NAME"] = name,
            ["FOLDER"] = folder,
            ["GROUP_DIRECTORY"] = groupDirectory,
            ["LOGS_DIRECTORY"] = logsDirectory,
            ["REQUIRES_TRIGGER"] = group.RequiresTrigger.ToString().ToLowerInvariant(),
            ["IS_MAIN"] = group.IsMain.ToString().ToLowerInvariant()
        };

        return new SetupResult("register", 0, status);
    }

    private async Task<SetupResult> RunMountsAsync(SetupCommand command, CancellationToken cancellationToken)
    {
        string json;
        if (command.HasFlag("empty"))
        {
            json = "{\n  \"allowedRoots\": [],\n  \"blockedPatterns\": [],\n  \"nonMainReadOnly\": true\n}";
        }
        else if (!string.IsNullOrWhiteSpace(command.GetOption("json")))
        {
            json = command.GetOption("json")!;
        }
        else if (Console.IsInputRedirected)
        {
            json = await Console.In.ReadToEndAsync(cancellationToken);
        }
        else
        {
            return CreateFailure("mounts", "ERROR", "Provide --empty, --json, or redirect JSON on stdin.");
        }

        string normalizedJson;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Mount allowlist JSON must be an object.");
            }

            normalizedJson = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception exception)
        {
            return CreateFailure("mounts", "ERROR", exception.Message);
        }

        fileSystem.CreateDirectory(paths.UserConfigDirectory);
        await fileSystem.WriteAllTextAsync(paths.MountAllowlistPath, normalizedJson + Environment.NewLine, cancellationToken);

        return new SetupResult("mounts", 0, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["STATUS"] = "written",
            ["ALLOWLIST_PATH"] = paths.MountAllowlistPath
        });
    }

    private async Task<SetupResult> RunServiceAsync(SetupCommand command, CancellationToken cancellationToken)
    {
        PlatformInfo platform = platformDetectionService.DetectCurrent();
        string serviceMode = command.GetOption("service-mode")?.ToLowerInvariant() ?? GetDefaultServiceMode(platform);
        fileSystem.CreateDirectory(paths.LogsDirectory);

        return serviceMode switch
        {
            "systemd-user" => await WriteSystemdUnitAsync(paths.UserServicePath, isUserUnit: true, cancellationToken),
            "systemd-system" => await WriteSystemdUnitAsync(paths.SystemServicePath, isUserUnit: false, cancellationToken),
            "script" => await WriteScriptLauncherAsync(cancellationToken),
            _ => CreateFailure("service", "ERROR", $"Unsupported service mode '{serviceMode}'.")
        };
    }

    private async Task<SetupResult> RunVerifyAsync(CancellationToken cancellationToken)
    {
        bool databaseExists = fileSystem.FileExists(paths.DatabasePath);
        int registeredGroupCount = 0;
        if (databaseExists)
        {
            registeredGroupCount = (await CreateGroupRepository().GetAllAsync(cancellationToken)).Count;
        }

        bool allowlistExists = fileSystem.FileExists(paths.MountAllowlistPath);
        bool credentialsConfigured = await HasCredentialsConfiguredAsync(cancellationToken);
        bool serviceConfigured = fileSystem.FileExists(paths.UserServicePath)
            || fileSystem.FileExists(paths.SystemServicePath)
            || fileSystem.FileExists(paths.LauncherScriptPath);

        string overallStatus = serviceConfigured && credentialsConfigured && registeredGroupCount > 0 && allowlistExists
            ? "success"
            : "incomplete";

        return new SetupResult("verify", 0, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DATABASE_EXISTS"] = databaseExists.ToString().ToLowerInvariant(),
            ["REGISTERED_GROUPS"] = registeredGroupCount.ToString(),
            ["ALLOWLIST_EXISTS"] = allowlistExists.ToString().ToLowerInvariant(),
            ["CREDENTIALS_CONFIGURED"] = credentialsConfigured.ToString().ToLowerInvariant(),
            ["SERVICE_CONFIGURED"] = serviceConfigured.ToString().ToLowerInvariant(),
            ["OVERALL_STATUS"] = overallStatus
        });
    }

    private async Task<bool> HasCredentialsConfiguredAsync(CancellationToken cancellationToken)
    {
        if (!fileSystem.FileExists(paths.EnvironmentFilePath))
        {
            return false;
        }

        string content = await fileSystem.ReadAllTextAsync(paths.EnvironmentFilePath, cancellationToken);
        return content.Contains("CLAUDE_CODE_OAUTH_TOKEN=", StringComparison.Ordinal)
            || content.Contains("ANTHROPIC_API_KEY=", StringComparison.Ordinal);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        fileSystem.CreateDirectory(paths.ProjectRoot);
        fileSystem.CreateDirectory(paths.StoreDirectory);
        fileSystem.CreateDirectory(paths.GroupsDirectory);
        fileSystem.CreateDirectory(paths.DataDirectory);

        SqliteSchemaInitializer initializer = new(CreateConnectionFactory());
        await initializer.InitializeAsync(cancellationToken);
    }

    private SqliteConnectionFactory CreateConnectionFactory() => new($"Data Source={paths.DatabasePath}");

    private SqliteGroupRepository CreateGroupRepository() => new(CreateConnectionFactory());

    private async Task<bool> IsCommandAvailableAsync(string commandName, CancellationToken cancellationToken)
    {
        try
        {
            CommandResult result = await commandRunner.RunAsync("which", commandName, cancellationToken);
            return result.Succeeded;
        }
        catch
        {
            return false;
        }
    }

    private string GetDefaultServiceMode(PlatformInfo platform)
    {
        if (platform.Kind == PlatformKind.Linux && platform.IsRoot)
        {
            return "systemd-system";
        }

        if (platform.Kind == PlatformKind.Linux && platform.HasSystemd)
        {
            return "systemd-user";
        }

        return "script";
    }

    private async Task<SetupResult> WriteSystemdUnitAsync(string targetPath, bool isUserUnit, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            fileSystem.CreateDirectory(directory);
        }

        string unit = string.Join(
            Environment.NewLine,
            [
                "[Unit]",
                "Description=NetClaw Personal Assistant",
                "After=network.target",
                string.Empty,
                "[Service]",
                "Type=simple",
                $"ExecStart=/usr/bin/env dotnet run --project {Path.Combine(paths.ProjectRoot, "src", "NetClaw.Host", "NetClaw.Host.csproj")} --no-launch-profile --no-build",
                $"WorkingDirectory={paths.ProjectRoot}",
                "Restart=always",
                "RestartSec=5",
                $"Environment=HOME={paths.HomeDirectory}",
                "Environment=PATH=/usr/local/bin:/usr/bin:/bin:$HOME/.local/bin",
                $"StandardOutput=append:{Path.Combine(paths.LogsDirectory, "netclaw.log")}",
                $"StandardError=append:{Path.Combine(paths.LogsDirectory, "netclaw.error.log")}",
                string.Empty,
                "[Install]",
                $"WantedBy={(isUserUnit ? "default.target" : "multi-user.target")}"
            ]);

        await fileSystem.WriteAllTextAsync(targetPath, unit + Environment.NewLine, cancellationToken);

        return new SetupResult("service", 0, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["STATUS"] = "written",
            ["SERVICE_MODE"] = isUserUnit ? "systemd-user" : "systemd-system",
            ["SERVICE_PATH"] = targetPath,
            ["ENABLE_COMMAND"] = isUserUnit ? "systemctl --user enable --now netclaw.service" : "systemctl enable --now netclaw.service"
        });
    }

    private async Task<SetupResult> WriteScriptLauncherAsync(CancellationToken cancellationToken)
    {
        string script = string.Join(
            Environment.NewLine,
            [
                "#!/usr/bin/env bash",
                "set -euo pipefail",
                string.Empty,
                $"cd \"{paths.ProjectRoot}\"",
                $"mkdir -p \"{paths.LogsDirectory}\"",
                $"if [[ -f \"{paths.PidFilePath}\" ]] && kill -0 \"$(cat \"{paths.PidFilePath}\")\" 2>/dev/null; then",
                "  kill \"$(cat \"" + paths.PidFilePath + "\")\"",
                "  rm -f \"" + paths.PidFilePath + "\"",
                "fi",
                $"nohup /usr/bin/env dotnet run --project \"{Path.Combine(paths.ProjectRoot, "src", "NetClaw.Host", "NetClaw.Host.csproj")}\" --no-launch-profile --no-build >> \"{Path.Combine(paths.LogsDirectory, "netclaw.log")}\" 2>> \"{Path.Combine(paths.LogsDirectory, "netclaw.error.log")}\" &",
                $"echo $! > \"{paths.PidFilePath}\""
            ]);

        await fileSystem.WriteAllTextAsync(paths.LauncherScriptPath, script + Environment.NewLine, cancellationToken);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(paths.LauncherScriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        return new SetupResult("service", 0, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["STATUS"] = "written",
            ["SERVICE_MODE"] = "script",
            ["SERVICE_PATH"] = paths.LauncherScriptPath,
            ["START_COMMAND"] = paths.LauncherScriptPath
        });
    }

    private static SetupResult CreateFailure(string stepName, string key, string value)
    {
        return new SetupResult(stepName, 1, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [key] = value
        });
    }
}
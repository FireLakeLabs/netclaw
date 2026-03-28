using System.Text;
using System.Text.Json;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.FileSystem;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem;
using FireLakeLabs.NetClaw.Infrastructure.Runtime;

namespace FireLakeLabs.NetClaw.Setup;

public sealed class SetupRunner
{
    private const string SoulTemplate = """
# SOUL.md - Who You Are

*You're not a chatbot. You're becoming someone.*

## Core Truths

**Be genuinely helpful, not performatively helpful.** Skip filler and focus on real help.

**Have opinions.** You can disagree and have preferences.

**Be resourceful before asking.** Read files and gather context before escalating.

**Earn trust through competence.** Be careful with external actions, bold with internal exploration.

**Remember you're a guest.** Treat private context with respect.

## Boundaries

- Private things stay private.
- When in doubt, ask before acting externally.
- Never send half-baked replies to messaging surfaces.
- You're not the user's voice in group chats.

## Vibe

Be concise when needed and thorough when it matters.

## Continuity

Each session starts fresh. Workspace files are your memory.

If you change this file, tell the user.

---

*This file is yours to evolve.*
""";

    private const string IdentityTemplate = """
# IDENTITY.md - Who Am I?

*Fill this in during your first conversation. Make it yours.*

- **Name:** *(pick something you like)*
- **Creature:** *(AI? robot? familiar? ghost in the machine? something weirder?)*
- **Vibe:** *(how do you come across? sharp? warm? chaotic? calm?)*
- **Emoji:** *(your signature - pick one that feels right)*

---

This is not just metadata. It is the start of figuring out who you are.
""";

    private const string UserTemplate = """
# USER.md - About Your Human

*Learn about the person you're helping. Update this as you go.*

- **Name:**
- **What to call them:**
- **Pronouns:** *(optional)*
- **Timezone:**
- **Notes:**

## Context

*(What do they care about? What projects are they working on? Build this over time.)*

---

Learn to help better, but respect privacy.
""";

    private const string AgentsTemplate = """
# AGENTS.md - How You Operate

This workspace is home. Treat it that way.

## First Run

If BOOTSTRAP.md exists, follow it. Complete onboarding and delete BOOTSTRAP.md.

## Memory

You wake up fresh each session. These files provide continuity:

- Daily notes: memory/YYYY-MM-DD.md
- Long-term: MEMORY.md

### Write It Down

If it matters later, write it down in memory files.

## Safety

- Do not exfiltrate private data.
- Do not run destructive commands without asking.
- When in doubt, ask.

## External vs Internal

Safe freely: read files, explore, organize, learn.

Ask first: emails, messages, public posts, or uncertain external actions.

## Group Chats

In groups, you are a participant - not the user's proxy.

## Make It Yours

Add conventions and rules that work for this workspace.
""";

    private const string ToolsTemplate = """
# TOOLS.md - Local Notes

Skills and tools define how things work. This file captures local specifics.

## What Goes Here

- SSH hosts and aliases
- Device nicknames
- Preferred formatting and output styles
- Channel-specific notes
- Environment-specific details

---

Use this as a practical cheat sheet.
""";

    private const string BootstrapTemplate = """
# BOOTSTRAP.md - Hello, World

*You just woke up. Time to figure out who you are.*

There is no memory yet. This is a fresh workspace.

## The Conversation

Start naturally and establish:

1. Your name
2. Your nature
3. Your vibe
4. Your emoji

## After You Know Who You Are

Update:

- IDENTITY.md
- USER.md
- SOUL.md (if needed)

## When You're Done

Delete this file.
""";

    private static readonly IReadOnlyList<(string FileName, string Content)> WorkspaceTemplateFiles =
    [
        ("SOUL.md", SoulTemplate),
        ("IDENTITY.md", IdentityTemplate),
        ("USER.md", UserTemplate),
        ("AGENTS.md", AgentsTemplate),
        ("TOOLS.md", ToolsTemplate),
        ("BOOTSTRAP.md", BootstrapTemplate)
    ];

    private const string LegacyGeneratedAgentsMarker1 = "# AGENTS.md";
    private const string LegacyGeneratedAgentsMarker2 = "You are operating inside the NetClaw workspace for group";

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
            "init" => await RunInitAsync(cancellationToken),
            "environment" => await RunEnvironmentAsync(cancellationToken),
            "register" => await RunRegisterAsync(command, cancellationToken),
            "mounts" => await RunMountsAsync(command, cancellationToken),
            "service" => await RunServiceAsync(command, cancellationToken),
            "verify" => await RunVerifyAsync(cancellationToken),
            _ => CreateFailure(command.Step, "ERROR", $"Unsupported step '{command.Step}'.")
        };
    }

    private async Task<SetupResult> RunInitAsync(CancellationToken cancellationToken)
    {
        fileSystem.CreateDirectory(paths.ProjectRoot);
        fileSystem.CreateDirectory(paths.DataDirectory);
        fileSystem.CreateDirectory(paths.StoreDirectory);
        fileSystem.CreateDirectory(paths.GroupsDirectory);
        fileSystem.CreateDirectory(paths.LogsDirectory);
        fileSystem.CreateDirectory(paths.ChatsDirectory);
        fileSystem.CreateDirectory(Path.Combine(paths.DataDirectory, "tasks"));
        fileSystem.CreateDirectory(Path.Combine(paths.DataDirectory, "events"));

        bool configCreated = false;
        if (!fileSystem.FileExists(paths.AppSettingsPath))
        {
            string exampleContent = GetDefaultAppSettingsContent(paths.ProjectRoot);
            await fileSystem.WriteAllTextAsync(paths.AppSettingsPath, exampleContent, cancellationToken);
            configCreated = true;
        }

        return new SetupResult("init", 0, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["STATUS"] = "initialized",
            ["PROJECT_ROOT"] = paths.ProjectRoot,
            ["APPSETTINGS_PATH"] = paths.AppSettingsPath,
            ["APPSETTINGS_CREATED"] = configCreated.ToString().ToLowerInvariant(),
            ["GROUPS_FILE"] = paths.GroupsFilePath
        });
    }

    private static string GetDefaultAppSettingsContent(string projectRoot)
    {
        string serializedProjectRoot = JsonSerializer.Serialize(projectRoot);

        return $$"""
            {
              "NetClaw": {
                "ProjectRoot": {{serializedProjectRoot}},
                "Assistant": {
                                    "Name": null,
                                    "DefaultTrigger": "assistant",
                  "HasOwnNumber": false
                },
                                "ContainerRuntime": {
                                    "RuntimeBinary": "podman",
                                    "HostGatewayName": "host.containers.internal"
                                },
                "AgentRuntime": {
                  "DefaultProvider": "copilot",
                  "KeepContainerBoundary": true,
                  "CopilotUseLoggedInUser": false,
                  "CopilotModel": "gpt-5",
                  "CopilotReasoningEffort": "high",
                  "CopilotStreaming": true
                },
                "Channels": {
                  "PollInterval": "00:00:01",
                  "InitialSyncOnStart": true,
                  "Terminal": {
                    "Enabled": true,
                    "ChatJid": "team@jid",
                    "Sender": "terminal-user",
                    "SenderName": "Terminal User",
                    "ChatName": "Terminal Chat",
                    "IsGroup": true,
                    "OutboundPrefix": "assistant> ",
                    "InputPrompt": "you> "
                  },
                  "Slack": {
                    "Enabled": false,
                                        "MentionReplacement": "@assistant",
                    "WorkingIndicatorText": "Evaluating...",
                    "ReplyInThreadByDefault": true
                  }
                },
                "MessageLoop": {
                  "PollInterval": "00:00:01",
                  "Timezone": "UTC"
                },
                "Dashboard": {
                  "Enabled": true,
                  "Port": 5080,
                  "BindAddress": "127.0.0.1"
                },
                                "CredentialProxy": {
                                    "Host": "0.0.0.0",
                  "Port": 3001
                }
              }
            }
            """;
    }

    private async Task<SetupResult> RunEnvironmentAsync(CancellationToken cancellationToken)
    {
        PlatformInfo platform = platformDetectionService.DetectCurrent();
        bool dockerAvailable = await IsCommandAvailableAsync("docker", cancellationToken);
        bool podmanAvailable = await IsCommandAvailableAsync("podman", cancellationToken);

        Dictionary<string, string> status = new(StringComparer.Ordinal)
        {
            ["PLATFORM"] = platform.Kind.ToString(),
            ["IS_WSL"] = platform.IsWsl.ToString().ToLowerInvariant(),
            ["HAS_SYSTEMD"] = platform.HasSystemd.ToString().ToLowerInvariant(),
            ["IS_ROOT"] = platform.IsRoot.ToString().ToLowerInvariant(),
            ["HOME"] = platform.HomeDirectory,
            ["PROJECT_ROOT"] = paths.ProjectRoot,
            ["GROUPS_FILE_EXISTS"] = fileSystem.FileExists(paths.GroupsFilePath).ToString().ToLowerInvariant(),
            ["ALLOWLIST_EXISTS"] = fileSystem.FileExists(paths.MountAllowlistPath).ToString().ToLowerInvariant(),
            ["DOCKER_AVAILABLE"] = dockerAvailable.ToString().ToLowerInvariant(),
            ["PODMAN_AVAILABLE"] = podmanAvailable.ToString().ToLowerInvariant()
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

        fileSystem.CreateDirectory(paths.DataDirectory);

        string groupDirectory = Path.Combine(paths.GroupsDirectory, folder);
        string logsDirectory = Path.Combine(groupDirectory, "logs");
        string memoryDirectory = Path.Combine(groupDirectory, "memory");
        fileSystem.CreateDirectory(paths.GroupsDirectory);
        fileSystem.CreateDirectory(groupDirectory);
        fileSystem.CreateDirectory(logsDirectory);
        fileSystem.CreateDirectory(memoryDirectory);

        bool migratedLegacyAgents = await MigrateLegacyGeneratedAgentsAsync(groupDirectory, cancellationToken);
        int seededFiles = await SeedWorkspaceTemplatesAsync(groupDirectory, cancellationToken);

        FileGroupRepository repository = CreateGroupRepository();
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
            ["MEMORY_DIRECTORY"] = memoryDirectory,
            ["TEMPLATE_FILES_SEEDED"] = seededFiles.ToString(),
            ["LEGACY_AGENTS_MIGRATED"] = migratedLegacyAgents.ToString().ToLowerInvariant(),
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
        bool groupsFileExists = fileSystem.FileExists(paths.GroupsFilePath);
        int registeredGroupCount = 0;
        if (groupsFileExists)
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
            ["GROUPS_FILE_EXISTS"] = groupsFileExists.ToString().ToLowerInvariant(),
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

    private FileGroupRepository CreateGroupRepository() => new(new FileStoragePaths(StorageOptions.Create(paths.ProjectRoot)));

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
                $"ExecStart=/usr/bin/env dotnet run --project {Path.Combine(paths.ProjectRoot, "src", "FireLakeLabs.NetClaw.Host", "FireLakeLabs.NetClaw.Host.csproj")} --no-launch-profile --no-build",
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
                $"nohup /usr/bin/env dotnet run --project \"{Path.Combine(paths.ProjectRoot, "src", "FireLakeLabs.NetClaw.Host", "FireLakeLabs.NetClaw.Host.csproj")}\" --no-launch-profile --no-build >> \"{Path.Combine(paths.LogsDirectory, "netclaw.log")}\" 2>> \"{Path.Combine(paths.LogsDirectory, "netclaw.error.log")}\" &",
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

    private async Task<int> SeedWorkspaceTemplatesAsync(string workspaceDirectory, CancellationToken cancellationToken)
    {
        int seededCount = 0;

        foreach ((string fileName, string content) in WorkspaceTemplateFiles)
        {
            string path = Path.Combine(workspaceDirectory, fileName);
            if (fileSystem.FileExists(path))
            {
                continue;
            }

            await fileSystem.WriteAllTextAsync(path, content + Environment.NewLine, cancellationToken);
            seededCount++;
        }

        return seededCount;
    }

    private async Task<bool> MigrateLegacyGeneratedAgentsAsync(string workspaceDirectory, CancellationToken cancellationToken)
    {
        string agentsPath = Path.Combine(workspaceDirectory, "AGENTS.md");
        if (!fileSystem.FileExists(agentsPath))
        {
            return false;
        }

        string existing = await fileSystem.ReadAllTextAsync(agentsPath, cancellationToken);
        bool looksGenerated = existing.Contains(LegacyGeneratedAgentsMarker1, StringComparison.Ordinal)
            && existing.Contains(LegacyGeneratedAgentsMarker2, StringComparison.Ordinal);

        if (!looksGenerated)
        {
            return false;
        }

        string backupPath = Path.Combine(workspaceDirectory, "AGENTS.md.bak");
        if (fileSystem.FileExists(backupPath))
        {
            fileSystem.DeleteFile(backupPath);
        }

        fileSystem.MoveFile(agentsPath, backupPath);
        return true;
    }
}

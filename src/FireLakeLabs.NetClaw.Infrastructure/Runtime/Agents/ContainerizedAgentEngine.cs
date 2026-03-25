using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FireLakeLabs.NetClaw.Domain.Contracts.Agents;
using FireLakeLabs.NetClaw.Domain.Contracts.Containers;
using FireLakeLabs.NetClaw.Domain.Contracts.Services;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using FireLakeLabs.NetClaw.Infrastructure.FileSystem;
using FireLakeLabs.NetClaw.Infrastructure.Paths;
using Microsoft.Extensions.Logging;

namespace FireLakeLabs.NetClaw.Infrastructure.Runtime.Agents;

public sealed class ContainerizedAgentEngine : ICodingAgentEngine, IInteractiveCodingAgentEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly IContainerRuntime containerRuntime;
    private readonly ContainerRuntimeOptions containerOptions;
    private readonly CredentialProxyOptions proxyOptions;
    private readonly AgentRuntimeOptions agentOptions;
    private readonly StorageOptions storageOptions;
    private readonly GroupPathResolver groupPathResolver;
    private readonly IFileSystem fileSystem;
    private readonly PlatformInfo platformInfo;
    private readonly ILogger<ContainerizedAgentEngine> logger;

    public ContainerizedAgentEngine(
        IContainerRuntime containerRuntime,
        ContainerRuntimeOptions containerOptions,
        CredentialProxyOptions proxyOptions,
        AgentRuntimeOptions agentOptions,
        StorageOptions storageOptions,
        GroupPathResolver groupPathResolver,
        IFileSystem fileSystem,
        PlatformInfo platformInfo,
        ILogger<ContainerizedAgentEngine> logger)
    {
        this.containerRuntime = containerRuntime;
        this.containerOptions = containerOptions;
        this.proxyOptions = proxyOptions;
        this.agentOptions = agentOptions;
        this.storageOptions = storageOptions;
        this.groupPathResolver = groupPathResolver;
        this.fileSystem = fileSystem;
        this.platformInfo = platformInfo;
        this.logger = logger;
    }

    public AgentProviderKind Provider => agentOptions.GetDefaultProvider();

    public AgentCapabilityProfile Capabilities => new(
        Provider,
        SupportsPersistentSessions: true,
        SupportsSessionResumeAtMessage: false,
        SupportsStreamingText: true,
        SupportsStreamingReasoning: false,
        SupportsCustomTools: false,
        SupportsBuiltInShellTools: true,
        SupportsHookInterception: false,
        SupportsUserInputRequests: false,
        SupportsSubagents: false,
        SupportsWorkspaceInstructions: true,
        SupportsSkills: false,
        SupportsProviderManagedCompaction: false,
        SupportsExplicitCheckpointing: false);

    public async Task<AgentExecutionResult> ExecuteAsync(
        AgentExecutionRequest request,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default)
    {
        ContainerName containerName = BuildContainerName(request.Provider, request.Group.Folder);
        IReadOnlyList<ContainerMount> mounts = BuildMounts(request);
        string ipcDirectory = EnsureIpcDirectory(request.Group.Folder);

        ContainerInput containerInput = request.Input;

        using CancellationTokenSource timeoutCts = new(request.Group.ContainerConfig?.Timeout ?? containerOptions.ExecutionTimeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            ContainerOutput? lastOutput = await RunContainerAsync(
                request.Provider,
                containerName,
                containerInput,
                mounts,
                output =>
                {
                    if (onStreamEvent is not null)
                    {
                        AgentStreamEvent streamEvent = TranslateOutput(output, request.Session);
                        onStreamEvent(streamEvent, CancellationToken.None).GetAwaiter().GetResult();
                    }
                },
                linkedCts.Token);

            if (lastOutput is null)
            {
                return new AgentExecutionResult(ContainerRunStatus.Error, null, request.Session, "Container produced no output.");
            }

            AgentSessionReference? session = lastOutput.NewSessionId is { } sid
                ? new AgentSessionReference(request.Provider, sid.Value, request.Workspace.WorkspaceDirectory)
                : request.Session;

            return new AgentExecutionResult(lastOutput.Status, lastOutput.Result, session, lastOutput.Error);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            logger.LogWarning("Container {ContainerName} timed out after {Timeout}.", containerName.Value, containerOptions.ExecutionTimeout);
            await StopContainerSafeAsync(containerName);
            return new AgentExecutionResult(ContainerRunStatus.Error, null, request.Session, "Container execution timed out.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Container {ContainerName} execution failed.", containerName.Value);
            return new AgentExecutionResult(ContainerRunStatus.Error, null, request.Session, exception.Message);
        }
    }

    public async Task<IInteractiveAgentSession> StartInteractiveSessionAsync(
        AgentExecutionRequest request,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default)
    {
        ContainerName containerName = BuildContainerName(request.Provider, request.Group.Folder);
        IReadOnlyList<ContainerMount> mounts = BuildMounts(request);
        string ipcInputDirectory = EnsureIpcInputDirectory(request.Group.Folder);

        ContainerInput containerInput = request.Input;

        return new ContainerizedInteractiveSession(
            this,
            request,
            containerName,
            containerInput,
            mounts,
            ipcInputDirectory,
            onStreamEvent,
            agentOptions.InteractiveIdleTimeout);
    }

    internal async Task<ContainerOutput?> RunContainerAsync(
        AgentProviderKind provider,
        ContainerName containerName,
        ContainerInput input,
        IReadOnlyList<ContainerMount> mounts,
        Action<ContainerOutput> onOutput,
        CancellationToken cancellationToken)
    {
        string arguments = BuildDockerRunArgs(provider, containerName, mounts);
        string inputJson = JsonSerializer.Serialize(input, JsonOptions);

        logger.LogDebug("Starting container {ContainerName}: {Runtime} {Arguments}", containerName.Value, containerOptions.RuntimeBinary, arguments);

        using Process process = new();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = containerOptions.RuntimeBinary,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        await process.StandardInput.WriteLineAsync(inputJson);
        process.StandardInput.Close();

        ContainerOutput? lastOutput = null;
        StringBuilder stderrBuilder = new();

        Task stderrTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync(cancellationToken)) is not null)
            {
                stderrBuilder.AppendLine(line);
                logger.LogTrace("[container:{ContainerName}] {StderrLine}", containerName.Value, line);
            }
        }, cancellationToken);

        string? stdoutLine;
        while ((stdoutLine = await process.StandardOutput.ReadLineAsync(cancellationToken)) is not null)
        {
            if (string.IsNullOrWhiteSpace(stdoutLine))
            {
                continue;
            }

            try
            {
                ContainerOutput? output = JsonSerializer.Deserialize<ContainerOutput>(stdoutLine, JsonOptions);
                if (output is not null)
                {
                    lastOutput = output;
                    onOutput(output);
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning("Failed to parse container JSONL line: {Line} - {Error}", stdoutLine, ex.Message);
            }
        }

        await stderrTask;
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0 && lastOutput is null)
        {
            string stderr = stderrBuilder.ToString().Trim();
            lastOutput = new ContainerOutput(
                ContainerRunStatus.Error,
                null,
                null,
                string.IsNullOrWhiteSpace(stderr) ? $"Container exited with code {process.ExitCode}" : stderr);
        }

        return lastOutput;
    }

    internal IReadOnlyList<ContainerMount> BuildMounts(AgentExecutionRequest request)
    {
        List<ContainerMount> mounts = [];

        string groupDir = groupPathResolver.ResolveGroupDirectory(request.Group.Folder);
        fileSystem.CreateDirectory(groupDir);
        mounts.Add(new ContainerMount(groupDir, "/workspace/group", false));

        if (!request.Group.IsMain)
        {
            string globalDir = Path.Combine(storageOptions.GroupsDirectory, "global");
            if (fileSystem.DirectoryExists(globalDir))
            {
                mounts.Add(new ContainerMount(fileSystem.GetFullPath(globalDir), "/workspace/global", true));
            }
        }

        string sessionDir = groupPathResolver.ResolveGroupSessionDirectory(request.Group.Folder);
        fileSystem.CreateDirectory(sessionDir);
        string containerSessionPath = request.Provider == AgentProviderKind.ClaudeCode
            ? "/home/user/.claude"
            : "/home/user/.copilot";
        mounts.Add(new ContainerMount(sessionDir, containerSessionPath, false));

        string ipcDir = groupPathResolver.ResolveGroupIpcDirectory(request.Group.Folder);
        fileSystem.CreateDirectory(ipcDir);
        mounts.Add(new ContainerMount(ipcDir, "/workspace/ipc", false));

        if (request.Group.IsMain)
        {
            string projectRoot = storageOptions.ProjectRoot;
            mounts.Add(new ContainerMount(projectRoot, "/workspace/project", true));
        }

        return mounts;
    }

    private string BuildDockerRunArgs(AgentProviderKind provider, ContainerName containerName, IReadOnlyList<ContainerMount> mounts)
    {
        StringBuilder args = new();
        args.Append("run -i --rm ");
        args.Append($"--name {containerName.Value} ");

        string providerName = provider switch
        {
            AgentProviderKind.ClaudeCode => "claude-code",
            AgentProviderKind.Codex => "codex",
            AgentProviderKind.OpenCode => "open-code",
            _ => "copilot"
        };
        args.Append($"-e NETCLAW_PROVIDER={providerName} ");

        string proxyHost = containerOptions.HostGatewayName;
        args.Append($"-e NETCLAW_CREDENTIAL_PROXY_URL=http://{proxyHost}:{proxyOptions.Port} ");

        if (provider == AgentProviderKind.ClaudeCode)
        {
            args.Append("-e ANTHROPIC_API_KEY=placeholder ");
        }
        else
        {
            args.Append("-e COPILOT_TOKEN=placeholder ");
        }

        string? timezone = Environment.GetEnvironmentVariable("TZ");
        if (!string.IsNullOrWhiteSpace(timezone))
        {
            args.Append($"-e TZ={timezone} ");
        }

        foreach (string gatewayArg in containerRuntime.GetHostGatewayArguments())
        {
            args.Append($"{gatewayArg} ");
        }

        if (!platformInfo.IsRoot)
        {
            int uid = Environment.ProcessId;
            try
            {
                string uidStr = File.ReadAllText("/proc/self/loginuid").Trim();
                if (int.TryParse(uidStr, out int parsedUid) && parsedUid > 0 && parsedUid < 65534)
                {
                    uid = parsedUid;
                }
            }
            catch
            {
                // Fall back to current process uid approach
            }
        }

        foreach (ContainerMount mount in mounts)
        {
            string roFlag = mount.IsReadOnly ? ":ro" : string.Empty;
            args.Append($"-v {mount.HostPath}:{mount.ContainerPath}{roFlag} ");
        }

        args.Append(containerOptions.ImageName);

        return args.ToString().TrimEnd();
    }

    private string EnsureIpcDirectory(GroupFolder groupFolder)
    {
        string ipcDir = groupPathResolver.ResolveGroupIpcDirectory(groupFolder);
        fileSystem.CreateDirectory(ipcDir);
        fileSystem.CreateDirectory(Path.Combine(ipcDir, "messages"));
        fileSystem.CreateDirectory(Path.Combine(ipcDir, "tasks"));
        return ipcDir;
    }

    private string EnsureIpcInputDirectory(GroupFolder groupFolder)
    {
        string ipcDir = groupPathResolver.ResolveGroupIpcDirectory(groupFolder);
        string inputDir = Path.Combine(ipcDir, "input");
        fileSystem.CreateDirectory(inputDir);
        return inputDir;
    }

    private static ContainerName BuildContainerName(AgentProviderKind provider, GroupFolder groupFolder)
    {
        string safeGroup = new string(groupFolder.Value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray()).Trim('-');
        return new ContainerName($"netclaw-{provider.ToString().ToLowerInvariant()}-{safeGroup}");
    }

    private static AgentStreamEvent TranslateOutput(ContainerOutput output, AgentSessionReference? session)
    {
        AgentEventKind kind = output.Status switch
        {
            ContainerRunStatus.Error => AgentEventKind.Error,
            ContainerRunStatus.Success => AgentEventKind.MessageCompleted,
            _ => AgentEventKind.TextDelta
        };

        AgentSessionReference? updatedSession = output.NewSessionId is { } sid
            ? new AgentSessionReference(session?.Provider ?? AgentProviderKind.Copilot, sid.Value)
            : session;

        return new AgentStreamEvent(kind, output.Result, null, updatedSession, output.Error, DateTimeOffset.UtcNow);
    }

    private async Task StopContainerSafeAsync(ContainerName containerName)
    {
        try
        {
            await containerRuntime.StopContainerAsync(containerName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to stop container {ContainerName}.", containerName.Value);
        }
    }

    private sealed class ContainerizedInteractiveSession : IInteractiveAgentSession
    {
        private readonly ContainerizedAgentEngine engine;
        private readonly AgentExecutionRequest request;
        private readonly ContainerName containerName;
        private readonly string ipcInputDirectory;
        private int closeRequested;
        private int inputSequence;

        public ContainerizedInteractiveSession(
            ContainerizedAgentEngine engine,
            AgentExecutionRequest request,
            ContainerName containerName,
            ContainerInput input,
            IReadOnlyList<ContainerMount> mounts,
            string ipcInputDirectory,
            Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent,
            TimeSpan idleTimeout)
        {
            this.engine = engine;
            this.request = request;
            this.containerName = containerName;
            this.ipcInputDirectory = ipcInputDirectory;

            Session = request.Session ?? new AgentSessionReference(request.Provider, Guid.NewGuid().ToString("D"));

            Completion = RunAsync(input, mounts, onStreamEvent, idleTimeout);
        }

        public AgentSessionReference Session { get; }

        public Task<AgentExecutionResult> Completion { get; }

        public bool TryPostInput(string text)
        {
            if (Volatile.Read(ref closeRequested) == 1)
            {
                return false;
            }

            try
            {
                int seq = Interlocked.Increment(ref inputSequence);
                string fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{seq:D4}.json";
                string filePath = Path.Combine(ipcInputDirectory, fileName);
                string json = JsonSerializer.Serialize(new { text }, JsonOptions);
                File.WriteAllText(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void RequestClose()
        {
            if (Interlocked.Exchange(ref closeRequested, 1) == 0)
            {
                try
                {
                    string closePath = Path.Combine(ipcInputDirectory, "_close");
                    File.WriteAllText(closePath, string.Empty);
                }
                catch
                {
                    // Best-effort
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            RequestClose();
            await engine.StopContainerSafeAsync(containerName);
        }

        private async Task<AgentExecutionResult> RunAsync(
            ContainerInput input,
            IReadOnlyList<ContainerMount> mounts,
            Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent,
            TimeSpan idleTimeout)
        {
            try
            {
                using CancellationTokenSource timeoutCts = new(idleTimeout);

                ContainerOutput? lastOutput = await engine.RunContainerAsync(
                    request.Provider,
                    containerName,
                    input,
                    mounts,
                    output =>
                    {
                        timeoutCts.CancelAfter(idleTimeout);
                        if (onStreamEvent is not null)
                        {
                            AgentStreamEvent streamEvent = TranslateOutput(output, Session);
                            onStreamEvent(streamEvent, CancellationToken.None).GetAwaiter().GetResult();
                        }
                    },
                    timeoutCts.Token);

                if (lastOutput is null)
                {
                    return new AgentExecutionResult(ContainerRunStatus.Error, null, Session, "Container produced no output.");
                }

                return new AgentExecutionResult(lastOutput.Status, lastOutput.Result, Session, lastOutput.Error);
            }
            catch (OperationCanceledException)
            {
                return new AgentExecutionResult(ContainerRunStatus.Error, null, Session, "Interactive session timed out.");
            }
        }
    }
}

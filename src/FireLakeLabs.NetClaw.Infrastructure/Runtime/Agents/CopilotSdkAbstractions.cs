using FireLakeLabs.NetClaw.Domain.Contracts.Agents;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Infrastructure.Configuration;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;

namespace FireLakeLabs.NetClaw.Infrastructure.Runtime.Agents;

public sealed record CopilotSessionConfiguration(
    string SessionId,
    string ClientName,
    string Model,
    string? ReasoningEffort,
    string WorkingDirectory,
    string ConfigDirectory,
    string? SystemMessage,
    bool Streaming,
    bool EnableInfiniteSessions,
    double? BackgroundCompactionThreshold,
    double? BufferExhaustionThreshold,
    IReadOnlyList<string> SkillDirectories,
    IReadOnlyList<string> DisabledSkills,
    IReadOnlyList<AIFunction> Tools);

public interface ICopilotSessionAdapter : IAsyncDisposable
{
    string SessionId { get; }

    string? WorkspacePath { get; }

    Task<string?> SendPromptAsync(string prompt, CancellationToken cancellationToken = default);
}

public interface ICopilotClientAdapter
{
    Task<ICopilotSessionAdapter> CreateSessionAsync(
        CopilotSessionConfiguration configuration,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default);

    Task<ICopilotSessionAdapter> ResumeSessionAsync(
        string sessionId,
        CopilotSessionConfiguration configuration,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default);
}

public interface ICopilotClientPool
{
    Task<ICopilotClientAdapter> GetClientAsync(CancellationToken cancellationToken = default);
}

public interface ICopilotClientAdapterFactory
{
    ICopilotClientAdapter Create();
}

public sealed class CopilotClientPool : ICopilotClientPool, IAsyncDisposable
{
    private readonly ICopilotClientAdapterFactory clientFactory;
    private readonly SemaphoreSlim gate = new(1, 1);
    private ICopilotClientAdapter? client;

    public CopilotClientPool(ICopilotClientAdapterFactory clientFactory)
    {
        this.clientFactory = clientFactory;
    }

    public async Task<ICopilotClientAdapter> GetClientAsync(CancellationToken cancellationToken = default)
    {
        if (client is not null)
        {
            return client;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            client ??= clientFactory.Create();
            return client;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (client is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }

        gate.Dispose();
    }
}

public sealed class SdkCopilotClientAdapterFactory : ICopilotClientAdapterFactory
{
    private readonly AgentRuntimeOptions options;

    public SdkCopilotClientAdapterFactory(AgentRuntimeOptions options)
    {
        this.options = options;
    }

    public ICopilotClientAdapter Create()
    {
        return new SdkCopilotClientAdapter(BuildClient());
    }

    private CopilotClient BuildClient()
    {
        string? cliUrl = string.IsNullOrWhiteSpace(options.CopilotCliUrl) ? null : options.CopilotCliUrl;
        Directory.CreateDirectory(options.CopilotConfigDirectory);

        CopilotClientOptions clientOptions = new()
        {
            AutoRestart = options.CopilotAutoRestart,
            AutoStart = options.CopilotAutoStart,
            CliPath = cliUrl is null ? options.CopilotCliPath : null,
            CliUrl = cliUrl,
            Cwd = options.CopilotConfigDirectory,
            GitHubToken = string.IsNullOrWhiteSpace(options.CopilotGitHubToken) ? null : options.CopilotGitHubToken,
            LogLevel = options.CopilotLogLevel,
            UseLoggedInUser = options.CopilotUseLoggedInUser,
            UseStdio = options.CopilotUseStdio
        };

        return new CopilotClient(clientOptions);
    }
}

public sealed class SdkCopilotClientAdapter : ICopilotClientAdapter, IAsyncDisposable
{
    private readonly CopilotClient client;

    public SdkCopilotClientAdapter(CopilotClient client)
    {
        this.client = client;
    }

    public async Task<ICopilotSessionAdapter> CreateSessionAsync(
        CopilotSessionConfiguration configuration,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default)
    {
        SessionConfig sessionConfig = BuildSessionConfig(configuration, onStreamEvent);
        CopilotSession session = await client.CreateSessionAsync(sessionConfig, cancellationToken);
        RegisterEventHandler(session, configuration.SessionId, onStreamEvent);
        return new SdkCopilotSessionAdapter(session);
    }

    public async Task<ICopilotSessionAdapter> ResumeSessionAsync(
        string sessionId,
        CopilotSessionConfiguration configuration,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default)
    {
        ResumeSessionConfig sessionConfig = BuildResumeSessionConfig(configuration, onStreamEvent);
        CopilotSession session = await client.ResumeSessionAsync(sessionId, sessionConfig, cancellationToken);
        RegisterEventHandler(session, configuration.SessionId, onStreamEvent);
        return new SdkCopilotSessionAdapter(session);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await client.DisposeAsync();
        }
        catch (Exception)
        {
        }
    }

    private static SessionConfig BuildSessionConfig(
        CopilotSessionConfiguration configuration,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent)
    {
        return new SessionConfig
        {
            ClientName = configuration.ClientName,
            ConfigDir = configuration.ConfigDirectory,
            DisabledSkills = configuration.DisabledSkills.Count == 0 ? null : [.. configuration.DisabledSkills],
            InfiniteSessions = BuildInfiniteSessionConfig(configuration),
            Model = configuration.Model,
            OnPermissionRequest = PermissionHandler.ApproveAll,
            ReasoningEffort = configuration.ReasoningEffort,
            SessionId = configuration.SessionId,
            SkillDirectories = configuration.SkillDirectories.Count == 0 ? null : [.. configuration.SkillDirectories],
            Tools = configuration.Tools.Count == 0 ? null : [.. configuration.Tools],
            Streaming = configuration.Streaming,
            SystemMessage = BuildSystemMessage(configuration.SystemMessage),
            WorkingDirectory = configuration.WorkingDirectory
        };
    }

    private static ResumeSessionConfig BuildResumeSessionConfig(
        CopilotSessionConfiguration configuration,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent)
    {
        return new ResumeSessionConfig
        {
            ClientName = configuration.ClientName,
            ConfigDir = configuration.ConfigDirectory,
            DisabledSkills = configuration.DisabledSkills.Count == 0 ? null : [.. configuration.DisabledSkills],
            InfiniteSessions = BuildInfiniteSessionConfig(configuration),
            Model = configuration.Model,
            OnPermissionRequest = PermissionHandler.ApproveAll,
            ReasoningEffort = configuration.ReasoningEffort,
            SkillDirectories = configuration.SkillDirectories.Count == 0 ? null : [.. configuration.SkillDirectories],
            Tools = configuration.Tools.Count == 0 ? null : [.. configuration.Tools],
            Streaming = configuration.Streaming,
            SystemMessage = BuildSystemMessage(configuration.SystemMessage),
            WorkingDirectory = configuration.WorkingDirectory
        };
    }

    private static void RegisterEventHandler(
        CopilotSession session,
        string sessionId,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent)
    {
        SessionEventHandler? handler = BuildEventHandler(sessionId, onStreamEvent);
        if (handler is not null)
        {
            session.On(handler);
        }
    }

    private static InfiniteSessionConfig BuildInfiniteSessionConfig(CopilotSessionConfiguration configuration)
    {
        return new InfiniteSessionConfig
        {
            Enabled = configuration.EnableInfiniteSessions,
            BackgroundCompactionThreshold = configuration.BackgroundCompactionThreshold,
            BufferExhaustionThreshold = configuration.BufferExhaustionThreshold
        };
    }

    private static SystemMessageConfig? BuildSystemMessage(string? content)
    {
        return string.IsNullOrWhiteSpace(content)
            ? null
            : new SystemMessageConfig
            {
                Mode = SystemMessageMode.Append,
                Content = content
            };
    }

    private static SessionEventHandler? BuildEventHandler(
        string sessionId,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent)
    {
        if (onStreamEvent is null)
        {
            return null;
        }

        return sessionEvent =>
        {
            AgentStreamEvent? mappedEvent = MapEvent(sessionId, sessionEvent);
            if (mappedEvent is not null)
            {
                onStreamEvent(mappedEvent, CancellationToken.None).GetAwaiter().GetResult();
            }
        };
    }

    private static AgentStreamEvent? MapEvent(string sessionId, SessionEvent sessionEvent)
    {
        AgentSessionReference sessionReference = new(AgentProviderKind.Copilot, sessionId);

        return sessionEvent switch
        {
            SessionStartEvent => new AgentStreamEvent(AgentEventKind.SessionStarted, null, null, sessionReference, null, sessionEvent.Timestamp),
            SessionResumeEvent => new AgentStreamEvent(AgentEventKind.SessionStarted, null, null, sessionReference, null, sessionEvent.Timestamp),
            AssistantMessageDeltaEvent delta => new AgentStreamEvent(AgentEventKind.TextDelta, delta.Data.DeltaContent, null, sessionReference, null, sessionEvent.Timestamp),
            AssistantReasoningDeltaEvent delta => new AgentStreamEvent(AgentEventKind.ReasoningDelta, delta.Data.DeltaContent, null, sessionReference, null, sessionEvent.Timestamp),
            AssistantMessageEvent message => new AgentStreamEvent(AgentEventKind.MessageCompleted, message.Data.Content, null, sessionReference, null, sessionEvent.Timestamp),
            ToolExecutionStartEvent toolStart => new AgentStreamEvent(AgentEventKind.ToolStarted, null, toolStart.Data.ToolName, sessionReference, null, sessionEvent.Timestamp),
            ToolExecutionCompleteEvent toolComplete => new AgentStreamEvent(
                AgentEventKind.ToolCompleted,
                toolComplete.Data.Result?.DetailedContent ?? toolComplete.Data.Result?.Content,
                null,
                sessionReference,
                toolComplete.Data.Error?.Message,
                sessionEvent.Timestamp),
            SessionIdleEvent => new AgentStreamEvent(AgentEventKind.Idle, null, null, sessionReference, null, sessionEvent.Timestamp),
            SessionErrorEvent error => new AgentStreamEvent(AgentEventKind.Error, null, null, sessionReference, error.Data.Message, sessionEvent.Timestamp),
            _ => null
        };
    }
}

public sealed class SdkCopilotSessionAdapter : ICopilotSessionAdapter
{
    private readonly CopilotSession session;

    public SdkCopilotSessionAdapter(CopilotSession session)
    {
        this.session = session;
    }

    public string SessionId => session.SessionId;

    public string? WorkspacePath => session.WorkspacePath;

    public async Task<string?> SendPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        AssistantMessageEvent? response = await session.SendAndWaitAsync(
            new MessageOptions
            {
                Prompt = prompt
            },
            cancellationToken: cancellationToken);

        return response?.Data.Content;
    }

    public ValueTask DisposeAsync()
    {
        return session.DisposeAsync();
    }
}

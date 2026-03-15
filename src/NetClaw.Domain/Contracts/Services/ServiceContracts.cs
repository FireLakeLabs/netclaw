using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Channels;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Contracts.Ipc;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Domain.Contracts.Services;

public interface IMessageFormatter
{
    string FormatInbound(IReadOnlyList<StoredMessage> messages, string timezone);

    string NormalizeOutbound(string rawText);

    IReadOnlyList<OutboundFileReference> ExtractFileReferences(string rawText);
}

public interface IOutboundRouter
{
    Task RouteAsync(IReadOnlyList<IChannel> channels, ChatJid chatJid, string text, CancellationToken cancellationToken = default);

    Task RouteFileAsync(IReadOnlyList<IChannel> channels, ChatJid chatJid, string filePath, string fileName, CancellationToken cancellationToken = default);
}

public interface IGroupExecutionQueue
{
    void EnqueueMessageCheck(ChatJid groupJid);

    void EnqueueTask(ChatJid groupJid, TaskId taskId, Func<CancellationToken, Task> workItem);

    bool SendMessage(ChatJid groupJid, string text);

    void CloseInput(ChatJid groupJid);

    void NotifyIdle(ChatJid groupJid);
}

public interface ITaskSchedulerService
{
    DateTimeOffset? ComputeNextRun(ScheduledTask task, DateTimeOffset now);

    Task RunDueTasksAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}

public interface IIpcCommandProcessor
{
    Task ProcessAsync(GroupFolder sourceGroup, bool isMainGroup, IpcCommand command, CancellationToken cancellationToken = default);
}

public interface IIpcCommandWatcher
{
    Task PollOnceAsync(CancellationToken cancellationToken = default);
}

public interface ISenderAuthorizationService
{
    IReadOnlyList<StoredMessage> ApplyInboundPolicy(ChatJid chatJid, IReadOnlyList<StoredMessage> messages);

    bool CanTrigger(ChatJid chatJid, StoredMessage message);
}

public interface IContainerRuntime
{
    Task EnsureRunningAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetHostGatewayArguments();

    Task StopContainerAsync(ContainerName containerName, CancellationToken cancellationToken = default);

    Task CleanupOrphansAsync(CancellationToken cancellationToken = default);
}

public interface IContainerExecutionService
{
    Task<ContainerExecutionResult> RunAsync(
        ContainerExecutionRequest request,
        Func<ContainerStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default);
}

public interface IAgentRuntime
{
    Task<ContainerExecutionResult> ExecuteAsync(
        ContainerInput input,
        Func<ContainerStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default);

    Task<IInteractiveContainerSession> StartInteractiveSessionAsync(
        ContainerInput input,
        Func<ContainerStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default);
}

public interface ICredentialProxyService
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Uri BindAddress { get; }
}

public interface ICodingAgentEngine
{
    AgentProviderKind Provider { get; }

    AgentCapabilityProfile Capabilities { get; }

    Task<AgentExecutionResult> ExecuteAsync(
        AgentExecutionRequest request,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default);
}

public interface IInteractiveCodingAgentEngine : ICodingAgentEngine
{
    Task<IInteractiveAgentSession> StartInteractiveSessionAsync(
        AgentExecutionRequest request,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default);
}

public interface IAgentWorkspaceBuilder
{
    Task<AgentWorkspaceContext> BuildAsync(
        RegisteredGroup group,
        ContainerInput input,
        CancellationToken cancellationToken = default);
}

public interface IAgentToolRegistry
{
    IReadOnlyList<AgentToolDefinition> GetTools(RegisteredGroup group, ContainerInput input);
}

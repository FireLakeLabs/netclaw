using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Domain.Contracts.Containers;

public sealed record ContainerInput(
    string Prompt,
    SessionId? SessionId,
    GroupFolder GroupFolder,
    ChatJid ChatJid,
    bool IsMain,
    bool IsScheduledTask,
    string? AssistantName);

public sealed record ContainerOutput(ContainerRunStatus Status, string? Result, SessionId? NewSessionId, string? Error);

public sealed record ContainerStreamEvent(ContainerEventKind Kind, ContainerOutput Output, DateTimeOffset ObservedAt);

public sealed record AvailableGroup(ChatJid Jid, string Name, DateTimeOffset? LastActivity, bool IsRegistered);

public sealed record ContainerMount(string HostPath, string ContainerPath, bool IsReadOnly);

public sealed record ContainerExecutionRequest(
    RegisteredGroup Group,
    ContainerInput Input,
    IReadOnlyList<ContainerMount> Mounts,
    ContainerName ContainerName);

public sealed record ContainerExecutionResult(ContainerRunStatus Status, string? Result, SessionId? NewSessionId, string? Error, ContainerName ContainerName);

public interface IInteractiveContainerSession : IAsyncDisposable
{
    SessionId? SessionId { get; }

    ContainerName ContainerName { get; }

    bool TryPostInput(string text);

    void RequestClose();

    Task<ContainerExecutionResult> Completion { get; }
}
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Domain.Contracts.Agents;

public sealed record AgentSessionReference
{
    public AgentSessionReference(AgentProviderKind provider, string sessionId, string? workspacePath = null, string? resumeAt = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Agent session ID is required.", nameof(sessionId));
        }

        Provider = provider;
        SessionId = sessionId.Trim();
        WorkspacePath = string.IsNullOrWhiteSpace(workspacePath) ? null : workspacePath.Trim();
        ResumeAt = string.IsNullOrWhiteSpace(resumeAt) ? null : resumeAt.Trim();
    }

    public AgentProviderKind Provider { get; }

    public string SessionId { get; }

    public string? WorkspacePath { get; }

    public string? ResumeAt { get; }
}

public sealed record AgentToolDefinition
{
    public AgentToolDefinition(string name, string description, bool isBuiltIn = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tool name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Tool description is required.", nameof(description));
        }

        Name = name.Trim();
        Description = description.Trim();
        IsBuiltIn = isBuiltIn;
    }

    public string Name { get; }

    public string Description { get; }

    public bool IsBuiltIn { get; }
}

public sealed record AgentInstructionDocument
{
    public AgentInstructionDocument(string relativePath, string content, bool isGenerated = false)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Instruction document path is required.", nameof(relativePath));
        }

        RelativePath = relativePath.Trim();
        Content = content ?? string.Empty;
        IsGenerated = isGenerated;
    }

    public string RelativePath { get; }

    public string Content { get; }

    public bool IsGenerated { get; }
}

public sealed record AgentInstructionSet(IReadOnlyList<AgentInstructionDocument> Documents, string? RuntimeAppendix = null);

public sealed record AgentWorkspaceContext(
    GroupFolder GroupFolder,
    string WorkingDirectory,
    string SessionDirectory,
    string WorkspaceDirectory,
    bool IsMain,
    IReadOnlyList<string> AdditionalDirectories,
    AgentInstructionSet Instructions);

public sealed record AgentCapabilityProfile(
    AgentProviderKind Provider,
    bool SupportsPersistentSessions,
    bool SupportsSessionResumeAtMessage,
    bool SupportsStreamingText,
    bool SupportsStreamingReasoning,
    bool SupportsCustomTools,
    bool SupportsBuiltInShellTools,
    bool SupportsHookInterception,
    bool SupportsUserInputRequests,
    bool SupportsSubagents,
    bool SupportsWorkspaceInstructions,
    bool SupportsSkills,
    bool SupportsProviderManagedCompaction,
    bool SupportsExplicitCheckpointing);

public sealed record AgentExecutionRequest(
    AgentProviderKind Provider,
    RegisteredGroup Group,
    ContainerInput Input,
    AgentWorkspaceContext Workspace,
    AgentSessionReference? Session,
    IReadOnlyList<AgentToolDefinition> Tools);

public sealed record AgentStreamEvent(
    AgentEventKind Kind,
    string? Content,
    string? ToolName,
    AgentSessionReference? Session,
    string? Error,
    DateTimeOffset ObservedAt);

public sealed record AgentExecutionResult(
    ContainerRunStatus Status,
    string? Result,
    AgentSessionReference? Session,
    string? Error);

public interface IInteractiveAgentSession : IAsyncDisposable
{
    AgentSessionReference Session { get; }

    bool TryPostInput(string text);

    void RequestClose();

    Task<AgentExecutionResult> Completion { get; }
}

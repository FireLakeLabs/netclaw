using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Enums;
using NetClaw.Infrastructure.Configuration;

namespace NetClaw.Infrastructure.Runtime.Agents;

public sealed class CopilotCodingAgentEngine : ICodingAgentEngine
{
    private readonly ICopilotClientPool clientPool;
    private readonly AgentRuntimeOptions options;

    public CopilotCodingAgentEngine(ICopilotClientPool clientPool, AgentRuntimeOptions options)
    {
        this.clientPool = clientPool;
        this.options = options;
    }

    public AgentProviderKind Provider => AgentProviderKind.Copilot;

    public AgentCapabilityProfile Capabilities => new(
        AgentProviderKind.Copilot,
        SupportsPersistentSessions: true,
        SupportsSessionResumeAtMessage: true,
        SupportsStreamingText: true,
        SupportsStreamingReasoning: true,
        SupportsCustomTools: true,
        SupportsBuiltInShellTools: true,
        SupportsHookInterception: true,
        SupportsUserInputRequests: true,
        SupportsSubagents: false,
        SupportsWorkspaceInstructions: true,
        SupportsSkills: true,
        SupportsProviderManagedCompaction: true,
        SupportsExplicitCheckpointing: false);

    public async Task<AgentExecutionResult> ExecuteAsync(
        AgentExecutionRequest request,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ICopilotClientAdapter client = await clientPool.GetClientAsync(cancellationToken);
            CopilotSessionConfiguration configuration = BuildConfiguration(request);

            await using ICopilotSessionAdapter session = request.Session is null
                ? await client.CreateSessionAsync(configuration, onStreamEvent, cancellationToken)
                : await client.ResumeSessionAsync(request.Session.SessionId, configuration, onStreamEvent, cancellationToken);

            string? result = await session.SendPromptAsync(request.Input.Prompt, cancellationToken);
            AgentSessionReference sessionReference = new(
                AgentProviderKind.Copilot,
                session.SessionId,
                session.WorkspacePath ?? request.Workspace.WorkspaceDirectory,
                request.Session?.ResumeAt);

            return new AgentExecutionResult(ContainerRunStatus.Success, result, sessionReference, null);
        }
        catch (Exception exception)
        {
            if (onStreamEvent is not null)
            {
                await onStreamEvent(
                    new AgentStreamEvent(AgentEventKind.Error, null, null, request.Session, exception.Message, DateTimeOffset.UtcNow),
                    cancellationToken);
            }

            return new AgentExecutionResult(ContainerRunStatus.Error, null, request.Session, exception.Message);
        }
    }

    private CopilotSessionConfiguration BuildConfiguration(AgentExecutionRequest request)
    {
        string sessionId = request.Session?.SessionId ?? Guid.NewGuid().ToString("D");
        string systemMessage = BuildSystemMessage(request.Workspace.Instructions);

        return new CopilotSessionConfiguration(
            sessionId,
            options.CopilotClientName,
            options.CopilotModel,
            options.CopilotReasoningEffort,
            request.Workspace.WorkingDirectory,
            options.CopilotConfigDirectory,
            systemMessage,
            options.CopilotStreaming,
            options.CopilotEnableInfiniteSessions,
            options.CopilotBackgroundCompactionThreshold,
            options.CopilotBufferExhaustionThreshold,
            [],
            []);
    }

    private static string BuildSystemMessage(AgentInstructionSet instructionSet)
    {
        List<string> sections = [];

        foreach (AgentInstructionDocument document in instructionSet.Documents)
        {
            if (string.IsNullOrWhiteSpace(document.Content))
            {
                continue;
            }

            sections.Add($"<instruction_document path=\"{document.RelativePath}\">\n{document.Content}\n</instruction_document>");
        }

        if (!string.IsNullOrWhiteSpace(instructionSet.RuntimeAppendix))
        {
            sections.Add($"<runtime_appendix>\n{instructionSet.RuntimeAppendix}\n</runtime_appendix>");
        }

        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }
}
using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Enums;
using NetClaw.Infrastructure.Configuration;
using System.Threading.Channels;

namespace NetClaw.Infrastructure.Runtime.Agents;

public sealed class CopilotCodingAgentEngine : IInteractiveCodingAgentEngine
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

    public async Task<IInteractiveAgentSession> StartInteractiveSessionAsync(
        AgentExecutionRequest request,
        Func<AgentStreamEvent, CancellationToken, Task>? onStreamEvent = null,
        CancellationToken cancellationToken = default)
    {
        ICopilotClientAdapter client = await clientPool.GetClientAsync(cancellationToken);
        CopilotSessionConfiguration configuration = BuildConfiguration(request);

        ICopilotSessionAdapter session = request.Session is null
            ? await client.CreateSessionAsync(configuration, onStreamEvent, cancellationToken)
            : await client.ResumeSessionAsync(request.Session.SessionId, configuration, onStreamEvent, cancellationToken);

        AgentSessionReference sessionReference = new(
            AgentProviderKind.Copilot,
            session.SessionId,
            session.WorkspacePath ?? request.Workspace.WorkspaceDirectory,
            request.Session?.ResumeAt);

        return new CopilotInteractiveAgentSession(
            session,
            sessionReference,
            request.Input.Prompt,
            options.InteractiveIdleTimeout);
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

    private sealed class CopilotInteractiveAgentSession : IInteractiveAgentSession
    {
        private readonly Channel<string> inputs = Channel.CreateUnbounded<string>();
        private readonly TimeSpan idleTimeout;
        private readonly object promptGate = new();
        private readonly ICopilotSessionAdapter session;
        private CancellationTokenSource? activePromptCancellation;
        private int closeRequested;
        private int disposeState;

        public CopilotInteractiveAgentSession(
            ICopilotSessionAdapter session,
            AgentSessionReference sessionReference,
            string initialPrompt,
            TimeSpan idleTimeout)
        {
            this.session = session;
            Session = sessionReference;
            this.idleTimeout = idleTimeout;
            Completion = RunAsync(initialPrompt);
        }

        public AgentSessionReference Session { get; }

        public Task<AgentExecutionResult> Completion { get; }

        public bool TryPostInput(string text)
        {
            if (Volatile.Read(ref closeRequested) == 1)
            {
                return false;
            }

            return inputs.Writer.TryWrite(text);
        }

        public void RequestClose()
        {
            if (Interlocked.Exchange(ref closeRequested, 1) == 1)
            {
                return;
            }

            inputs.Writer.TryComplete();

            lock (promptGate)
            {
                activePromptCancellation?.Cancel();
            }
        }

        public async ValueTask DisposeAsync()
        {
            RequestClose();
            try
            {
                await Completion;
            }
            finally
            {
                await DisposeSessionAsync();
            }
        }

        private async Task<AgentExecutionResult> RunAsync(string initialPrompt)
        {
            string? latestResult = null;
            string currentPrompt = initialPrompt;

            try
            {
                while (true)
                {
                    if (Volatile.Read(ref closeRequested) == 1)
                    {
                        return CreateInterruptedResult(latestResult);
                    }

                    using CancellationTokenSource promptCancellation = new();
                    SetActivePromptCancellation(promptCancellation);

                    try
                    {
                        latestResult = await session.SendPromptAsync(currentPrompt, promptCancellation.Token);
                    }
                    catch (OperationCanceledException) when (Volatile.Read(ref closeRequested) == 1)
                    {
                        return CreateInterruptedResult(latestResult);
                    }
                    finally
                    {
                        ClearActivePromptCancellation(promptCancellation);
                    }

                    string? nextInput = await WaitForNextInputAsync();
                    if (string.IsNullOrWhiteSpace(nextInput))
                    {
                        return new AgentExecutionResult(ContainerRunStatus.Success, latestResult, Session, null);
                    }

                    currentPrompt = nextInput;
                }
            }
            catch (Exception exception)
            {
                return new AgentExecutionResult(ContainerRunStatus.Error, latestResult, Session, exception.Message);
            }
            finally
            {
                await DisposeSessionAsync();
            }
        }

        private AgentExecutionResult CreateInterruptedResult(string? latestResult)
        {
            return new AgentExecutionResult(ContainerRunStatus.Error, latestResult, Session, "Interactive session interrupted.");
        }

        private void SetActivePromptCancellation(CancellationTokenSource promptCancellation)
        {
            lock (promptGate)
            {
                if (Volatile.Read(ref closeRequested) == 1)
                {
                    promptCancellation.Cancel();
                }

                activePromptCancellation = promptCancellation;
            }
        }

        private void ClearActivePromptCancellation(CancellationTokenSource promptCancellation)
        {
            lock (promptGate)
            {
                if (ReferenceEquals(activePromptCancellation, promptCancellation))
                {
                    activePromptCancellation = null;
                }
            }
        }

        private async ValueTask DisposeSessionAsync()
        {
            if (Interlocked.Exchange(ref disposeState, 1) == 1)
            {
                return;
            }

            await session.DisposeAsync();
        }

        private async Task<string?> WaitForNextInputAsync()
        {
            Task<bool> waitTask = inputs.Reader.WaitToReadAsync().AsTask();
            Task delayTask = Task.Delay(idleTimeout);
            Task completed = await Task.WhenAny(waitTask, delayTask);

            if (completed != waitTask)
            {
                inputs.Writer.TryComplete();
                return null;
            }

            if (!await waitTask)
            {
                return null;
            }

            return inputs.Reader.TryRead(out string? input) ? input : null;
        }
    }
}
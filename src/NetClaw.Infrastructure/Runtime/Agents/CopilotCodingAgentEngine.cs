using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Enums;

namespace NetClaw.Infrastructure.Runtime.Agents;

public sealed class CopilotCodingAgentEngine : AbstractPlaceholderCodingAgentEngine
{
    public CopilotCodingAgentEngine()
        : base(
            AgentProviderKind.Copilot,
            new AgentCapabilityProfile(
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
                SupportsExplicitCheckpointing: false))
    {
    }
}
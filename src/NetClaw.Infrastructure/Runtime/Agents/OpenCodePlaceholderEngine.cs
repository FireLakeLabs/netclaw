using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Enums;

namespace NetClaw.Infrastructure.Runtime.Agents;

public sealed class OpenCodePlaceholderEngine : AbstractPlaceholderCodingAgentEngine
{
    public OpenCodePlaceholderEngine()
        : base(
            AgentProviderKind.OpenCode,
            new AgentCapabilityProfile(
                AgentProviderKind.OpenCode,
                SupportsPersistentSessions: true,
                SupportsSessionResumeAtMessage: false,
                SupportsStreamingText: true,
                SupportsStreamingReasoning: false,
                SupportsCustomTools: true,
                SupportsBuiltInShellTools: true,
                SupportsHookInterception: false,
                SupportsUserInputRequests: false,
                SupportsSubagents: false,
                SupportsWorkspaceInstructions: true,
                SupportsSkills: false,
                SupportsProviderManagedCompaction: false,
                SupportsExplicitCheckpointing: false))
    {
    }
}

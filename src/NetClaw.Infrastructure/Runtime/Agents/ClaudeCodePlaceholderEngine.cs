using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Enums;

namespace NetClaw.Infrastructure.Runtime.Agents;

public sealed class ClaudeCodePlaceholderEngine : AbstractPlaceholderCodingAgentEngine
{
    public ClaudeCodePlaceholderEngine()
        : base(
            AgentProviderKind.ClaudeCode,
            new AgentCapabilityProfile(
                AgentProviderKind.ClaudeCode,
                SupportsPersistentSessions: true,
                SupportsSessionResumeAtMessage: true,
                SupportsStreamingText: true,
                SupportsStreamingReasoning: true,
                SupportsCustomTools: true,
                SupportsBuiltInShellTools: true,
                SupportsHookInterception: true,
                SupportsUserInputRequests: true,
                SupportsSubagents: true,
                SupportsWorkspaceInstructions: true,
                SupportsSkills: true,
                SupportsProviderManagedCompaction: true,
                SupportsExplicitCheckpointing: true))
    {
    }
}

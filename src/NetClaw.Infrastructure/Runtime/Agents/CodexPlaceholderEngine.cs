using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Enums;

namespace NetClaw.Infrastructure.Runtime.Agents;

public sealed class CodexPlaceholderEngine : AbstractPlaceholderCodingAgentEngine
{
    public CodexPlaceholderEngine()
        : base(
            AgentProviderKind.Codex,
            new AgentCapabilityProfile(
                AgentProviderKind.Codex,
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

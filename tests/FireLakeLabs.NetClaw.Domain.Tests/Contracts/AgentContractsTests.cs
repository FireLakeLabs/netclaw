using FireLakeLabs.NetClaw.Domain.Contracts.Agents;
using FireLakeLabs.NetClaw.Domain.Contracts.Containers;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.Enums;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Domain.Tests.Contracts;

public sealed class AgentContractsTests
{
    [Fact]
    public void AgentSessionReference_RejectsEmptySessionId()
    {
        Assert.Throws<ArgumentException>(() => new AgentSessionReference(AgentProviderKind.Copilot, string.Empty));
    }

    [Fact]
    public void AgentToolDefinition_RejectsMissingName()
    {
        Assert.Throws<ArgumentException>(() => new AgentToolDefinition(string.Empty, "desc"));
    }

    [Fact]
    public void AgentExecutionRequest_PreservesProviderWorkspaceAndTools()
    {
        RegisteredGroup group = new("Team", new GroupFolder("team"), "@assistant", DateTimeOffset.UtcNow);
        ContainerInput input = new("Prompt", null, new GroupFolder("team"), new ChatJid("team@jid"), false, false, "assistant");
        AgentWorkspaceContext workspace = new(
            new GroupFolder("team"),
            "/workspace/group",
            "/workspace/sessions/team",
            "/workspace/runtime/team",
            false,
            ["/workspace/global"],
            new AgentInstructionSet([new AgentInstructionDocument("AGENTS.md", "# Instructions", true)]));
        AgentToolDefinition[] tools = [new("send_group_message", "Send a message back to the group")];

        AgentExecutionRequest request = new(AgentProviderKind.Copilot, group, input, workspace, null, tools);

        Assert.Equal(AgentProviderKind.Copilot, request.Provider);
        Assert.Equal("/workspace/runtime/team", request.Workspace.WorkspaceDirectory);
        Assert.Single(request.Tools);
        Assert.Equal("AGENTS.md", request.Workspace.Instructions.Documents[0].RelativePath);
    }
}

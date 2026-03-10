using NetClaw.Domain.Contracts.Agents;
using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Contracts.Services;
using NetClaw.Domain.Entities;

namespace NetClaw.Infrastructure.Runtime.Agents;

public sealed class NetClawAgentToolRegistry : IAgentToolRegistry
{
    public IReadOnlyList<AgentToolDefinition> GetTools(RegisteredGroup group, ContainerInput input)
    {
        List<AgentToolDefinition> tools =
        [
            new AgentToolDefinition("send_group_message", "Send a response back to the active group."),
            new AgentToolDefinition("list_registered_groups", "List the registered groups known to NetClaw."),
            new AgentToolDefinition("schedule_group_task", "Schedule a task for the active or specified group."),
            new AgentToolDefinition("lookup_session_state", "Resolve the stored session state for the current group."),
            new AgentToolDefinition("close_group_input", "Close the active group input stream when interactive work is complete.")
        ];

        if (group.IsMain || input.IsMain)
        {
            tools.Add(new AgentToolDefinition("register_group", "Register a new group through the main-group control plane."));
        }

        return tools;
    }
}
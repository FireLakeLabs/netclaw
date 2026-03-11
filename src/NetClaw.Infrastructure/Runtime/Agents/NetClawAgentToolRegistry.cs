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
            new AgentToolDefinition("send_group_message", "Send a message immediately to the active group. From the main group you may optionally target another registered group by targetJid."),
            new AgentToolDefinition("list_registered_groups", "List all registered groups with their JIDs, folders, triggers, and main-group status."),
            new AgentToolDefinition("schedule_group_task", "Schedule a reminder or recurring task. Use scheduleType of once, interval, or cron. Use scheduleValue as an ISO-8601 timestamp for once, milliseconds for interval, or a cron expression for cron. Optionally provide contextMode of isolated or group and targetJid when acting from the main group."),
            new AgentToolDefinition("list_scheduled_tasks", "List scheduled tasks for the current group. From the main group, omitting targetJid lists tasks across all groups. Optionally provide targetJid to scope to one registered group and includeInactive=true to include completed or cancelled tasks."),
            new AgentToolDefinition("pause_scheduled_task", "Pause a scheduled task by taskId. The current group may pause its own tasks, and the main group may pause tasks across groups."),
            new AgentToolDefinition("resume_scheduled_task", "Resume a paused scheduled task by taskId. The current group may resume its own tasks, and the main group may resume tasks across groups."),
            new AgentToolDefinition("cancel_scheduled_task", "Cancel a scheduled task by taskId. The current group may cancel its own tasks, and the main group may cancel tasks across groups."),
            new AgentToolDefinition("lookup_session_state", "Look up the persisted interactive session state for the active group or an optionally specified registered group."),
            new AgentToolDefinition("close_group_input", "Close the active interactive input stream for the current group or, from the main group, another registered group.")
        ];

        if (group.IsMain || input.IsMain)
        {
            tools.Add(new AgentToolDefinition("register_group", "Register a new group through the main-group control plane using jid, name, folder, trigger, and optional requiresTrigger/isMain flags."));
        }

        return tools;
    }
}

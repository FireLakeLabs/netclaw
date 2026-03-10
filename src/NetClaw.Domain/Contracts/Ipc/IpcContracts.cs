using NetClaw.Domain.Contracts.Containers;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;
using TaskStatusEnum = NetClaw.Domain.Enums.TaskStatus;

namespace NetClaw.Domain.Contracts.Ipc;

public abstract record IpcCommand(string Type);

public sealed record IpcMessageCommand(ChatJid ChatJid, string Text) : IpcCommand("message");

public sealed record IpcInputMessage(string Text);

public sealed record IpcCloseSignal;

public sealed record IpcTaskCommand(
    TaskId? TaskId,
    string Prompt,
    ScheduleType ScheduleType,
    string ScheduleValue,
    TaskContextMode ContextMode,
    ChatJid TargetJid) : IpcCommand("schedule_task");

public sealed record IpcRegisterGroupCommand(
    ChatJid Jid,
    string Name,
    GroupFolder Folder,
    string Trigger,
    bool RequiresTrigger,
    bool IsMain,
    ContainerConfig? ContainerConfig) : IpcCommand("register_group");

public sealed record GroupSnapshotItem(ChatJid Jid, string Name, DateTimeOffset? LastActivity, bool IsRegistered);

public sealed record GroupsSnapshot(IReadOnlyList<GroupSnapshotItem> Groups);

public sealed record TaskSnapshotItem(TaskId Id, GroupFolder GroupFolder, string Prompt, ScheduleType ScheduleType, string ScheduleValue, TaskStatusEnum Status, DateTimeOffset? NextRun);

public sealed record TasksSnapshot(IReadOnlyList<TaskSnapshotItem> Tasks);
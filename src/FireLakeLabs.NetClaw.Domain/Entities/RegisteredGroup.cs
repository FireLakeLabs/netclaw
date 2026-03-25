using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Domain.Entities;

public sealed record RegisteredGroup
{
    public RegisteredGroup(
        string name,
        GroupFolder folder,
        string trigger,
        DateTimeOffset addedAt,
        ContainerConfig? containerConfig = null,
        bool requiresTrigger = true,
        bool isMain = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Group name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(trigger))
        {
            throw new ArgumentException("Trigger is required.", nameof(trigger));
        }

        if (isMain)
        {
            requiresTrigger = false;
        }

        Name = name.Trim();
        Folder = folder;
        Trigger = trigger.Trim();
        AddedAt = addedAt;
        ContainerConfig = containerConfig;
        RequiresTrigger = requiresTrigger;
        IsMain = isMain;
    }

    public string Name { get; }

    public GroupFolder Folder { get; }

    public string Trigger { get; }

    public DateTimeOffset AddedAt { get; }

    public ContainerConfig? ContainerConfig { get; }

    public bool RequiresTrigger { get; }

    public bool IsMain { get; }
}

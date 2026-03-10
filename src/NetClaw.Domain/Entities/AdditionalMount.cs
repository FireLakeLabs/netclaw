namespace NetClaw.Domain.Entities;

public sealed record AdditionalMount
{
    public AdditionalMount(string hostPath, string? containerPath = null, bool isReadOnly = true)
    {
        if (string.IsNullOrWhiteSpace(hostPath))
        {
            throw new ArgumentException("Host path is required.", nameof(hostPath));
        }

        if (containerPath is not null && string.IsNullOrWhiteSpace(containerPath))
        {
            throw new ArgumentException("Container path cannot be empty when provided.", nameof(containerPath));
        }

        HostPath = hostPath.Trim();
        ContainerPath = containerPath?.Trim();
        IsReadOnly = isReadOnly;
    }

    public string HostPath { get; }

    public string? ContainerPath { get; }

    public bool IsReadOnly { get; }
}
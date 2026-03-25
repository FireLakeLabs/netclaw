namespace FireLakeLabs.NetClaw.Dashboard.Services;

/// <summary>
/// Thrown when a requested path escapes the allowed workspace directory boundaries.
/// </summary>
public sealed class WorkspacePathTraversalException : InvalidOperationException
{
    public WorkspacePathTraversalException(string message) : base(message)
    {
    }
}

namespace NetClaw.Infrastructure.Runtime;

public enum PlatformKind
{
    Linux,
    MacOs,
    Windows,
    Unknown
}

public sealed record PlatformInfo(PlatformKind Kind, bool IsWsl, bool HasSystemd, bool IsRoot, string HomeDirectory)
{
    public bool UsesUserLoopbackForProxy => Kind == PlatformKind.MacOs || IsWsl;
}
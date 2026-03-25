namespace FireLakeLabs.NetClaw.Infrastructure.Runtime;

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

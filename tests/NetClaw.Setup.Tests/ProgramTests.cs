namespace NetClaw.Setup.Tests;

public sealed class ProgramTests
{
    [Fact]
    public void Main_ReturnsSuccessExitCode()
    {
        int exitCode = NetClaw.Setup.Program.Main([]);

        Assert.Equal(0, exitCode);
    }
}
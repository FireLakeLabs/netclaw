namespace FireLakeLabs.NetClaw.Setup;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        SetupCommand command = SetupCommand.Parse(args);
        SetupRunner runner = SetupRunner.CreateDefault();
        SetupResult result = await runner.RunAsync(command);
        SetupStatusWriter.Write(result);
        return result.ExitCode;
    }
}

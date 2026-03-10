using NetClaw.Setup;

namespace NetClaw.Setup.Tests;

public sealed class ProgramTests
{
    [Fact]
    public async Task RunAsync_RegisterStep_CreatesDirectoriesAndStoresGroup()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();

        try
        {
            SetupRunner runner = CreateRunner(projectRoot, homeDirectory);

            SetupResult result = await runner.RunAsync(SetupCommand.Parse([
                "--step", "register",
                "--jid", "group@jid",
                "--name", "Team",
                "--trigger", "@Andy",
                "--folder", "team"
            ]));

            Assert.Equal(0, result.ExitCode);
            Assert.True(Directory.Exists(Path.Combine(projectRoot, "groups", "team", "logs")));
            Assert.True(File.Exists(Path.Combine(projectRoot, "data", "netclaw.db")));
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    [Fact]
    public async Task RunAsync_MountsStep_WritesAllowlistConfig()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();

        try
        {
            SetupRunner runner = CreateRunner(projectRoot, homeDirectory);

            SetupResult result = await runner.RunAsync(SetupCommand.Parse([
                "--step", "mounts",
                "--empty"
            ]));

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(homeDirectory, ".config", "netclaw", "mount-allowlist.json")));
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    [Fact]
    public async Task RunAsync_ServiceStep_ScriptMode_WritesLauncherScript()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();

        try
        {
            SetupRunner runner = CreateRunner(projectRoot, homeDirectory);

            SetupResult result = await runner.RunAsync(SetupCommand.Parse([
                "--step", "service",
                "--service-mode", "script"
            ]));

            Assert.Equal(0, result.ExitCode);
            Assert.True(File.Exists(Path.Combine(projectRoot, "start-netclaw.sh")));
            Assert.Contains("dotnet run --project", await File.ReadAllTextAsync(Path.Combine(projectRoot, "start-netclaw.sh")));
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    [Fact]
    public async Task RunAsync_VerifyStep_ReportsSuccessWhenArtifactsExist()
    {
        string projectRoot = CreateTemporaryPath();
        string homeDirectory = CreateTemporaryPath();

        try
        {
            SetupRunner runner = CreateRunner(projectRoot, homeDirectory);

            await File.WriteAllTextAsync(Path.Combine(projectRoot, ".env"), "ANTHROPIC_API_KEY=test-key\n");
            await runner.RunAsync(SetupCommand.Parse([
                "--step", "register",
                "--jid", "group@jid",
                "--name", "Team",
                "--trigger", "@Andy",
                "--folder", "team"
            ]));
            await runner.RunAsync(SetupCommand.Parse(["--step", "mounts", "--empty"]));
            await runner.RunAsync(SetupCommand.Parse(["--step", "service", "--service-mode", "script"]));

            SetupResult result = await runner.RunAsync(SetupCommand.Parse(["--step", "verify"]));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("success", result.Status["OVERALL_STATUS"]);
        }
        finally
        {
            DeleteTemporaryPath(projectRoot);
            DeleteTemporaryPath(homeDirectory);
        }
    }

    private static SetupRunner CreateRunner(string projectRoot, string homeDirectory)
    {
        return new SetupRunner(
            SetupPaths.Create(projectRoot, homeDirectory),
            new NetClaw.Infrastructure.FileSystem.PhysicalFileSystem(),
            new NetClaw.Infrastructure.Runtime.ProcessCommandRunner(),
            new NetClaw.Infrastructure.Runtime.PlatformDetectionService());
    }

    private static string CreateTemporaryPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"netclaw-setup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryPath(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
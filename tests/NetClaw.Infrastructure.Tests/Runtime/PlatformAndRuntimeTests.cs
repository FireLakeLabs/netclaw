using NetClaw.Domain.ValueObjects;
using NetClaw.Infrastructure.Configuration;
using NetClaw.Infrastructure.Runtime;

namespace NetClaw.Infrastructure.Tests.Runtime;

public sealed class PlatformAndRuntimeTests
{
    [Fact]
    public void PlatformDetectionService_MapsLinuxAndWslCorrectly()
    {
        PlatformDetectionService service = new();
        PlatformInfo info = service.Detect("linux", isWsl: true, hasSystemd: true, isRoot: false, homeDirectory: "/home/aaron");

        Assert.Equal(PlatformKind.Linux, info.Kind);
        Assert.True(info.IsWsl);
        Assert.True(info.UsesUserLoopbackForProxy);
    }

    [Fact]
    public async Task DockerContainerRuntime_UsesCommandRunnerForHealthChecksAndStops()
    {
        FakeCommandRunner commandRunner = new();
        DockerContainerRuntime runtime = new(
            commandRunner,
            new ContainerRuntimeOptions(),
            new PlatformInfo(PlatformKind.Linux, IsWsl: false, HasSystemd: true, IsRoot: false, HomeDirectory: "/home/aaron"));

        await runtime.EnsureRunningAsync();
        await runtime.StopContainerAsync(new ContainerName("netclaw-test"));

        Assert.Contains(commandRunner.Commands, command => command == "docker info");
        Assert.Contains(commandRunner.Commands, command => command == "docker stop netclaw-test");
        Assert.Single(runtime.GetHostGatewayArguments());
    }

    private sealed class FakeCommandRunner : ICommandRunner
    {
        public List<string> Commands { get; } = [];

        public Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        {
            Commands.Add($"{fileName} {arguments}");
            return Task.FromResult(new CommandResult(0, string.Empty, string.Empty));
        }
    }
}

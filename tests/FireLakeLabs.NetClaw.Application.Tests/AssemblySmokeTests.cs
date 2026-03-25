namespace FireLakeLabs.NetClaw.Application.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void ApplicationAssemblyMarker_ComesFromApplicationAssembly()
    {
        Assert.Equal(
            "FireLakeLabs.NetClaw.Application",
            typeof(FireLakeLabs.NetClaw.Application.ApplicationAssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void ApplicationAssembly_ExposesCoreServiceImplementations()
    {
        Type[] types =
        [
            typeof(FireLakeLabs.NetClaw.Application.Formatting.XmlMessageFormatter),
            typeof(FireLakeLabs.NetClaw.Application.Routing.ChannelOutboundRouter),
            typeof(FireLakeLabs.NetClaw.Application.Execution.GroupExecutionQueue),
            typeof(FireLakeLabs.NetClaw.Application.Scheduling.TaskSchedulerService),
            typeof(FireLakeLabs.NetClaw.Application.Ipc.IpcCommandProcessor)
        ];

        Assert.All(types, type => Assert.Equal("FireLakeLabs.NetClaw.Application", type.Assembly.GetName().Name));
    }
}

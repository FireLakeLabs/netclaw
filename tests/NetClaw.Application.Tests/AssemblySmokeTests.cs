namespace NetClaw.Application.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void ApplicationAssemblyMarker_ComesFromApplicationAssembly()
    {
        Assert.Equal(
            "NetClaw.Application",
            typeof(NetClaw.Application.ApplicationAssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void ApplicationAssembly_ExposesCoreServiceImplementations()
    {
        Type[] types =
        [
            typeof(NetClaw.Application.Formatting.XmlMessageFormatter),
            typeof(NetClaw.Application.Routing.ChannelOutboundRouter),
            typeof(NetClaw.Application.Execution.GroupExecutionQueue),
            typeof(NetClaw.Application.Scheduling.TaskSchedulerService),
            typeof(NetClaw.Application.Ipc.IpcCommandProcessor)
        ];

        Assert.All(types, type => Assert.Equal("NetClaw.Application", type.Assembly.GetName().Name));
    }
}

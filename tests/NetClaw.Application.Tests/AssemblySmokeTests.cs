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
}
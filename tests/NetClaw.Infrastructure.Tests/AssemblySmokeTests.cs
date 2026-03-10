namespace NetClaw.Infrastructure.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void InfrastructureAssemblyMarker_ComesFromInfrastructureAssembly()
    {
        Assert.Equal(
            "NetClaw.Infrastructure",
            typeof(NetClaw.Infrastructure.InfrastructureAssemblyMarker).Assembly.GetName().Name);
    }
}
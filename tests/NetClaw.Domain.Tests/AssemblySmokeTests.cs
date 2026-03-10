namespace NetClaw.Domain.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void DomainAssemblyMarker_ComesFromDomainAssembly()
    {
        Assert.Equal(
            "NetClaw.Domain",
            typeof(NetClaw.Domain.DomainAssemblyMarker).Assembly.GetName().Name);
    }
}
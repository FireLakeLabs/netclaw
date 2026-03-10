namespace NetClaw.IntegrationTests;

public sealed class BootstrapCompositionTests
{
    [Fact]
    public void BootstrapAssemblies_ExposeSolutionMarkerTypes()
    {
        Assert.Equal("NetClaw.Domain", typeof(NetClaw.Domain.DomainAssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("NetClaw.Application", typeof(NetClaw.Application.ApplicationAssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("NetClaw.Infrastructure", typeof(NetClaw.Infrastructure.InfrastructureAssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("NetClaw.Host", typeof(NetClaw.Host.Program).Assembly.GetName().Name);
        Assert.Equal("NetClaw.Setup", typeof(NetClaw.Setup.Program).Assembly.GetName().Name);
    }
}
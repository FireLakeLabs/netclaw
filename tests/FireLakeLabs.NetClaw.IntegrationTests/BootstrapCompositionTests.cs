namespace FireLakeLabs.NetClaw.IntegrationTests;

public sealed class BootstrapCompositionTests
{
    [Fact]
    public void BootstrapAssemblies_ExposeSolutionMarkerTypes()
    {
        Assert.Equal("FireLakeLabs.NetClaw.Domain", typeof(FireLakeLabs.NetClaw.Domain.DomainAssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("FireLakeLabs.NetClaw.Application", typeof(FireLakeLabs.NetClaw.Application.ApplicationAssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("FireLakeLabs.NetClaw.Infrastructure", typeof(FireLakeLabs.NetClaw.Infrastructure.InfrastructureAssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("FireLakeLabs.NetClaw.Host", typeof(FireLakeLabs.NetClaw.Host.Program).Assembly.GetName().Name);
        Assert.Equal("FireLakeLabs.NetClaw.Setup", typeof(FireLakeLabs.NetClaw.Setup.Program).Assembly.GetName().Name);
    }
}

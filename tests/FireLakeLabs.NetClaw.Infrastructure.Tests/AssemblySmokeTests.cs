namespace FireLakeLabs.NetClaw.Infrastructure.Tests;

public sealed class AssemblySmokeTests
{
    [Fact]
    public void InfrastructureAssemblyMarker_ComesFromInfrastructureAssembly()
    {
        Assert.Equal(
            "FireLakeLabs.NetClaw.Infrastructure",
            typeof(FireLakeLabs.NetClaw.Infrastructure.InfrastructureAssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void InfrastructureAssembly_ExposesFilePersistenceTypes()
    {
        Type[] types =
        [
            typeof(FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem.FileStoragePaths),
            typeof(FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem.FileMessageRepository),
            typeof(FireLakeLabs.NetClaw.Infrastructure.Persistence.FileSystem.FileGroupRepository),
            typeof(FireLakeLabs.NetClaw.Infrastructure.Ipc.FileSystemIpcWatcher),
            typeof(FireLakeLabs.NetClaw.Infrastructure.Runtime.Agents.CopilotCodingAgentEngine),
            typeof(FireLakeLabs.NetClaw.Infrastructure.Runtime.Agents.NetClawAgentRuntime)
        ];

        Assert.All(types, type => Assert.Equal("FireLakeLabs.NetClaw.Infrastructure", type.Assembly.GetName().Name));
    }
}

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
    public void InfrastructureAssembly_ExposesSqlitePersistenceTypes()
    {
        Type[] types =
        [
            typeof(FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite.SqliteConnectionFactory),
            typeof(FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite.SqliteSchemaInitializer),
            typeof(FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite.SqliteMessageRepository),
            typeof(FireLakeLabs.NetClaw.Infrastructure.Ipc.FileSystemIpcWatcher),
            typeof(FireLakeLabs.NetClaw.Infrastructure.Runtime.Agents.CopilotCodingAgentEngine),
            typeof(FireLakeLabs.NetClaw.Infrastructure.Runtime.Agents.NetClawAgentRuntime)
        ];

        Assert.All(types, type => Assert.Equal("FireLakeLabs.NetClaw.Infrastructure", type.Assembly.GetName().Name));
    }
}

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

    [Fact]
    public void InfrastructureAssembly_ExposesSqlitePersistenceTypes()
    {
        Type[] types =
        [
            typeof(NetClaw.Infrastructure.Persistence.Sqlite.SqliteConnectionFactory),
            typeof(NetClaw.Infrastructure.Persistence.Sqlite.SqliteSchemaInitializer),
            typeof(NetClaw.Infrastructure.Persistence.Sqlite.SqliteMessageRepository)
        ];

        Assert.All(types, type => Assert.Equal("NetClaw.Infrastructure", type.Assembly.GetName().Name));
    }
}
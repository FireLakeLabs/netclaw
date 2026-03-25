using FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.Sqlite;

internal sealed class TestSqliteDatabase : IAsyncDisposable
{
    private readonly string databasePath;

    public TestSqliteDatabase()
    {
        databasePath = Path.Combine(Path.GetTempPath(), $"netclaw-tests-{Guid.NewGuid():N}.db");
        ConnectionFactory = new SqliteConnectionFactory($"Data Source={databasePath}");
        SchemaInitializer = new SqliteSchemaInitializer(ConnectionFactory);
    }

    public SqliteConnectionFactory ConnectionFactory { get; }

    public SqliteSchemaInitializer SchemaInitializer { get; }

    public ValueTask DisposeAsync()
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        return ValueTask.CompletedTask;
    }
}

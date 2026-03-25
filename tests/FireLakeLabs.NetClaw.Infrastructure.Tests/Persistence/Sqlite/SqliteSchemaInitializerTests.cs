using FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite;
using Microsoft.Data.Sqlite;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.Sqlite;

public sealed class SqliteSchemaInitializerTests
{
    [Fact]
    public async Task InitializeAsync_CreatesExpectedTables()
    {
        await using TestSqliteDatabase database = new();

        await database.SchemaInitializer.InitializeAsync();

        await using SqliteConnection connection = database.ConnectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();

        List<string> tableNames = [];
        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        Assert.Contains("chats", tableNames);
        Assert.Contains("messages", tableNames);
        Assert.Contains("scheduled_tasks", tableNames);
        Assert.Contains("task_run_logs", tableNames);
        Assert.Contains("router_state", tableNames);
        Assert.Contains("sessions", tableNames);
        Assert.Contains("registered_groups", tableNames);
    }
}

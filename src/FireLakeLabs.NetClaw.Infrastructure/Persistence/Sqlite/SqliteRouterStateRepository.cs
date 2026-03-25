using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Entities;
using Microsoft.Data.Sqlite;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite;

public sealed class SqliteRouterStateRepository : IRouterStateRepository
{
    private readonly SqliteConnectionFactory connectionFactory;

    public SqliteRouterStateRepository(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<RouterStateEntry?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM router_state WHERE key = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", key);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new RouterStateEntry(reader.GetString(0), reader.GetString(1));
    }

    public async Task<IReadOnlyList<RouterStateEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<RouterStateEntry> results = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT key, value FROM router_state ORDER BY key;";

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new RouterStateEntry(reader.GetString(0), reader.GetString(1)));
        }

        return results;
    }

    public async Task SetAsync(RouterStateEntry entry, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO router_state (key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", entry.Key);
        command.Parameters.AddWithValue("$value", entry.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

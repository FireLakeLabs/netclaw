using Microsoft.Data.Sqlite;
using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite;

public sealed class SqliteSessionRepository : ISessionRepository
{
    private readonly SqliteConnectionFactory connectionFactory;

    public SqliteSessionRepository(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyDictionary<GroupFolder, SessionId>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<GroupFolder, SessionId> sessions = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT group_folder, session_id FROM sessions;";

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sessions[new GroupFolder(reader.GetString(0))] = new SessionId(reader.GetString(1));
        }

        return sessions;
    }

    public async Task<SessionId?> GetByGroupFolderAsync(GroupFolder groupFolder, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT session_id FROM sessions WHERE group_folder = $groupFolder LIMIT 1;";
        command.Parameters.AddWithValue("$groupFolder", groupFolder.Value);

        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result is string sessionId ? new SessionId(sessionId) : null;
    }

    public async Task UpsertAsync(SessionState sessionState, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO sessions (group_folder, session_id)
            VALUES ($groupFolder, $sessionId)
            ON CONFLICT(group_folder) DO UPDATE SET session_id = excluded.session_id;
            """;
        command.Parameters.AddWithValue("$groupFolder", sessionState.GroupFolder.Value);
        command.Parameters.AddWithValue("$sessionId", sessionState.SessionId.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

using FireLakeLabs.NetClaw.Domain.Contracts.Persistence;
using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite;

public sealed class SqliteGroupRepository : IGroupRepository
{
    private readonly SqliteConnectionFactory connectionFactory;

    public SqliteGroupRepository(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyDictionary<ChatJid, RegisteredGroup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<ChatJid, RegisteredGroup> groups = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT jid, name, folder, trigger_pattern, added_at, container_config, requires_trigger, is_main FROM registered_groups;";

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ChatJid jid = new(reader.GetString(0));
            groups[jid] = ReadGroup(reader);
        }

        return groups;
    }

    public async Task<RegisteredGroup?> GetByJidAsync(ChatJid chatJid, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT jid, name, folder, trigger_pattern, added_at, container_config, requires_trigger, is_main FROM registered_groups WHERE jid = $jid LIMIT 1;";
        command.Parameters.AddWithValue("$jid", chatJid.Value);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadGroup(reader) : null;
    }

    public async Task UpsertAsync(ChatJid chatJid, RegisteredGroup group, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO registered_groups (jid, name, folder, trigger_pattern, added_at, container_config, requires_trigger, is_main)
            VALUES ($jid, $name, $folder, $trigger, $addedAt, $containerConfig, $requiresTrigger, $isMain)
            ON CONFLICT(jid) DO UPDATE SET
                name = excluded.name,
                folder = excluded.folder,
                trigger_pattern = excluded.trigger_pattern,
                added_at = excluded.added_at,
                container_config = excluded.container_config,
                requires_trigger = excluded.requires_trigger,
                is_main = excluded.is_main;
            """;
        command.Parameters.AddWithValue("$jid", chatJid.Value);
        command.Parameters.AddWithValue("$name", group.Name);
        command.Parameters.AddWithValue("$folder", group.Folder.Value);
        command.Parameters.AddWithValue("$trigger", group.Trigger);
        command.Parameters.AddWithValue("$addedAt", group.AddedAt.ToString("O"));
        command.Parameters.AddWithValue("$containerConfig", (object?)SqliteSerialization.SerializeContainerConfig(group.ContainerConfig) ?? DBNull.Value);
        command.Parameters.AddWithValue("$requiresTrigger", group.RequiresTrigger ? 1 : 0);
        command.Parameters.AddWithValue("$isMain", group.IsMain ? 1 : 0);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static RegisteredGroup ReadGroup(SqliteDataReader reader)
    {
        return new RegisteredGroup(
            reader.GetString(1),
            new GroupFolder(reader.GetString(2)),
            reader.GetString(3),
            DateTimeOffset.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
            SqliteSerialization.DeserializeContainerConfig(reader.IsDBNull(5) ? null : reader.GetString(5)),
            reader.GetInt64(6) == 1,
            reader.GetInt64(7) == 1);
    }
}

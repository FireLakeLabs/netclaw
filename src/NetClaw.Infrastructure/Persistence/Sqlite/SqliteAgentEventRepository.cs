using Microsoft.Data.Sqlite;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;

namespace NetClaw.Infrastructure.Persistence.Sqlite;

public sealed class SqliteAgentEventRepository : IAgentEventRepository
{
    private readonly SqliteConnectionFactory connectionFactory;

    public SqliteAgentEventRepository(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task StoreAsync(AgentActivityEvent activityEvent, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO agent_events (group_folder, chat_jid, session_id, event_kind, content, tool_name, error, is_scheduled_task, task_id, observed_at, captured_at)
            VALUES ($groupFolder, $chatJid, $sessionId, $eventKind, $content, $toolName, $error, $isScheduledTask, $taskId, $observedAt, $capturedAt);
            """;
        BindEvent(command, activityEvent);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task StoreBatchAsync(IReadOnlyList<AgentActivityEvent> events, CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteTransaction transaction = connection.BeginTransaction();

        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO agent_events (group_folder, chat_jid, session_id, event_kind, content, tool_name, error, is_scheduled_task, task_id, observed_at, captured_at)
            VALUES ($groupFolder, $chatJid, $sessionId, $eventKind, $content, $toolName, $error, $isScheduledTask, $taskId, $observedAt, $capturedAt);
            """;

        SqliteParameter pGroupFolder = command.Parameters.Add("$groupFolder", SqliteType.Text);
        SqliteParameter pChatJid = command.Parameters.Add("$chatJid", SqliteType.Text);
        SqliteParameter pSessionId = command.Parameters.Add("$sessionId", SqliteType.Text);
        SqliteParameter pEventKind = command.Parameters.Add("$eventKind", SqliteType.Text);
        SqliteParameter pContent = command.Parameters.Add("$content", SqliteType.Text);
        SqliteParameter pToolName = command.Parameters.Add("$toolName", SqliteType.Text);
        SqliteParameter pError = command.Parameters.Add("$error", SqliteType.Text);
        SqliteParameter pIsScheduledTask = command.Parameters.Add("$isScheduledTask", SqliteType.Integer);
        SqliteParameter pTaskId = command.Parameters.Add("$taskId", SqliteType.Text);
        SqliteParameter pObservedAt = command.Parameters.Add("$observedAt", SqliteType.Text);
        SqliteParameter pCapturedAt = command.Parameters.Add("$capturedAt", SqliteType.Text);
        command.Prepare();

        foreach (AgentActivityEvent activityEvent in events)
        {
            pGroupFolder.Value = (object?)activityEvent.GroupFolder ?? DBNull.Value;
            pChatJid.Value = (object?)activityEvent.ChatJid ?? DBNull.Value;
            pSessionId.Value = (object?)activityEvent.SessionId ?? DBNull.Value;
            pEventKind.Value = activityEvent.EventKind.ToString();
            pContent.Value = (object?)activityEvent.Content ?? DBNull.Value;
            pToolName.Value = (object?)activityEvent.ToolName ?? DBNull.Value;
            pError.Value = (object?)activityEvent.Error ?? DBNull.Value;
            pIsScheduledTask.Value = activityEvent.IsScheduledTask ? 1 : 0;
            pTaskId.Value = (object?)activityEvent.TaskId ?? DBNull.Value;
            pObservedAt.Value = activityEvent.ObservedAt.ToString("O");
            pCapturedAt.Value = activityEvent.CapturedAt.ToString("O");
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentActivityEvent>> GetRecentAsync(int limit = 100, DateTimeOffset? since = null, string? groupFolder = null, CancellationToken cancellationToken = default)
    {
        List<AgentActivityEvent> results = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();

        List<string> conditions = [];

        if (since is not null)
        {
            conditions.Add("observed_at > $since");
            command.Parameters.AddWithValue("$since", since.Value.ToString("O"));
        }

        if (!string.IsNullOrWhiteSpace(groupFolder))
        {
            conditions.Add("group_folder = $groupFolder");
            command.Parameters.AddWithValue("$groupFolder", groupFolder);
        }

        string whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : string.Empty;
        command.CommandText = $"SELECT id, group_folder, chat_jid, session_id, event_kind, content, tool_name, error, is_scheduled_task, task_id, observed_at, captured_at FROM agent_events {whereClause} ORDER BY observed_at DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadEvent(reader));
        }

        return results;
    }

    public async Task<IReadOnlyList<AgentActivityEvent>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        List<AgentActivityEvent> results = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id, group_folder, chat_jid, session_id, event_kind, content, tool_name, error, is_scheduled_task, task_id, observed_at, captured_at FROM agent_events WHERE session_id = $sessionId ORDER BY observed_at ASC;";
        command.Parameters.AddWithValue("$sessionId", sessionId);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadEvent(reader));
        }

        return results;
    }

    public async Task<IReadOnlyList<AgentActivityEvent>> GetByTaskRunAsync(string taskId, DateTimeOffset runAt, CancellationToken cancellationToken = default)
    {
        List<AgentActivityEvent> results = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id, group_folder, chat_jid, session_id, event_kind, content, tool_name, error, is_scheduled_task, task_id, observed_at, captured_at FROM agent_events WHERE task_id = $taskId AND observed_at >= $runAt ORDER BY observed_at ASC;";
        command.Parameters.AddWithValue("$taskId", taskId);
        command.Parameters.AddWithValue("$runAt", runAt.ToString("O"));

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadEvent(reader));
        }

        return results;
    }

    private static void BindEvent(SqliteCommand command, AgentActivityEvent activityEvent)
    {
        command.Parameters.Clear();
        command.Parameters.AddWithValue("$groupFolder", activityEvent.GroupFolder);
        command.Parameters.AddWithValue("$chatJid", activityEvent.ChatJid);
        command.Parameters.AddWithValue("$sessionId", (object?)activityEvent.SessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$eventKind", activityEvent.EventKind.ToString());
        command.Parameters.AddWithValue("$content", (object?)activityEvent.Content ?? DBNull.Value);
        command.Parameters.AddWithValue("$toolName", (object?)activityEvent.ToolName ?? DBNull.Value);
        command.Parameters.AddWithValue("$error", (object?)activityEvent.Error ?? DBNull.Value);
        command.Parameters.AddWithValue("$isScheduledTask", activityEvent.IsScheduledTask ? 1 : 0);
        command.Parameters.AddWithValue("$taskId", (object?)activityEvent.TaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$observedAt", activityEvent.ObservedAt.ToString("O"));
        command.Parameters.AddWithValue("$capturedAt", activityEvent.CapturedAt.ToString("O"));
    }

    private static AgentActivityEvent ReadEvent(SqliteDataReader reader)
    {
        return new AgentActivityEvent(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            Enum.Parse<ContainerEventKind>(reader.GetString(4), ignoreCase: true),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetInt64(8) != 0,
            reader.IsDBNull(9) ? null : reader.GetString(9),
            DateTimeOffset.Parse(reader.GetString(10), null, System.Globalization.DateTimeStyles.RoundtripKind),
            DateTimeOffset.Parse(reader.GetString(11), null, System.Globalization.DateTimeStyles.RoundtripKind));
    }
}

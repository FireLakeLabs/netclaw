using Microsoft.Data.Sqlite;
using NetClaw.Domain.Contracts.Persistence;
using NetClaw.Domain.Entities;
using NetClaw.Domain.Enums;
using NetClaw.Domain.ValueObjects;

namespace NetClaw.Infrastructure.Persistence.Sqlite;

public sealed class SqliteTaskRepository : ITaskRepository
{
    private readonly SqliteConnectionFactory connectionFactory;

    public SqliteTaskRepository(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task AppendRunLogAsync(TaskRunLog log, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO task_run_logs (task_id, run_at, duration_ms, status, result, error)
            VALUES ($taskId, $runAt, $durationMs, $status, $result, $error);
            """;
        command.Parameters.AddWithValue("$taskId", log.TaskId.Value);
        command.Parameters.AddWithValue("$runAt", log.RunAt.ToString("O"));
        command.Parameters.AddWithValue("$durationMs", Convert.ToInt64(log.Duration.TotalMilliseconds));
        command.Parameters.AddWithValue("$status", log.Status.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$result", (object?)log.Result ?? DBNull.Value);
        command.Parameters.AddWithValue("$error", (object?)log.Error ?? DBNull.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task CreateAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO scheduled_tasks (id, group_folder, chat_jid, prompt, schedule_type, schedule_value, context_mode, next_run, last_run, last_result, status, created_at)
            VALUES ($id, $groupFolder, $chatJid, $prompt, $scheduleType, $scheduleValue, $contextMode, $nextRun, $lastRun, $lastResult, $status, $createdAt);
            """;
        BindTask(command, task);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<ScheduledTask> tasks = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id, group_folder, chat_jid, prompt, schedule_type, schedule_value, context_mode, next_run, last_run, last_result, status, created_at FROM scheduled_tasks ORDER BY created_at ASC;";

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tasks.Add(ReadTask(reader));
        }

        return tasks;
    }

    public async Task<ScheduledTask?> GetByIdAsync(TaskId taskId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT id, group_folder, chat_jid, prompt, schedule_type, schedule_value, context_mode, next_run, last_run, last_result, status, created_at FROM scheduled_tasks WHERE id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", taskId.Value);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTask(reader) : null;
    }

    public async Task<IReadOnlyList<ScheduledTask>> GetDueTasksAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        List<ScheduledTask> tasks = [];

        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, group_folder, chat_jid, prompt, schedule_type, schedule_value, context_mode, next_run, last_run, last_result, status, created_at
            FROM scheduled_tasks
            WHERE status = 'active' AND next_run IS NOT NULL AND next_run <= $now
            ORDER BY next_run ASC;
            """;
        command.Parameters.AddWithValue("$now", now.ToString("O"));

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tasks.Add(ReadTask(reader));
        }

        return tasks;
    }

    public async Task UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE scheduled_tasks
            SET group_folder = $groupFolder,
                chat_jid = $chatJid,
                prompt = $prompt,
                schedule_type = $scheduleType,
                schedule_value = $scheduleValue,
                context_mode = $contextMode,
                next_run = $nextRun,
                last_run = $lastRun,
                last_result = $lastResult,
                status = $status,
                created_at = $createdAt
            WHERE id = $id;
            """;
        BindTask(command, task);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void BindTask(SqliteCommand command, ScheduledTask task)
    {
        command.Parameters.Clear();
        command.Parameters.AddWithValue("$id", task.Id.Value);
        command.Parameters.AddWithValue("$groupFolder", task.GroupFolder.Value);
        command.Parameters.AddWithValue("$chatJid", task.ChatJid.Value);
        command.Parameters.AddWithValue("$prompt", task.Prompt);
        command.Parameters.AddWithValue("$scheduleType", task.ScheduleType.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$scheduleValue", task.ScheduleValue);
        command.Parameters.AddWithValue("$contextMode", task.ContextMode.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$nextRun", (object?)task.NextRun?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastRun", (object?)task.LastRun?.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$lastResult", (object?)task.LastResult ?? DBNull.Value);
        command.Parameters.AddWithValue("$status", task.Status.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$createdAt", task.CreatedAt.ToString("O"));
    }

    private static ScheduledTask ReadTask(SqliteDataReader reader)
    {
        return new ScheduledTask(
            new TaskId(reader.GetString(0)),
            new GroupFolder(reader.GetString(1)),
            new ChatJid(reader.GetString(2)),
            reader.GetString(3),
            Enum.Parse<ScheduleType>(reader.GetString(4), ignoreCase: true),
            reader.GetString(5),
            Enum.Parse<TaskContextMode>(reader.GetString(6), ignoreCase: true),
            reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            Enum.Parse<NetClaw.Domain.Enums.TaskStatus>(reader.GetString(10), ignoreCase: true),
            DateTimeOffset.Parse(reader.GetString(11), null, System.Globalization.DateTimeStyles.RoundtripKind));
    }
}

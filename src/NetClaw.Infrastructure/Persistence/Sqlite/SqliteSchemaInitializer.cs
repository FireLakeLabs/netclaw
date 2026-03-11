using Microsoft.Data.Sqlite;

namespace NetClaw.Infrastructure.Persistence.Sqlite;

public sealed class SqliteSchemaInitializer
{
    private readonly SqliteConnectionFactory connectionFactory;

    public SqliteSchemaInitializer(SqliteConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = connectionFactory.OpenConnection();

        string[] statements =
        [
            """
            CREATE TABLE IF NOT EXISTS chats (
                jid TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                last_message_time TEXT NOT NULL,
                channel TEXT,
                is_group INTEGER DEFAULT 0
            );
            """,
            """
            CREATE TABLE IF NOT EXISTS messages (
                id TEXT NOT NULL,
                chat_jid TEXT NOT NULL,
                sender TEXT NOT NULL,
                sender_name TEXT NOT NULL,
                content TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                is_from_me INTEGER NOT NULL,
                is_bot_message INTEGER DEFAULT 0,
                PRIMARY KEY (id, chat_jid),
                FOREIGN KEY (chat_jid) REFERENCES chats(jid)
            );
            """,
            "CREATE INDEX IF NOT EXISTS idx_messages_timestamp ON messages(timestamp);",
            """
            CREATE TABLE IF NOT EXISTS scheduled_tasks (
                id TEXT PRIMARY KEY,
                group_folder TEXT NOT NULL,
                chat_jid TEXT NOT NULL,
                prompt TEXT NOT NULL,
                schedule_type TEXT NOT NULL,
                schedule_value TEXT NOT NULL,
                context_mode TEXT NOT NULL DEFAULT 'isolated',
                next_run TEXT,
                last_run TEXT,
                last_result TEXT,
                status TEXT NOT NULL DEFAULT 'active',
                created_at TEXT NOT NULL
            );
            """,
            "CREATE INDEX IF NOT EXISTS idx_scheduled_tasks_next_run ON scheduled_tasks(next_run);",
            "CREATE INDEX IF NOT EXISTS idx_scheduled_tasks_status ON scheduled_tasks(status);",
            """
            CREATE TABLE IF NOT EXISTS task_run_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                task_id TEXT NOT NULL,
                run_at TEXT NOT NULL,
                duration_ms INTEGER NOT NULL,
                status TEXT NOT NULL,
                result TEXT,
                error TEXT,
                FOREIGN KEY (task_id) REFERENCES scheduled_tasks(id)
            );
            """,
            "CREATE INDEX IF NOT EXISTS idx_task_run_logs_task_id_run_at ON task_run_logs(task_id, run_at);",
            "CREATE TABLE IF NOT EXISTS router_state (key TEXT PRIMARY KEY, value TEXT NOT NULL);",
            "CREATE TABLE IF NOT EXISTS sessions (group_folder TEXT PRIMARY KEY, session_id TEXT NOT NULL);",
            """
            CREATE TABLE IF NOT EXISTS registered_groups (
                jid TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                folder TEXT NOT NULL UNIQUE,
                trigger_pattern TEXT NOT NULL,
                added_at TEXT NOT NULL,
                container_config TEXT,
                requires_trigger INTEGER DEFAULT 1,
                is_main INTEGER DEFAULT 0
            );
            """
        ];

        foreach (string statement in statements)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}

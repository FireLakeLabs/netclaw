using Microsoft.Data.Sqlite;

namespace FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite;

public sealed class SqliteConnectionFactory
{
    public SqliteConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQLite connection string is required.", nameof(connectionString));
        }

        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(ConnectionString);
        connection.Open();

        using SqliteCommand pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA foreign_keys = ON;";
        pragmaCommand.ExecuteNonQuery();

        return connection;
    }
}

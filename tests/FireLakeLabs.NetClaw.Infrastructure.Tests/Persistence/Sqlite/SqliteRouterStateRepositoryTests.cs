using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.Sqlite;

public sealed class SqliteRouterStateRepositoryTests
{
    [Fact]
    public async Task RouterStateRepository_UpsertsAndReadsValues()
    {
        await using TestSqliteDatabase database = new();
        await database.SchemaInitializer.InitializeAsync();

        SqliteRouterStateRepository repository = new(database.ConnectionFactory);
        RouterStateEntry entry = new("last_timestamp", "2026-03-10T00:00:00Z");

        await repository.SetAsync(entry);
        RouterStateEntry? storedEntry = await repository.GetAsync("last_timestamp");

        Assert.NotNull(storedEntry);
        Assert.Equal(entry.Value, storedEntry!.Value);
    }
}

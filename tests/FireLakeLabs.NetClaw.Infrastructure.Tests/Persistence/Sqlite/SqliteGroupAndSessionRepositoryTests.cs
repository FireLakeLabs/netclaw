using FireLakeLabs.NetClaw.Domain.Entities;
using FireLakeLabs.NetClaw.Domain.ValueObjects;
using FireLakeLabs.NetClaw.Infrastructure.Persistence.Sqlite;

namespace FireLakeLabs.NetClaw.Infrastructure.Tests.Persistence.Sqlite;

public sealed class SqliteGroupAndSessionRepositoryTests
{
    [Fact]
    public async Task GroupRepository_UpsertsAndReadsRegisteredGroups()
    {
        await using TestSqliteDatabase database = new();
        await database.SchemaInitializer.InitializeAsync();

        SqliteGroupRepository repository = new(database.ConnectionFactory);
        RegisteredGroup group = new(
            "Main",
            new GroupFolder("main"),
            "@Andy",
            DateTimeOffset.UtcNow,
            new ContainerConfig([new AdditionalMount("/tmp")], TimeSpan.FromMinutes(10)),
            requiresTrigger: false,
            isMain: true);

        await repository.UpsertAsync(new ChatJid("main@jid"), group);

        RegisteredGroup? readGroup = await repository.GetByJidAsync(new ChatJid("main@jid"));
        IReadOnlyDictionary<ChatJid, RegisteredGroup> allGroups = await repository.GetAllAsync();

        Assert.NotNull(readGroup);
        Assert.True(readGroup!.IsMain);
        Assert.Single(allGroups);
        Assert.NotNull(readGroup.ContainerConfig);
    }

    [Fact]
    public async Task SessionRepository_UpsertsAndReadsSessions()
    {
        await using TestSqliteDatabase database = new();
        await database.SchemaInitializer.InitializeAsync();

        SqliteSessionRepository repository = new(database.ConnectionFactory);
        SessionState sessionState = new(new GroupFolder("team"), new SessionId("session-1"));

        await repository.UpsertAsync(sessionState);

        SessionId? sessionId = await repository.GetByGroupFolderAsync(new GroupFolder("team"));
        IReadOnlyDictionary<GroupFolder, SessionId> allSessions = await repository.GetAllAsync();

        Assert.NotNull(sessionId);
        Assert.Equal("session-1", sessionId!.Value.Value);
        Assert.Single(allSessions);
    }
}

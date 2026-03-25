# Step 03: SQLite Layer

## Changes Made

- Added `Microsoft.Data.Sqlite` to `FireLakeLabs.NetClaw.Infrastructure`.
- Implemented a reusable SQLite connection factory with foreign-key enforcement.
- Implemented a schema initializer that creates the core NetClaw tables and indexes:
  - chats
  - messages
  - scheduled tasks
  - task run logs
  - router state
  - sessions
  - registered groups
- Implemented JSON serialization helpers for persisted container configuration.
- Implemented SQLite-backed repository classes for the Domain persistence contracts:
  - `SqliteMessageRepository`
  - `SqliteGroupRepository`
  - `SqliteSessionRepository`
  - `SqliteTaskRepository`
  - `SqliteRouterStateRepository`
- Added repository tests using isolated temporary SQLite databases per test run.

## Files And Projects Affected

- Production:
  - `src/FireLakeLabs.NetClaw.Infrastructure/FireLakeLabs.NetClaw.Infrastructure.csproj`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Persistence/Sqlite/*`
- Tests:
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/AssemblySmokeTests.cs`
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/Persistence/Sqlite/*`

## Unit Tests And Integration Tests Added

- `SqliteSchemaInitializerTests`
  - Verifies creation of the expected database tables.
- `SqliteMessageRepositoryTests`
  - Verifies chat metadata storage, message storage, filtering of bot messages, and retrieval of new messages.
- `SqliteGroupAndSessionRepositoryTests`
  - Verifies registered-group upsert/read behavior and session upsert/read behavior.
- `SqliteTaskRepositoryTests`
  - Verifies task creation, due-task querying, task updates, and run-log persistence.
- `SqliteRouterStateRepositoryTests`
  - Verifies router-state upsert and retrieval.
- `AssemblySmokeTests`
  - Verifies the Infrastructure assembly now exposes the SQLite persistence types.

## Verification Performed

- `dotnet build tests/FireLakeLabs.NetClaw.Infrastructure.Tests/FireLakeLabs.NetClaw.Infrastructure.Tests.csproj`
- `dotnet test tests/FireLakeLabs.NetClaw.Infrastructure.Tests/FireLakeLabs.NetClaw.Infrastructure.Tests.csproj --no-build`

Result: 8 xUnit tests passed.

## Deferred Items And Known Gaps

- The SQLite layer is implemented, but dependency injection and runtime configuration binding are not in place yet.
- Filesystem abstractions, path policies, mount-allowlist loading, and platform detection still belong to the next infrastructure step.
- No application services consume the repositories yet.
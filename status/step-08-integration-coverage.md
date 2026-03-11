# Step 08: Integration Coverage

## Changes Made

- Expanded the integration test suite from bootstrap smoke coverage to real cross-project flows.
- Added setup-to-host coverage proving that group registration written by the Setup CLI is visible through Host-resolved SQLite repositories.
- Added IPC-to-scheduler coverage proving that tasks created through the Application IPC processor are executed by the Host scheduler and persisted to SQLite run logs.
- Added end-to-end setup verification coverage proving that registration, mount allowlist configuration, service artifact generation, and credentials combine into a successful verification result.

## Files And Projects Affected

- Tests:
  - `tests/NetClaw.IntegrationTests/EndToEndIntegrationTests.cs`

## Unit Tests And Integration Tests Added

- `EndToEndIntegrationTests`
  - verifies setup registration is visible through host repository resolution
  - verifies IPC task creation and host scheduler execution persist task lifecycle data into SQLite
  - verifies setup verification reports success when bootstrap artifacts are present

## Verification Performed

- `dotnet build tests/NetClaw.IntegrationTests/NetClaw.IntegrationTests.csproj`
- `dotnet test tests/NetClaw.IntegrationTests/NetClaw.IntegrationTests.csproj`

Result: 4 xUnit integration tests passed.

## Deferred Items And Known Gaps

- Integration coverage still uses the deferred task execution delegate rather than a real in-container agent runtime.
- Live channel integrations, service activation with `systemctl`, and container image execution remain outside automated test coverage.
- Step 09 remains blocked on the explicit runtime integration decision that was deferred earlier in the migration plan.
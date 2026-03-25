# Step 10: File-Based IPC Watcher

## Changes Made

- Added a poll-based file-system IPC watcher that scans per-group IPC directories under `data/ipc`, parses NanoClaw-style JSON command files, and dispatches them through `IIpcCommandProcessor`.
- Added host-owned IPC watcher configuration with a dedicated poll interval and registered the watcher behind a provider-neutral `IIpcCommandWatcher` contract.
- Wired a new hosted worker into the Host so pending IPC files are processed continuously during normal runtime.
- Extended the filesystem abstraction with file move and delete operations so processed IPC files can be removed and malformed files can be quarantined.
- Implemented failure quarantine by moving invalid or failed IPC files into `data/ipc/errors` instead of silently discarding them.
- Added end-to-end coverage proving that a task JSON file written into a group IPC folder becomes a persisted scheduled task through the running host.

## Files And Projects Affected

- Production:
  - `src/FireLakeLabs.NetClaw.Domain/Contracts/Services/ServiceContracts.cs`
  - `src/FireLakeLabs.NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs`
  - `src/FireLakeLabs.NetClaw.Host/Services/IpcWatcherWorker.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Configuration/IpcWatcherOptions.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/FileSystem/IFileSystem.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/FileSystem/PhysicalFileSystem.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Ipc/FileSystemIpcWatcher.cs`
- Tests:
  - `tests/FireLakeLabs.NetClaw.Host.Tests/ProgramTests.cs`
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/AssemblySmokeTests.cs`
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/Configuration/OptionsTests.cs`
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/FileSystem/PhysicalFileSystemTests.cs`
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/Ipc/FileSystemIpcWatcherTests.cs`
  - `tests/FireLakeLabs.NetClaw.IntegrationTests/EndToEndIntegrationTests.cs`

## Unit Tests And Integration Tests Added

- `FileSystemIpcWatcherTests`
  - verifies message and task files are parsed and dispatched
  - verifies main-group detection and `register_group` payload parsing including `containerConfig`
  - verifies malformed files are quarantined into `data/ipc/errors`
- `OptionsTests`
  - verifies IPC watcher poll interval validation
- `PhysicalFileSystemTests`
  - verifies file move and delete operations used by IPC cleanup
- `ProgramTests`
  - verifies the host registers `IIpcCommandWatcher` and binds IPC watcher options
- `EndToEndIntegrationTests`
  - verifies a task file dropped into a group IPC directory is polled and persisted as a scheduled task

## Verification Performed

- `dotnet test tests/FireLakeLabs.NetClaw.Infrastructure.Tests/FireLakeLabs.NetClaw.Infrastructure.Tests.csproj`
- `dotnet test tests/FireLakeLabs.NetClaw.Host.Tests/FireLakeLabs.NetClaw.Host.Tests.csproj`
- `dotnet test tests/FireLakeLabs.NetClaw.IntegrationTests/FireLakeLabs.NetClaw.IntegrationTests.csproj`
- `dotnet test FireLakeLabs.NetClaw.slnx`

Result: 97 xUnit tests passed across the solution.

## Deferred Items And Known Gaps

- IPC support still covers the currently modeled command set: `message`, `schedule_task`, and `register_group`.
- Interactive follow-up input and long-lived active agent sessions are still not wired through the group execution queue.
- Channel migration and real inbound channel polling remain future increments beyond this watcher-based host plumbing.
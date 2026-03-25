# Step 02: Domain Contracts

## Changes Made

- Implemented the first-pass NetClaw domain model under `src/FireLakeLabs.NetClaw.Domain`.
- Added domain enums for scheduling, task lifecycle, task context, and container run status.
- Added validated value objects for:
  - `GroupFolder`
  - `ChatJid`
  - `TaskId`
  - `SessionId`
  - `ContainerName`
  - `ChannelName`
- Added domain entities and supporting records for:
  - registered groups
  - scheduled tasks
  - task run logs
  - stored messages
  - chats
  - router state entries
  - session state
  - container config and additional mounts
  - mount allowlist and allowed roots
- Added channel contracts, container contracts, IPC contracts, persistence interfaces, and service interfaces required for the later infrastructure and application steps.
- Preserved narrow contracts for the future agent runtime and container execution boundaries.

## Files And Projects Affected

- Production:
  - `src/FireLakeLabs.NetClaw.Domain/Enums/*`
  - `src/FireLakeLabs.NetClaw.Domain/ValueObjects/*`
  - `src/FireLakeLabs.NetClaw.Domain/Entities/*`
  - `src/FireLakeLabs.NetClaw.Domain/Contracts/Channels/*`
  - `src/FireLakeLabs.NetClaw.Domain/Contracts/Containers/*`
  - `src/FireLakeLabs.NetClaw.Domain/Contracts/Ipc/*`
  - `src/FireLakeLabs.NetClaw.Domain/Contracts/Persistence/*`
  - `src/FireLakeLabs.NetClaw.Domain/Contracts/Services/*`
- Tests:
  - `tests/FireLakeLabs.NetClaw.Domain.Tests/AssemblySmokeTests.cs`
  - `tests/FireLakeLabs.NetClaw.Domain.Tests/ValueObjects/*`
  - `tests/FireLakeLabs.NetClaw.Domain.Tests/Entities/*`

## Unit Tests And Integration Tests Added

- `GroupFolderTests`
  - Valid folder acceptance
  - Reserved name rejection
  - path traversal and invalid pattern rejection
- `SimpleIdentifierTests`
  - whitespace trimming and rejection rules for identifiers and names
- `RegisteredGroupTests`
  - main-group trigger invariant
  - name validation
  - container config preservation
- `ScheduledTaskTests`
  - valid task construction
  - prompt validation
  - last-result normalization
- `StoredMessageAndRunLogTests`
  - stored message content validation
  - failed-run error requirement
  - successful run acceptance
- `AuxiliaryEntityTests`
  - chat info validation
  - router state validation
  - container timeout validation
  - session state preservation
  - mount allowlist preservation
- `AssemblySmokeTests`
  - verifies the domain assembly exposes the expected core contract namespaces

## Verification Performed

- `dotnet build src/FireLakeLabs.NetClaw.Domain/FireLakeLabs.NetClaw.Domain.csproj`
- `dotnet build tests/FireLakeLabs.NetClaw.Domain.Tests/FireLakeLabs.NetClaw.Domain.Tests.csproj`
- `dotnet test tests/FireLakeLabs.NetClaw.Domain.Tests/FireLakeLabs.NetClaw.Domain.Tests.csproj --no-build`

Result: 32 xUnit tests passed.

## Deferred Items And Known Gaps

- Persistence interfaces are defined, but no SQLite implementation exists yet.
- Service interfaces are defined, but no application-level behavior exists yet.
- IPC and container contracts exist, but serialization and filesystem behavior are not implemented yet.
- Channel contracts are defined, but no channel adapters are implemented yet.
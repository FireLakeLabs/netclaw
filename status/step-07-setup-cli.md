# Step 07: Setup CLI

## Changes Made

- Replaced the placeholder Setup entry point with an async CLI that parses `--step` commands and emits structured status blocks.
- Added reusable setup path resolution for project storage, SQLite database, user config, mount allowlist, service units, and launcher scripts.
- Added Linux-first setup steps for:
  - `environment`
  - `register`
  - `mounts`
  - `service`
  - `verify`
- Implemented group registration against the SQLite persistence layer and automatic creation of group and log directories.
- Implemented mount allowlist writing under the user config directory with JSON validation.
- Implemented service artifact generation for user/system systemd units and a script-based fallback launcher.
- Implemented setup verification covering database presence, registered groups, allowlist presence, credentials, and service configuration.

## Files And Projects Affected

- Production:
  - `src/FireLakeLabs.NetClaw.Setup/Program.cs`
  - `src/FireLakeLabs.NetClaw.Setup/SetupCommand.cs`
  - `src/FireLakeLabs.NetClaw.Setup/SetupPaths.cs`
  - `src/FireLakeLabs.NetClaw.Setup/SetupResult.cs`
  - `src/FireLakeLabs.NetClaw.Setup/SetupRunner.cs`
  - `src/FireLakeLabs.NetClaw.Setup/SetupStatusWriter.cs`
- Tests:
  - `tests/FireLakeLabs.NetClaw.Setup.Tests/ProgramTests.cs`

## Unit Tests And Integration Tests Added

- `ProgramTests`
  - verifies the register step creates storage directories and persists group metadata
  - verifies the mounts step writes the allowlist config file
  - verifies the service step writes the launcher script
  - verifies the verify step reports success when credentials, groups, service config, and allowlist are present

## Verification Performed

- `dotnet build tests/FireLakeLabs.NetClaw.Setup.Tests/FireLakeLabs.NetClaw.Setup.Tests.csproj`
- `dotnet test tests/FireLakeLabs.NetClaw.Setup.Tests/FireLakeLabs.NetClaw.Setup.Tests.csproj`

Result: 4 xUnit tests passed.

## Deferred Items And Known Gaps

- The setup CLI does not yet build container images or perform live channel group sync.
- Service installation currently writes service artifacts and recommended commands, but it does not invoke `systemctl` automatically.
- The mount allowlist step validates JSON structure but does not yet enforce a richer schema beyond requiring a JSON object.
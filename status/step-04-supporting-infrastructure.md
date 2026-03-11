# Step 04: Supporting Infrastructure

## Changes Made

- Added configuration option types for assistant identity, credential proxy, container runtime, scheduler timing, and storage roots.
- Added a physical filesystem abstraction to support testable infrastructure code.
- Added a path resolver for group directories and IPC directories rooted under the configured storage paths.
- Added a mount allowlist loader for external JSON configuration.
- Added a mount security validator that enforces allowlisted roots, blocked patterns, and non-main read-only behavior.
- Added runtime support primitives:
  - command runner abstraction
  - process-based command runner
  - platform info model
  - platform detection service
  - Docker-backed container runtime adapter implementing the Domain runtime contract

## Files And Projects Affected

- Production:
  - `src/NetClaw.Infrastructure/Configuration/*`
  - `src/NetClaw.Infrastructure/FileSystem/*`
  - `src/NetClaw.Infrastructure/Paths/*`
  - `src/NetClaw.Infrastructure/Security/*`
  - `src/NetClaw.Infrastructure/Runtime/*`
- Tests:
  - `tests/NetClaw.Infrastructure.Tests/Configuration/*`
  - `tests/NetClaw.Infrastructure.Tests/FileSystem/*`
  - `tests/NetClaw.Infrastructure.Tests/Paths/*`
  - `tests/NetClaw.Infrastructure.Tests/Security/*`
  - `tests/NetClaw.Infrastructure.Tests/Runtime/*`

## Unit Tests And Integration Tests Added

- `OptionsTests`
  - verifies storage path creation
  - verifies scheduler validation
  - verifies credential proxy option validation
- `PhysicalFileSystemTests`
  - verifies write/read round-trip behavior
- `GroupPathResolverTests`
  - verifies group and IPC path resolution under the expected base directories
- `MountSecurityTests`
  - verifies default allowlist behavior for missing files
  - verifies JSON allowlist loading
  - verifies allowlist rejection and non-main read-only enforcement
- `PlatformAndRuntimeTests`
  - verifies platform detection semantics
  - verifies Docker runtime command usage and Linux host-gateway behavior

## Verification Performed

- `dotnet build tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj`
- `dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj --no-build`

Result: 20 xUnit tests passed.

## Deferred Items And Known Gaps

- Dependency injection wiring for these infrastructure services is not in place yet.
- The command runner and Docker runtime adapter exist, but they are not yet consumed by host orchestration.
- Configuration binding from appsettings/environment variables will be wired when the host and setup projects are implemented.
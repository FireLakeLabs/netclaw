# Step 06: Host Composition

## Changes Made

- Wired the Host project as the composition root for Domain, Application, and Infrastructure services.
- Added host path resolution for project root, SQLite database location, and mount allowlist location.
- Registered configuration-backed option objects for assistant identity, credential proxy, container runtime, scheduler timing, and storage roots.
- Registered SQLite repositories, runtime services, filesystem helpers, and Application-layer orchestration services in dependency injection.
- Added a startup initialization hosted service that creates required directories, validates the mount allowlist file, and initializes the SQLite schema.
- Added a scheduler background worker that polls for due tasks on a configurable interval.

## Files And Projects Affected

- Production:
  - `src/FireLakeLabs.NetClaw.Host/Program.cs`
  - `src/FireLakeLabs.NetClaw.Host/Configuration/*`
  - `src/FireLakeLabs.NetClaw.Host/DependencyInjection/*`
  - `src/FireLakeLabs.NetClaw.Host/Services/*`
- Tests:
  - `tests/FireLakeLabs.NetClaw.Host.Tests/ProgramTests.cs`

## Unit Tests And Integration Tests Added

- `ProgramTests`
  - verifies the host builds, starts, creates storage directories, and initializes the SQLite database
  - verifies core repositories, application services, runtime services, and configuration objects resolve from dependency injection

## Verification Performed

- `dotnet build tests/FireLakeLabs.NetClaw.Host.Tests/FireLakeLabs.NetClaw.Host.Tests.csproj`
- `dotnet test tests/FireLakeLabs.NetClaw.Host.Tests/FireLakeLabs.NetClaw.Host.Tests.csproj`

Result: 2 xUnit tests passed.

## Deferred Items And Known Gaps

- Outbound channel registrations are still empty, so real message delivery is not wired yet.
- Scheduled task execution currently uses a deferred delegate until the final agent runtime integration is implemented.
- Host configuration is code-driven for now; richer appsettings templates and setup-driven config generation remain for later steps.
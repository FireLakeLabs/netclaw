# Step 01: Bootstrap

## Changes Made

- Created the root .NET 10 solution file `FireLakeLabs.NetClaw.slnx`.
- Created the production projects under `src/`:
  - `FireLakeLabs.NetClaw.Domain`
  - `FireLakeLabs.NetClaw.Application`
  - `FireLakeLabs.NetClaw.Infrastructure`
  - `FireLakeLabs.NetClaw.Host`
  - `FireLakeLabs.NetClaw.Setup`
- Created the xUnit test projects under `tests/`:
  - `FireLakeLabs.NetClaw.Domain.Tests`
  - `FireLakeLabs.NetClaw.Application.Tests`
  - `FireLakeLabs.NetClaw.Infrastructure.Tests`
  - `FireLakeLabs.NetClaw.Host.Tests`
  - `FireLakeLabs.NetClaw.Setup.Tests`
  - `FireLakeLabs.NetClaw.IntegrationTests`
- Added the initial project reference graph so application, infrastructure, host, setup, and tests are wired together.
- Added `Directory.Build.props` to centralize shared build settings for the repository.
- Replaced template executable code in `FireLakeLabs.NetClaw.Host` and `FireLakeLabs.NetClaw.Setup` with minimal, testable entry points.
- Removed template placeholder files from the production and test projects.
- Added assembly marker types to the Domain, Application, and Infrastructure projects to support initial smoke tests.
- Updated `README.md` with the repository layout and bootstrap conventions.

## Files And Projects Affected

- Root:
  - `Directory.Build.props`
  - `FireLakeLabs.NetClaw.slnx`
  - `README.md`
- Production projects:
  - `src/FireLakeLabs.NetClaw.Domain`
  - `src/FireLakeLabs.NetClaw.Application`
  - `src/FireLakeLabs.NetClaw.Infrastructure`
  - `src/FireLakeLabs.NetClaw.Host`
  - `src/FireLakeLabs.NetClaw.Setup`
- Test projects:
  - `tests/FireLakeLabs.NetClaw.Domain.Tests`
  - `tests/FireLakeLabs.NetClaw.Application.Tests`
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests`
  - `tests/FireLakeLabs.NetClaw.Host.Tests`
  - `tests/FireLakeLabs.NetClaw.Setup.Tests`
  - `tests/FireLakeLabs.NetClaw.IntegrationTests`

## Unit Tests And Integration Tests Added

- `tests/FireLakeLabs.NetClaw.Domain.Tests/AssemblySmokeTests.cs`
  - Verifies the domain assembly marker resolves from `FireLakeLabs.NetClaw.Domain`.
- `tests/FireLakeLabs.NetClaw.Application.Tests/AssemblySmokeTests.cs`
  - Verifies the application assembly marker resolves from `FireLakeLabs.NetClaw.Application`.
- `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/AssemblySmokeTests.cs`
  - Verifies the infrastructure assembly marker resolves from `FireLakeLabs.NetClaw.Infrastructure`.
- `tests/FireLakeLabs.NetClaw.Host.Tests/ProgramTests.cs`
  - Verifies `FireLakeLabs.NetClaw.Host.Program.CreateHostBuilder` builds, starts, and stops a host successfully.
- `tests/FireLakeLabs.NetClaw.Setup.Tests/ProgramTests.cs`
  - Verifies `FireLakeLabs.NetClaw.Setup.Program.Main` returns a success exit code.
- `tests/FireLakeLabs.NetClaw.IntegrationTests/BootstrapCompositionTests.cs`
  - Verifies the bootstrap solution exposes the expected assemblies across Domain, Application, Infrastructure, Host, and Setup.

## Verification Performed

- `dotnet restore FireLakeLabs.NetClaw.slnx`
- `dotnet build FireLakeLabs.NetClaw.slnx`
- `dotnet test FireLakeLabs.NetClaw.slnx --no-build`

All projects built successfully and all bootstrap xUnit tests passed.

## Deferred Items And Known Gaps

- Domain contracts have not been implemented yet beyond bootstrap marker types.
- No SQLite schema or repository logic exists yet.
- No application orchestration services exist yet.
- No real host services, setup commands, or external integrations exist yet.
- The final in-container agent runtime remains intentionally deferred.
# Step 01: Bootstrap

## Changes Made

- Created the root .NET 10 solution file `NetClaw.slnx`.
- Created the production projects under `src/`:
  - `NetClaw.Domain`
  - `NetClaw.Application`
  - `NetClaw.Infrastructure`
  - `NetClaw.Host`
  - `NetClaw.Setup`
- Created the xUnit test projects under `tests/`:
  - `NetClaw.Domain.Tests`
  - `NetClaw.Application.Tests`
  - `NetClaw.Infrastructure.Tests`
  - `NetClaw.Host.Tests`
  - `NetClaw.Setup.Tests`
  - `NetClaw.IntegrationTests`
- Added the initial project reference graph so application, infrastructure, host, setup, and tests are wired together.
- Added `Directory.Build.props` to centralize shared build settings for the repository.
- Replaced template executable code in `NetClaw.Host` and `NetClaw.Setup` with minimal, testable entry points.
- Removed template placeholder files from the production and test projects.
- Added assembly marker types to the Domain, Application, and Infrastructure projects to support initial smoke tests.
- Updated `README.md` with the repository layout and bootstrap conventions.

## Files And Projects Affected

- Root:
  - `Directory.Build.props`
  - `NetClaw.slnx`
  - `README.md`
- Production projects:
  - `src/NetClaw.Domain`
  - `src/NetClaw.Application`
  - `src/NetClaw.Infrastructure`
  - `src/NetClaw.Host`
  - `src/NetClaw.Setup`
- Test projects:
  - `tests/NetClaw.Domain.Tests`
  - `tests/NetClaw.Application.Tests`
  - `tests/NetClaw.Infrastructure.Tests`
  - `tests/NetClaw.Host.Tests`
  - `tests/NetClaw.Setup.Tests`
  - `tests/NetClaw.IntegrationTests`

## Unit Tests And Integration Tests Added

- `tests/NetClaw.Domain.Tests/AssemblySmokeTests.cs`
  - Verifies the domain assembly marker resolves from `NetClaw.Domain`.
- `tests/NetClaw.Application.Tests/AssemblySmokeTests.cs`
  - Verifies the application assembly marker resolves from `NetClaw.Application`.
- `tests/NetClaw.Infrastructure.Tests/AssemblySmokeTests.cs`
  - Verifies the infrastructure assembly marker resolves from `NetClaw.Infrastructure`.
- `tests/NetClaw.Host.Tests/ProgramTests.cs`
  - Verifies `NetClaw.Host.Program.CreateHostBuilder` builds, starts, and stops a host successfully.
- `tests/NetClaw.Setup.Tests/ProgramTests.cs`
  - Verifies `NetClaw.Setup.Program.Main` returns a success exit code.
- `tests/NetClaw.IntegrationTests/BootstrapCompositionTests.cs`
  - Verifies the bootstrap solution exposes the expected assemblies across Domain, Application, Infrastructure, Host, and Setup.

## Verification Performed

- `dotnet restore NetClaw.slnx`
- `dotnet build NetClaw.slnx`
- `dotnet test NetClaw.slnx --no-build`

All projects built successfully and all bootstrap xUnit tests passed.

## Deferred Items And Known Gaps

- Domain contracts have not been implemented yet beyond bootstrap marker types.
- No SQLite schema or repository logic exists yet.
- No application orchestration services exist yet.
- No real host services, setup commands, or external integrations exist yet.
- The final in-container agent runtime remains intentionally deferred.
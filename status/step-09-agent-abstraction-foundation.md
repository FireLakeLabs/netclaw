# Step 09A: Agent Abstraction Foundation

## Changes Made

- Added provider-neutral Domain contracts for coding-agent execution, sessions, workspace context, instructions, tools, stream events, and capability profiles.
- Added provider enums for agent provider identity and normalized agent event kinds.
- Added runtime configuration for selecting a default coding-agent provider while preserving the container-boundary design.
- Extended group path resolution to include per-group session and agent-workspace roots.
- Added Infrastructure placeholders for Copilot, Claude Code, Codex, and OpenCode engines to telegraph the multi-provider direction.
- Added a provider-neutral workspace builder that projects NetClaw instruction state into generated `AGENTS.md` documents.
- Added a provider-neutral tool registry describing the NetClaw-owned tool surface.
- Replaced the Host’s ad hoc deferred task executor with a structured `IAgentRuntime` facade built on the new coding-agent abstraction.

## Files And Projects Affected

- Production:
  - `src/NetClaw.Domain/Contracts/Agents/*`
  - `src/NetClaw.Domain/Enums/Agent*`
  - `src/NetClaw.Domain/Contracts/Services/ServiceContracts.cs`
  - `src/NetClaw.Infrastructure/Configuration/AgentRuntimeOptions.cs`
  - `src/NetClaw.Infrastructure/Paths/GroupPathResolver.cs`
  - `src/NetClaw.Infrastructure/Runtime/Agents/*`
  - `src/NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs`
- Tests:
  - `tests/NetClaw.Domain.Tests/Contracts/*`
  - `tests/NetClaw.Domain.Tests/AssemblySmokeTests.cs`
  - `tests/NetClaw.Infrastructure.Tests/Configuration/OptionsTests.cs`
  - `tests/NetClaw.Infrastructure.Tests/Paths/GroupPathResolverTests.cs`
  - `tests/NetClaw.Infrastructure.Tests/Runtime/*`
  - `tests/NetClaw.Infrastructure.Tests/AssemblySmokeTests.cs`
  - `tests/NetClaw.Host.Tests/ProgramTests.cs`

## Unit Tests And Integration Tests Added

- `AgentContractsTests`
  - verifies provider-neutral agent contracts validate session IDs and tool definitions
  - verifies execution requests preserve workspace and instruction metadata
- `OptionsTests`
  - verifies agent runtime provider parsing and invalid-provider rejection
- `GroupPathResolverTests`
  - verifies session and agent-workspace path resolution under the data root
- `AgentRuntimeServicesTests`
  - verifies the Copilot placeholder advertises the new abstraction surface
  - verifies workspace generation includes `AGENTS.md`
  - verifies the structured agent runtime persists returned sessions
- `ProgramTests`
  - verifies the Host resolves the new agent services and default provider configuration

## Verification Performed

- `dotnet build tests/NetClaw.Domain.Tests/NetClaw.Domain.Tests.csproj`
- `dotnet test tests/NetClaw.Domain.Tests/NetClaw.Domain.Tests.csproj`
- `dotnet build tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj`
- `dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj`
- `dotnet build tests/NetClaw.Host.Tests/NetClaw.Host.Tests.csproj`
- `dotnet test tests/NetClaw.Host.Tests/NetClaw.Host.Tests.csproj`
- `dotnet build tests/NetClaw.IntegrationTests/NetClaw.IntegrationTests.csproj`
- `dotnet test tests/NetClaw.IntegrationTests/NetClaw.IntegrationTests.csproj`
- `dotnet test NetClaw.slnx`

Result: 87 xUnit tests passed across the solution.

## Deferred Items And Known Gaps

- The Copilot engine is still a placeholder; the actual SDK-backed adapter is a later part of step 09.
- The Host now uses a structured runtime seam, but interactive execution still needs to be wired through that same abstraction.
- `AGENTS.md` is generated at the abstraction layer but is not yet written into a live runtime workspace on disk.
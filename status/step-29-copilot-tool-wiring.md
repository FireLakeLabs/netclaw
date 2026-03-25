# Step 29: Copilot Tool Wiring

## Changes Made

- Added a dedicated Copilot tool factory that maps NetClaw's provider-neutral tool registry onto concrete Copilot SDK `AIFunction` handlers.
- Wired the resulting tool list into both new-session and resume-session Copilot configuration so live conversations can actually invoke scheduler and control-plane actions.
- Implemented real handlers for the currently declared runtime tools, including persisted task creation for `schedule_group_task`.
- Expanded runtime tests to verify both tool attachment and scheduler task persistence through the new bridge.

## Files And Projects Affected

- Production:
  - `src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/NetClawCopilotToolFactory.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/CopilotCodingAgentEngine.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/CopilotSdkAbstractions.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/NetClawAgentToolRegistry.cs`
  - `src/FireLakeLabs.NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs`
  - `README.md`
- Tests:
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/Runtime/AgentRuntimeServicesTests.cs`

## Verification

- `dotnet test tests/FireLakeLabs.NetClaw.Infrastructure.Tests/FireLakeLabs.NetClaw.Infrastructure.Tests.csproj --filter "AgentRuntimeServicesTests"`
- `dotnet test`
# Step 29: Copilot Tool Wiring

## Changes Made

- Added a dedicated Copilot tool factory that maps NetClaw's provider-neutral tool registry onto concrete Copilot SDK `AIFunction` handlers.
- Wired the resulting tool list into both new-session and resume-session Copilot configuration so live conversations can actually invoke scheduler and control-plane actions.
- Implemented real handlers for the currently declared runtime tools, including persisted task creation for `schedule_group_task`.
- Expanded runtime tests to verify both tool attachment and scheduler task persistence through the new bridge.

## Files And Projects Affected

- Production:
  - `src/NetClaw.Infrastructure/Runtime/Agents/NetClawCopilotToolFactory.cs`
  - `src/NetClaw.Infrastructure/Runtime/Agents/CopilotCodingAgentEngine.cs`
  - `src/NetClaw.Infrastructure/Runtime/Agents/CopilotSdkAbstractions.cs`
  - `src/NetClaw.Infrastructure/Runtime/Agents/NetClawAgentToolRegistry.cs`
  - `src/NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs`
  - `README.md`
- Tests:
  - `tests/NetClaw.Infrastructure.Tests/Runtime/AgentRuntimeServicesTests.cs`

## Verification

- `dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj --filter "AgentRuntimeServicesTests"`
- `dotnet test`
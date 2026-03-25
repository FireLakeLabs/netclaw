# Step 30: Scheduling Management Tools

## Changes Made

- Added the missing scheduler management tools to the live Copilot tool surface: task listing, pause, resume, and cancel.
- Implemented task-level authorization so normal groups can manage only their own tasks while the main group can manage tasks across groups.
- Returned structured task metadata from the new tools so live chat sessions can inspect status, next run, and group ownership directly.
- Preserved cancelled tasks as `cancelled` records instead of deleting them so NetClaw keeps task history for inspection.
- Expanded runtime coverage for cross-group listing, task status mutation, and non-main authorization failures.

## Files And Projects Affected

- Production:
  - `src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/NetClawAgentToolRegistry.cs`
  - `src/FireLakeLabs.NetClaw.Infrastructure/Runtime/Agents/NetClawCopilotToolFactory.cs`
  - `README.md`
- Tests:
  - `tests/FireLakeLabs.NetClaw.Infrastructure.Tests/Runtime/AgentRuntimeServicesTests.cs`

## Verification

- `dotnet test tests/FireLakeLabs.NetClaw.Infrastructure.Tests/FireLakeLabs.NetClaw.Infrastructure.Tests.csproj --filter "AgentRuntimeServicesTests"`
- `dotnet test`
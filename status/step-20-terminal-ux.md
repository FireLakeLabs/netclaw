# Step 20: Terminal UX Follow-Up

## Changes Made

- Added a configurable terminal input prompt so local terminal sessions visibly indicate when the host is waiting for input.
- Serialized terminal output writes so the input prompt and assistant replies do not interleave unpredictably.
- Kept the root terminal helper in no-trigger-required mode by default for local testing, while preserving opt-in trigger-required parity through `NETCLAW_REQUIRE_TRIGGER=true`.
- Added focused test coverage for prompt emission and prompt suppression.
- Documented the new `InputPrompt` terminal option and the launcher override variable.

## Files And Projects Affected

- Production:
  - `src/NetClaw.Infrastructure/Channels/TerminalChannel.cs`
  - `src/NetClaw.Infrastructure/Configuration/TerminalChannelOptions.cs`
  - `src/NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs`
  - `run-terminal-channel.sh`
  - `README.md`
- Tests:
  - `tests/NetClaw.Infrastructure.Tests/Channels/TerminalChannelTests.cs`
  - `tests/NetClaw.IntegrationTests/EndToEndIntegrationTests.cs`

## Verification

- `dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj --filter TerminalChannelTests`
- `dotnet test tests/NetClaw.IntegrationTests/NetClaw.IntegrationTests.csproj --filter TerminalChannel_ProcessesConsoleInputAndWritesReply`
- `bash -n run-terminal-channel.sh`

## Notes

- NetClaw terminal mode now defaults to natural plain-text interaction for local validation.
- Original NanoClaw behavior is stricter for non-main groups: trigger gating is enabled by default and only disabled when registration uses `--no-trigger-required`.
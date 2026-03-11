# Step 19: Terminal Channel

## Changes Made

- Added an optional `terminal` channel adapter that reads inbound messages from `stdin` and writes outbound replies to `stdout` through the same host/channel pipeline used by other adapters.
- Added `TerminalChannelOptions` so the terminal adapter can be configured with a chat JID, sender identity, chat name, group flag, and outbound output prefix.
- Wired the terminal channel into host dependency injection so it can run alongside the existing `ChannelWorker` without a special-case host path.
- Added automated coverage for the terminal adapter itself and for a host integration path that injects terminal input into the real message loop.
- Performed a live Copilot-backed smoke test by piping a triggered message into the terminal channel and verifying the exact stdout reply.
- Documented terminal-channel startup and usage in the repository README.

## Files And Projects Affected

- Production:
  - `src/NetClaw.Infrastructure/Channels/TerminalChannel.cs`
  - `src/NetClaw.Infrastructure/Configuration/TerminalChannelOptions.cs`
  - `src/NetClaw.Host/DependencyInjection/ServiceCollectionExtensions.cs`
  - `README.md`
- Tests:
  - `tests/NetClaw.Infrastructure.Tests/Channels/TerminalChannelTests.cs`
  - `tests/NetClaw.IntegrationTests/EndToEndIntegrationTests.cs`

## Unit Tests And Integration Tests Added

- `TerminalChannelTests`
  - verifies configured metadata is emitted once on connect/poll
  - verifies console input lines are converted into inbound messages
  - verifies outbound replies are written to the configured output writer
- `EndToEndIntegrationTests`
  - verifies terminal input can pass through the host pipeline and produce an outbound reply

## Verification Performed

- `dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj`
- `dotnet test tests/NetClaw.IntegrationTests/NetClaw.IntegrationTests.csproj`
- `dotnet test`
- Live smoke test:
  - registered `team@jid` in `/tmp/netclaw-terminal-live-1773166157`
  - ran `NetClaw.Host` with `NetClaw:Channels:Terminal:Enabled=true`
  - piped `@Andy reply with exactly: TERMINAL_SMOKE_OK`
  - observed stdout reply `assistant> TERMINAL_SMOKE_OK`

Result: 124 xUnit tests passed across the solution, and the live terminal smoke succeeded.

## Manual Validation Checklist

1. Start the host with the terminal channel enabled and confirm it boots without channel errors.
2. Send a simple triggered prompt such as `@Andy hello` and confirm a single stdout reply is produced.
3. Ask for an exact-string response such as `@Andy reply with exactly: TEST_OK` and confirm the output matches.
4. Send a follow-up prompt after the first answer and confirm the session still responds correctly.
5. Send two prompts quickly back to back and confirm ordering is preserved and both are processed.
6. Start a long-running request, then send another message or queue a task through existing mechanisms and confirm the interactive session still behaves correctly under preemption rules.
7. Verify a non-triggered message in a triggered group does not produce an assistant response.
8. Register a different group JID, change terminal channel configuration to that JID, and confirm routing still works for the new chat identity.
9. Stop the host with `Ctrl+C` and confirm shutdown is clean.

## Deferred Items And Known Gaps

- The terminal channel is intentionally single-chat and developer-focused; it does not model multi-chat routing the way a real platform adapter does.
- The current terminal adapter does not support command-mode chat switching, synthetic sender switching, or richer metadata mutation inside one live process.
- A real production adapter is still required to validate platform-specific delivery, identity, and reconnect semantics.
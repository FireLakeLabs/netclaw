# Step 18: Live Copilot Smoke Validation

## Changes Made

- Performed a live operational smoke test against the real Copilot-backed host runtime using the new `reference-file` channel in an isolated temporary project root.
- Registered a real group, started `NetClaw.Host` with the reference channel enabled, injected an inbound file message, and verified that the host emitted an outbound file reply through the full runtime path.
- Documented the reference file channel workflow and JSON envelope shapes in the repository README so the smoke path is reproducible outside this chat session.

## Live Validation Performed

- Temporary project root: `/tmp/netclaw-live-1773164544`
- Registration command:
  - `dotnet run --project src/NetClaw.Setup -- --step register --jid team@jid --name Team --trigger @Andy --folder team`
- Host runtime configuration:
  - `NetClaw:Channels:ReferenceFile:Enabled=true`
  - `NetClaw:Channels:PollInterval=00:00:01`
  - `NetClaw:MessageLoop:PollInterval=00:00:01`
  - `NetClaw:AgentRuntime:CopilotUseLoggedInUser=true`
  - `NetClaw:AgentRuntime:InteractiveIdleTimeout=00:00:10`
- Injected inbound message:
  - `@Andy reply with exactly: LIVE_SMOKE_OK`
- Observed outbound file payload:

```json
{
  "chatJid": "team@jid",
  "text": "LIVE_SMOKE_OK",
  "timestamp": "2026-03-10T17:43:10.930Z"
}
```

## Outcome

- The real host, reference file channel, message loop, Copilot runtime, and outbound routing path all worked together successfully in a live run.
- This confirms that local Copilot CLI authentication is sufficient for an end-to-end NetClaw message turn under the current Linux user context.

## Files And Projects Affected

- Documentation:
  - `README.md`
  - `status/step-18-live-copilot-smoke.md`

## Deferred Items And Known Gaps

- This validation was performed manually in a temporary sandbox; there is not yet an automated opt-in smoke command for live Copilot validation.
- The smoke path currently relies on the development-oriented `reference-file` channel rather than a production messaging adapter.
- Repeated live validation across session resume and preemption paths is still pending beyond this first successful turn.
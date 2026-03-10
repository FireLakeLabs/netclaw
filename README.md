# NetClaw

NetClaw is a .NET rewrite workspace for NanoClaw.

## Solution Layout

- `src/NetClaw.Domain`: core contracts and domain model
- `src/NetClaw.Application`: orchestration services and workflows
- `src/NetClaw.Infrastructure`: persistence and external adapters
- `src/NetClaw.Host`: long-running Linux-first host process
- `src/NetClaw.Setup`: setup and operational CLI
- `tests/*`: xUnit test projects aligned to the production projects they cover
- `status/*`: per-step implementation status documents

## Reference File Channel

NetClaw includes an optional `reference-file` channel for development and smoke testing. When enabled, the host polls inbound JSON messages from `data/channels/reference-file/inbox`, writes processed files to `processed`, invalid files to `errors`, and emits outbound replies as JSON envelopes in `outbox`.

Enable it with configuration like:

- `NetClaw:Channels:ReferenceFile:Enabled=true`
- `NetClaw:Channels:PollInterval=00:00:01`
- `NetClaw:Channels:ReferenceFile:RootDirectory=/path/to/project/data/channels/reference-file`

Inbound file shape:

```json
{
	"id": "message-1",
	"chatJid": "team@jid",
	"sender": "sender-1",
	"senderName": "User",
	"content": "@Andy hello",
	"timestamp": "2026-03-10T00:00:00Z",
	"chatName": "Team",
	"isGroup": true
}
```

Outbound file shape:

```json
{
	"chatJid": "team@jid",
	"text": "assistant reply",
	"timestamp": "2026-03-10T00:00:00Z"
}
```
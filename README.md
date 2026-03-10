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

## Terminal Channel

NetClaw also includes an optional `terminal` channel for local development and engine validation. When enabled, the host reads inbound messages from `stdin` and writes outbound replies to `stdout`.

Enable it with configuration like:

- `NetClaw:Channels:Terminal:Enabled=true`
- `NetClaw:Channels:Terminal:ChatJid=team@jid`
- `NetClaw:Channels:Terminal:Sender=terminal-user`
- `NetClaw:Channels:Terminal:SenderName=Terminal User`
- `NetClaw:Channels:Terminal:ChatName=Terminal Chat`
- `NetClaw:Channels:Terminal:IsGroup=true`
- `NetClaw:Channels:Terminal:InputPrompt=you> `
- `NetClaw:Channels:PollInterval=00:00:01`
- `NetClaw:MessageLoop:PollInterval=00:00:01`

Example live run:

```bash
export NETCLAW_PROJECT_ROOT=/tmp/netclaw-terminal

dotnet run --project src/NetClaw.Setup -- --step register \
	--jid team@jid \
	--name Team \
	--trigger @Andy \
	--folder team

env \
	NetClaw__ProjectRoot=/tmp/netclaw-terminal \
	NetClaw__Channels__Terminal__Enabled=true \
	NetClaw__Channels__Terminal__ChatJid=team@jid \
	NetClaw__Channels__Terminal__Sender=terminal-user \
	NetClaw__Channels__Terminal__SenderName='Terminal User' \
	NetClaw__Channels__Terminal__ChatName='Terminal Chat' \
	NetClaw__Channels__Terminal__IsGroup=true \
	NetClaw__Channels__Terminal__InputPrompt='you> ' \
	NetClaw__Channels__PollInterval=00:00:01 \
	NetClaw__MessageLoop__PollInterval=00:00:01 \
	NetClaw__MessageLoop__Timezone=UTC \
	NetClaw__AgentRuntime__CopilotUseLoggedInUser=true \
	dotnet run --project src/NetClaw.Host
```

For the common local workflow, you can also run the root helper script:

```bash
./run-terminal-channel.sh
```

It wraps the register step plus host startup. By default, the helper registers the terminal chat with `--no-trigger-required`, so plain prompts like `What is the capital of Missouri?` will be processed without `@Andy`.

If you want parity with the triggered group flow instead, start it with `NETCLAW_REQUIRE_TRIGGER=true`; in that mode prompts must include the configured trigger such as `@Andy What is the capital of Missouri?`.

The terminal channel now shows a `you> ` input prompt while it is waiting for input. You can override the defaults with environment variables such as `NETCLAW_PROJECT_ROOT`, `NETCLAW_CHAT_JID`, `NETCLAW_AGENT_TRIGGER`, `NETCLAW_REQUIRE_TRIGGER`, `NETCLAW_TERMINAL_SENDER_NAME`, and `NETCLAW_TERMINAL_INPUT_PROMPT`.

Type a prompt such as `What is the capital of Missouri?` or, in trigger-required mode, `@Andy hello`, and the assistant reply will be written to stdout with the configured prefix.
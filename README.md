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

## Slack Channel

NetClaw now includes an optional `slack` channel for real workspace messaging through Slack Socket Mode. The current implementation supports inbound message ingestion, outbound replies, a visible working indicator, mention normalization so Slack bot mentions can map onto the existing trigger flow, and native assistant-thread behavior when the Slack app has Agents & AI Apps enabled.

Enable it with configuration like:

- `NetClaw:Channels:Slack:Enabled=true`
- `NetClaw:Channels:Slack:BotToken=xoxb-...`
- `NetClaw:Channels:Slack:AppToken=xapp-...`
- `NetClaw:Channels:Slack:MentionReplacement=@Andy`
- `NetClaw:Channels:Slack:WorkingIndicatorText=Evaluating...`
- `NetClaw:Channels:Slack:ReplyInThreadByDefault=true`
- `NetClaw:Channels:PollInterval=00:00:01`
- `NetClaw:MessageLoop:PollInterval=00:00:01`

Current behavior:

- Register Slack conversations using their Slack conversation IDs as the group JID, for example `C...`, `G...`, or `D...`.
- Channel and group messages can remain trigger-gated using the existing group registration settings.
- Slack bot mentions such as `<@BOT_USER_ID>` are normalized to the configured mention replacement, so a registered trigger like `@Andy` still works with Slack mentions.
- Channel and group replies default to thread replies based on the triggering message.
- Direct messages continue in the Slack-provided `thread_ts` when Slack sends one, which keeps AI-app conversations in a single back-and-forth thread instead of spawning separate top-level replies.
- While the agent is working, the Slack channel prefers Slack's native assistant status indicator for DM assistant threads and falls back to a placeholder message such as `Evaluating...` when the app is not using the AI surfaces.

For the native DM assistant experience, enable Slack's Agents & AI Apps feature and subscribe to `assistant_thread_started`, `assistant_thread_context_changed`, and `message.im` in the Slack app configuration. Without those AI features, NetClaw still falls back to the plain bot-message flow.

The code is ready for live validation, but the actual Slack app tokens and scopes still need to be created before a real Slack smoke test can run.

For the common local Slack workflow, you can also run the root helper script:

```bash
./run-slack-channel.sh
```

The helper reads Slack secrets from environment variables instead of storing them in the repo. At minimum, set:

```bash
export NETCLAW_SLACK_BOT_TOKEN='xoxb-...'
export NETCLAW_SLACK_APP_TOKEN='xapp-...'
export NETCLAW_CHAT_JID='C0123456789'
```

Useful optional overrides:

- `NETCLAW_CHAT_NAME` for the registration name shown in NetClaw
- `NETCLAW_GROUP_FOLDER` for the persisted group folder name
- `NETCLAW_REQUIRE_TRIGGER=true|false` to control mention/trigger gating for the registered Slack conversation
- `NETCLAW_SLACK_WORKING_INDICATOR_TEXT` to change the placeholder shown while the agent is working

## Agent Tools

NetClaw now forwards its built-in control-plane tools into live Copilot sessions. That means the assistant can execute runtime actions directly from terminal, reference-file, and Slack conversations instead of only replying in text.

Currently wired tools:

- `send_group_message`: send an immediate outbound message to the active group and, from the main group, optionally to another registered group
- `list_registered_groups`: inspect the registered group list, including JIDs, folders, triggers, and main-group status
- `schedule_group_task`: create one-shot, interval, or cron-backed reminders/tasks using the persisted scheduler
- `lookup_session_state`: inspect whether a registered group currently has a persisted interactive session
- `close_group_input`: force-close the active interactive input stream for a registered group
- `register_group`: register a new group from the main-group control plane

For reminders, the current scheduling tool contract expects:

- `scheduleType`: `once`, `interval`, or `cron`
- `scheduleValue`: ISO-8601 timestamp for `once`, milliseconds for `interval`, or cron expression for `cron`
- `contextMode`: optional `isolated` or `group`
- `targetJid`: optional alternate registered group JID when the request is made from the main group

With this bridge in place, prompts such as `remind me in 5 minutes to check the pot` can be fulfilled by the agent through the scheduler instead of being treated as plain unsupported text.
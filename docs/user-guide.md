# User Guide

## What NetClaw Is

NetClaw is a chat-driven agent host. You register a chat, send messages into it, and the host decides whether to trigger the agent, continue an interactive session, or schedule future work.

The live channels today are:

- terminal
- reference-file
- Slack

The active real provider is Copilot.

## Basic Concepts

### Registered group

A registered group is a chat NetClaw knows how to handle. Each registered group has a JID, a name, a folder, and a trigger.

### Main group

The main group is the control plane. It can do normal chat, but it can also manage other groups and scheduled tasks.

### Triggered vs untriggered groups

If a group requires a trigger, prompts need to include the configured trigger such as `@Andy`. If a group does not require a trigger, plain text is enough.

### Interactive session

If the provider keeps a session open, follow-up messages can continue that same session instead of starting over.

## Running The Terminal Channel

The fastest way to try the project is:

```bash
./run-terminal-channel.sh
```

Expected behavior:

- a local group is registered if needed
- the host starts with the terminal channel enabled
- you get a `you> ` prompt
- entering a message triggers the agent and prints the reply to stdout

Example prompts:

- `What is the capital of Missouri?`
- `remind me in 5 minutes to check the pot`
- `list scheduled tasks`

If you enable trigger mode, prompts should include the trigger:

- `@Andy hello`
- `@Andy remind me tomorrow at 8am to review the logs`

## Running Slack

Set the required environment variables and then start the helper:

```bash
export NETCLAW_SLACK_BOT_TOKEN='xoxb-...'
export NETCLAW_SLACK_APP_TOKEN='xapp-...'
export NETCLAW_CHAT_JID='C0123456789'

./run-slack-channel.sh
```

Expected behavior:

- the Slack conversation is registered as a NetClaw group
- inbound messages in that Slack conversation are persisted and routed
- replies go back to Slack, usually in-thread
- DM assistant threads use Slack's native status behavior when available

Example prompts in a trigger-required Slack chat:

- `@Andy hello`
- `@Andy list all scheduled tasks across groups`
- `@Andy pause task task-123`

Example prompts in a no-trigger Slack chat:

- `hello`
- `remind me every weekday at 9am to post the status update`

## Registering A Group Manually

Use the setup CLI:

```bash
dotnet run --project src/NetClaw.Setup -- --step register \
  --jid team@jid \
  --name Team \
  --trigger @Andy \
  --folder team \
  --no-trigger-required
```

Expected behavior:

- the group is stored in SQLite
- future messages from that JID can trigger the runtime

## Current Features

### Normal chat

Prompt examples:

- `What changed in this repo?`
- `Summarize the latest failures`
- `Help me reason about the scheduler`

Expected behavior:

- NetClaw formats recent inbound messages into a prompt
- the provider responds in the same chat
- follow-up messages may continue the same session

### Interactive continuation

Prompt examples:

- `keep going`
- `that failed, try again with a smaller change`
- `close that out and summarize what happened`

Expected behavior:

- if there is an active or persisted session for the group, follow-up messages can continue the thread of work
- if there is no session, the message starts a fresh one

### Schedule a task

Prompt examples:

- `remind me in 5 minutes to check the pot`
- `every weekday at 9am send me a short standup prompt`
- `tomorrow at 8am remind me to review the overnight logs`

Expected behavior:

- the agent may call `schedule_group_task`
- a scheduled task is stored in SQLite
- when due, the scheduler runs the prompt through the agent runtime
- one-shot tasks finish as completed, recurring tasks remain active

### List scheduled tasks

Prompt examples:

- `list scheduled tasks`
- `show me all active reminders`
- `list all scheduled tasks across groups`

Expected behavior:

- the agent may call `list_scheduled_tasks`
- the current group sees its own tasks
- the main group can inspect tasks across groups

### Pause a scheduled task

Prompt examples:

- `pause task task-123`
- `pause the Monday briefing task`

Expected behavior:

- the agent identifies the target task
- `pause_scheduled_task` updates the persisted task status to `paused`
- the scheduler stops running that task until resumed

### Resume a scheduled task

Prompt examples:

- `resume task task-123`
- `turn the weekday reminder back on`

Expected behavior:

- the agent calls `resume_scheduled_task`
- the task status returns to `active`
- future due runs continue normally

### Cancel a scheduled task

Prompt examples:

- `cancel task task-123`
- `delete the daily reminder`

Expected behavior:

- the agent calls `cancel_scheduled_task`
- the task remains in storage for history, but its status becomes `cancelled`
- the task no longer runs

### List registered groups

Prompt examples:

- `list registered groups`
- `show me the groups you know about`

Expected behavior:

- the agent calls `list_registered_groups`
- the response includes JIDs, folders, triggers, and main-group status

### Register a group from the main group

Prompt examples:

- `register a new group with jid team-2@jid, name Team 2, folder team-2, trigger @Andy`
- `add a new Slack group for C0123456789 called Build Alerts`

Expected behavior:

- only the main group should be allowed to do this
- the new group is stored and can be used later

### Look up session state

Prompt examples:

- `do we have an active session here?`
- `look up the session state for team@jid`

Expected behavior:

- the agent calls `lookup_session_state`
- the response shows whether a persisted session exists for the target group

### Close active input

Prompt examples:

- `close the active input for this group`
- `stop taking more input for team@jid`

Expected behavior:

- the agent calls `close_group_input`
- the group execution queue closes the live input stream for that group

### Send a group message

Prompt examples:

- `send a message to this group saying deployment is complete`
- `from the main group, send team@jid a note that the job passed`

Expected behavior:

- the agent calls `send_group_message`
- the outbound router sends the message through the owning channel

## Behavior Notes

- Prompt wording is not a strict command parser. The examples in this guide are the kinds of prompts that should cause the feature to be used.
- Cross-group management actions should be performed from the main group.
- If a group requires a trigger and you omit it, the message may be stored but ignored for execution.
- If no connected channel owns the target JID, outbound message delivery fails.

## Features Not Yet Present

The repo does not yet provide real parity for WhatsApp, Telegram, Discord, Gmail, image vision, PDF reading, transcription, local Whisper, or X integration. The research notes for those live in `future-features/`.
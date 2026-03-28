# NetClaw Agents

NetClaw is a .NET host for a small chat-driven agent system. It exists so the behavior, persistence, routing, and execution model stay understandable to the people running it.

Run a quick local session with:

```bash
./run-terminal-channel.sh
```

Run the host directly with:

```bash
dotnet run --project src/FireLakeLabs.NetClaw.Host
```

Register a chat with:

```bash
dotnet run --project src/FireLakeLabs.NetClaw.Setup -- --step register --jid team@jid --name Team --trigger @assistant --folder team --no-trigger-required
```

The active providers are Copilot and Claude Code, running inside isolated containers. Terminal, reference-file, and Slack are the live channels today. Messages are persisted in SQLite, routed per registered group, and executed through a per-group queue so a single chat does not run multiple agent turns at once.

Use this repo as a system you can inspect and change, not as an authority.
# NetClaw

NetClaw is a loose port of NanoClaw to .NET.

The point of this repo is not to present a finished agent platform. The point is to build an agent host we understand well enough to change, debug, and live with. A lot of the shape of the project comes from NanoClaw, but the runtime choices are different: Linux first, .NET 10, xUnit, SQLite, and Copilot as the first working agent provider.

If you want to study it, borrow from it, fork it, or laugh at it, that is fine.

## What It Does Today

- Runs a hosted .NET process that polls inbound channels and routes messages into an agent runtime.
- Persists groups, messages, sessions, and scheduled tasks in SQLite.
- Supports terminal, reference-file, and Slack channels.
- Supports interactive sessions and a small control-plane tool surface for group management and scheduling.
- Runs agents inside isolated Docker/Podman containers. Copilot and Claude Code are both supported via a shared container image.
- Injects credentials through an HTTP proxy so containers never see real API keys.

## What It Is Not

- Not feature-complete relative to NanoClaw.
- Not channel-complete.
- Not a polished product.
- Not a claim that .NET is the objectively right way to do this.

## Repo Layout

- `src/FireLakeLabs.NetClaw.Domain`: contracts, entities, enums, value objects
- `src/FireLakeLabs.NetClaw.Application`: orchestration, routing, scheduling, execution
- `src/FireLakeLabs.NetClaw.Infrastructure`: channels, persistence, runtime adapters, filesystem and platform integration
- `src/FireLakeLabs.NetClaw.Host`: the long-running host process and dependency wiring
- `src/FireLakeLabs.NetClaw.AgentRunner`: standalone console app that runs inside the container
- `src/FireLakeLabs.NetClaw.Setup`: CLI for setup and operational steps
- `container/`: Dockerfile and build script for the agent container image
- `tests`: xUnit coverage aligned to the production projects
- `status`: step-by-step implementation notes
- `future-features`: research notes for missing parity and delayed work
- `docs`: architecture, user guide, coding standards, design documents

## Running It

### Terminal smoke run

```bash
./run-terminal-channel.sh
```

This registers a local chat and starts the host with the terminal channel enabled.

### Slack smoke run

```bash
export NETCLAW_SLACK_BOT_TOKEN='xoxb-...'
export NETCLAW_SLACK_APP_TOKEN='xapp-...'
export NETCLAW_CHAT_JID='C0123456789'

./run-slack-channel.sh
```

### Manual setup and host run

Register a chat:

```bash
dotnet run --project src/FireLakeLabs.NetClaw.Setup -- --step register \
  --jid team@jid \
  --name Team \
  --trigger @assistant \
  --folder team \
  --no-trigger-required
```

Start the host:

```bash
dotnet run --project src/FireLakeLabs.NetClaw.Host
```

## Testing

Run the full suite:

```bash
dotnet test
```

Common focused suites:

```bash
dotnet test tests/FireLakeLabs.NetClaw.Infrastructure.Tests/FireLakeLabs.NetClaw.Infrastructure.Tests.csproj --filter "AgentRuntimeServicesTests"
dotnet test tests/FireLakeLabs.NetClaw.Infrastructure.Tests/FireLakeLabs.NetClaw.Infrastructure.Tests.csproj --filter "SlackChannelTests"
dotnet test tests/FireLakeLabs.NetClaw.Application.Tests/FireLakeLabs.NetClaw.Application.Tests.csproj --filter "TaskSchedulerServiceTests"
```

## Documentation

- [AGENTS.md](AGENTS.md)
- [CONTRIBUTING.md](CONTRIBUTING.md)
- [ROADMAP.md](ROADMAP.md)
- [docs/architecture.md](docs/architecture.md)
- [docs/containerized-agent-execution.md](docs/containerized-agent-execution.md)
- [docs/troubleshooting-agent-not-responding.md](docs/troubleshooting-agent-not-responding.md)
- [docs/user-guide.md](docs/user-guide.md)
- [docs/coding-standards.md](docs/coding-standards.md)

## Configuration Samples

Sample configuration files live under `config-examples/`:

- `appsettings.example.json`
- `mount-allowlist.json`
- `sender-allowlist.json`

The repo also includes `global.json` to pin the .NET SDK used by local development and CI.

## Current Reality

NetClaw is most useful right now as a working experiment and a readable codebase. It already has enough moving parts to be interesting, and still has enough missing parts that nobody should confuse it for a finished system.
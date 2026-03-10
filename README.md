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
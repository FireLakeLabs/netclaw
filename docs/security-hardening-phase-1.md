# Step: Security Hardening Phase 1

## What Changed

### Dashboard bind address default

Both `run-terminal-channel.sh` and `run-slack-channel.sh` now default `DASHBOARD_BIND_ADDRESS` to `127.0.0.1` instead of `0.0.0.0`. This means the dashboard is only reachable from the local machine unless explicitly overridden.

The C# `DashboardOptions` class already defaulted to `127.0.0.1`, but the shell scripts were overriding that to `0.0.0.0`. The scripts now match the host default.

The `config-examples/appsettings.example.json` has been updated to include the `Dashboard` section with `127.0.0.1` and to replace the placeholder Slack tokens with `USE_OP_RUN_INSTEAD` as a reminder.

### 1Password CLI integration

A new `.env.tpl` file provides `op://` secret references for all sensitive values. When used with `op run --env-file=.env.tpl`, secrets are resolved at runtime by the 1Password CLI and injected into the process environment without ever touching disk, shell history, or process arguments.

This is additive. The existing env-var approach still works. The 1Password path is the recommended default for anyone who has `op` installed.

### IPC directory permissions

A new `DirectoryPermissions` utility in `FireLakeLabs.NetClaw.Infrastructure.FileSystem` sets owner-only permissions (`700`) on sensitive directories during host initialization. This covers `data/`, `data/ipc/`, `data/sessions/`, `data/files/`, and the database directory.

The `HostInitializationService` now calls `RestrictSensitiveDirectories()` after creating directories and before initializing the database or loading allowlists.

The utility is a no-op on non-Unix platforms and logs warnings if permissions cannot be set or verified.

### Documentation

A new `docs/security.md` describes the threat model, 1Password setup instructions, dashboard access policy, IPC security posture, agent permission boundaries, data-at-rest considerations, and a deployment checklist.

### Gitignore additions

A `.gitignore-security-additions` file lists patterns that should be merged into the repo's `.gitignore` to prevent accidental commits of database files, runtime data directories, resolved environment files, and other sensitive artifacts.

## What Did Not Change

- No behavioral changes to the agent runtime, channels, scheduling, or message processing.
- No new dependencies.
- No changes to the domain or application layers.
- The existing env-var-based launch flow is fully preserved.

## Risk Areas

- The `DirectoryPermissions` utility uses `File.SetUnixFileMode` which requires .NET 7+. NetClaw targets .NET 10, so this is fine.
- The `File.SetUnixFileMode` call on a directory path works on Linux but should be verified on macOS if that becomes a target.
- The `.env.tpl` references assume a vault named `netclaw` and item/field names as documented. Users need to create these in 1Password before `op run` will work.

## What Was Not Tested

- The 1Password integration requires `op` to be installed and authenticated, which is not available in CI.
- The Unix permission calls were not exercised in an automated test because the test environment may not support `SetUnixFileMode` on tmpfs.
- The bind address change was verified by reading the script logic, not by running a live host.

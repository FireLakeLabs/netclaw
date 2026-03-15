# Security

## Threat Model

NetClaw is a personal agent host running on a Linux machine under a single user account. The primary threats are:

- **Credential exposure**: tokens leaking to disk, logs, shell history, or process lists.
- **Unauthorized dashboard access**: anyone on the same network reading messages or injecting commands.
- **IPC injection**: other processes on the host writing commands into the IPC directory.
- **Agent overreach**: the agent runtime performing actions beyond its intended scope.
- **Data exposure**: the SQLite database and workspace files being readable by other users or processes.

This is not a multi-tenant system. The security model assumes a single operator on a personal machine, but still aims to minimize the blast radius of any single failure.

## Credential Management

### 1Password CLI Integration

NetClaw uses [1Password CLI](https://developer.1password.com/docs/cli/) (`op`) as the primary secret store. Secrets are never written to config files, environment files, or shell history.

#### Setup

1. Install the 1Password CLI and sign in:
   ```bash
   # See https://developer.1password.com/docs/cli/get-started/
   op account add
   op signin
   ```

2. Create a vault called `netclaw`:
   ```bash
   op vault create netclaw
   ```

3. Store your secrets:
   ```bash
   op item create --vault netclaw --category login \
     --title slack \
     'bot-token=xoxb-your-actual-token' \
     'app-token=xapp-your-actual-token'
   ```

4. Verify the references work:
   ```bash
   op read op://netclaw/slack/bot-token
   ```

#### Running with 1Password

Wrap any launch script with `op run` to inject secrets at runtime:

```bash
# Slack channel with secrets from 1Password
export NETCLAW_CHAT_JID='C0123456789'
op run --env-file=.env.tpl -- ./run-slack-channel.sh
```

The `.env.tpl` file contains `op://` secret references, not plaintext. It is safe to commit to the repository.

`op run` resolves each reference, injects the plaintext value into the child process environment, and cleans up when the process exits. The secrets never appear in shell history, on disk, or in `/proc/<pid>/cmdline`.

#### Fallback (No 1Password)

If you don't use 1Password, the existing approach of setting environment variables directly still works:

```bash
export NETCLAW_SLACK_BOT_TOKEN='xoxb-...'
export NETCLAW_SLACK_APP_TOKEN='xapp-...'
export NETCLAW_CHAT_JID='C0123456789'
./run-slack-channel.sh
```

This is less secure because the tokens are visible in your shell history and in `/proc/<pid>/environ`. Use `op run` when possible.

## Dashboard Access

### Default: Localhost Only

The dashboard binds to `127.0.0.1` by default. It is only accessible from the local machine. To access it remotely, use SSH port forwarding:

```bash
ssh -L 5080:127.0.0.1:5080 your-server
# Then open http://localhost:5080 in your browser
```

If you explicitly need to bind to all interfaces, override the bind address:

```bash
export NETCLAW_DASHBOARD_BIND_ADDRESS='0.0.0.0'
```

This exposes the dashboard to your local network without authentication. Do not do this on untrusted networks.

### Future: Bearer Token Authentication

A bearer token middleware is planned. When enabled, the dashboard will require an `Authorization: Bearer <token>` header on all API and SignalR requests. The token will be stored in 1Password and loaded at host startup.

## IPC Security

The filesystem IPC layer (`data/ipc/`) processes JSON command files dropped by the agent runtime. The IPC directories are created with owner-only permissions (`700`) during host initialization. Any process that can write to these directories can inject commands.

On a single-user machine this is acceptable because any process running as your user already has access to everything NetClaw can reach. The IPC layer does not expand the attack surface beyond what already exists.

If you run NetClaw alongside untrusted processes under the same user, consider running it under a dedicated service account.

## Agent Permissions

The current agent runtime auto-approves all permission requests from the Copilot SDK (`PermissionHandler.ApproveAll`). This means the agent can read and write any file your user can access, execute shell commands, and make network requests.

This is intentional for a personal agent where you want maximum autonomy. If you want to restrict the agent:

- Use the mount allowlist (`mount-allowlist.json`) to control which host directories are accessible.
- Use the sender allowlist (`sender-allowlist.json`) to control who can trigger the agent.
- The per-group workspace boundary ensures each group operates in its own directory.

A policy-based permission handler is planned as a future phase.

## Data at Rest

The SQLite database and workspace files are stored as plain files under the project root. They are protected by standard Unix file permissions. The database is not encrypted.

Recommendations:

- Set the project root directory to `700` permissions.
- Run on an encrypted filesystem (LUKS, FileVault, or similar).
- Do not commit `*.db` files or the `data/` directory to version control.

## Sensitive Patterns in Logs

NetClaw does not currently redact sensitive values from log output. Avoid logging at `Debug` level in production if your logs are stored or shipped elsewhere. A log sanitization filter is planned.

## Checklist for New Deployments

1. Install and configure 1Password CLI.
2. Create a `netclaw` vault and store your secrets.
3. Verify `op read` works for each secret reference in `.env.tpl`.
4. Launch with `op run --env-file=.env.tpl -- ./run-slack-channel.sh`.
5. Confirm the dashboard is only accessible on `127.0.0.1:5080`.
6. Set `chmod 700` on the project root directory.
7. Verify `.gitignore` includes `*.db`, `data/`, and `.env`.

# Container-Based Isolation

## Description

The delayed container work is really about restoring strong process and filesystem isolation while staying Linux-first. NanoClaw's Apple Container skill is macOS-specific, so effective parity for NetClaw should be interpreted as a Linux-native isolation strategy that preserves per-group workspaces, strict mounts, env shadowing, and runtime cleanup.

Current baseline:

- NetClaw now runs all agent execution inside Docker or Podman containers via `ContainerizedAgentEngine`.
- A credential proxy injects real API keys so containers never see secrets.
- Mount security, workspace layout, and per-group isolation are implemented.
- Both Docker and Podman are supported via `ContainerRuntimeOptions.RuntimeBinary`.
- The in-process execution path has been removed.

## Remaining Steps

1. Normalize mount formatting and readonly behavior more thoroughly across runtime providers.
2. Add runtime selection and verification logic in setup so the host can validate the chosen backend at registration time.
3. Add full integration tests around workspace mounting, auth/environment propagation, orphan cleanup, and session interruption.
4. Only consider more specialized runtimes such as `systemd-nspawn` or `bubblewrap` if Podman/Docker compatibility proves insufficient.

## Complexity

Low–medium. The core container execution path is implemented. Remaining work is cross-runtime testing and polish rather than greenfield implementation.
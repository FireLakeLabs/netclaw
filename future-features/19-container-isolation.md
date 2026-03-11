# Container-Based Isolation

## Description

The delayed container work is really about restoring strong process and filesystem isolation while staying Linux-first. NanoClaw's Apple Container skill is macOS-specific, so effective parity for NetClaw should be interpreted as a Linux-native isolation strategy that preserves per-group workspaces, strict mounts, env shadowing, and runtime cleanup.

Current baseline:

- NetClaw already has containerized execution semantics, but the implementation is Docker-only.
- Mount security and workspace layout are already part of the design.
- The repo includes container assets and a runtime abstraction that can be extended.

## High-Level Steps

1. Extend the existing container runtime abstraction so Docker is one provider rather than the only provider.
2. Add Linux-first runtime options, with Podman as the most practical first target because it is Docker-compatible and common on Linux.
3. Normalize mount formatting, readonly behavior, env injection, and cleanup across runtime providers.
4. Add runtime selection and verification logic in setup so the host can choose Docker, Podman, or another supported backend.
5. Add full integration tests around workspace mounting, auth/environment propagation, orphan cleanup, and session interruption.
6. Only consider more specialized runtimes such as `systemd-nspawn` or `bubblewrap` if Podman/Docker compatibility proves insufficient.

## Complexity

Medium. This is a contained infrastructure project rather than a research spike now that the runtime boundaries already exist. The real work is cross-runtime testing and getting mount behavior exactly right under Linux.
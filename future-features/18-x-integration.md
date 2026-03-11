# X Integration

## Description

The X integration lets the agent perform actions such as post, reply, quote, like, and repost. NanoClaw approaches this through browser automation rather than the paid platform API. Effective parity for NetClaw likely follows the same browser-automation approach, but adapted for Linux and either .NET Playwright or a subprocess bridge.

Current baseline:

- NetClaw has no social-platform integration.
- There is no browser-automation service in the current host.
- Linux support means headless browser execution is practical, but session persistence and auth safety need explicit design.

## High-Level Steps

1. Choose the execution model: native C# Playwright or a subprocess bridge to retained Node automation.
2. Add a setup flow that establishes and persists a browser-authenticated X session.
3. Expose a narrow first tool set such as post, reply, and like.
4. Add durable tasking or IPC if actions need to be decoupled from the main agent run.
5. Add visible audit logging so automated social actions are inspectable.

## Complexity

High. Browser automation is always operationally brittle, and this feature has more security and reputational risk than the messaging or document features.
# Gmail Integration

## Description

Gmail parity can be split into two useful layers: tool-mode and full channel-mode. Tool-mode lets the agent read, search, draft, and send mail on request. Channel-mode lets incoming email act as a trigger source for the agent. Tool-mode is the lower-risk first milestone for FireLakeLabs.NetClaw.

Current baseline:

- NetClaw has no Gmail tools or channel.
- The runtime now supports live custom tools cleanly, which makes Gmail tool-mode a natural fit.
- There is no current OAuth flow or inbound polling loop.

## High-Level Steps

1. Start with tool-mode using Google's .NET Gmail SDK and OAuth desktop credentials.
2. Add secure token storage and refresh handling in a Linux-friendly location.
3. Expose Gmail actions as live Copilot tools: list, read, search, draft, send.
4. If channel-mode is still desired, add a polling service for unread or filtered inbox items and translate them into NetClaw messages.
5. Add setup flows for GCP project creation, OAuth bootstrapping, and token repair.

## Complexity

Medium. Tool-mode is straightforward and fits NetClaw's current architecture well. Full channel-mode adds more operational complexity because of polling, sender mapping, and reply semantics.
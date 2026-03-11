# Parallel Research

## Description

The Parallel skill adds external web-research capabilities through provider tooling. Effective parity for NetClaw means deciding whether to expose external research as direct tools, a provider bridge, or an internal service facade, while keeping cost and permission control explicit.

Current baseline:

- NetClaw has no external research provider integration.
- It has a working live-tool mechanism, which is the right foundation for research tools.
- There is no current policy layer for gated or expensive tool calls.

## High-Level Steps

1. Choose the integration model: direct HTTP client, MCP bridge, or internal service wrapper.
2. Add configuration and secret handling for the provider API key.
3. Expose a narrow tool surface first, such as quick search and deep research.
4. Add policy controls so expensive or risky calls can be gated or disabled.
5. Add result-shaping so the agent receives structured rather than noisy web output.

## Complexity

Medium-High. The API plumbing is manageable, but cost control, result quality, and permission semantics add meaningful design work.
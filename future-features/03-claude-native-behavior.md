# Claude-Native Behavior

## Description

NanoClaw was shaped around Claude-native behaviors such as long-running conversational continuity, slash-style session controls, multimodal inputs, broad tool use, and orchestration patterns that assume a highly tool-capable agent. NetClaw needs equivalent behaviors adapted to Copilot and GPT-5.4 rather than literal Claude parity.

Current baseline:

- Copilot session persistence and custom tools are working.
- The scheduler and group control plane now have a real tool bridge.
- Multimodal inputs, session compaction, external research providers, and broader MCP-style tool ecosystems are still missing.

## High-Level Steps

1. Add explicit session control behaviors such as compaction and session inspection.
2. Add multimodal ingestion for images, PDFs, and voice so live chats can supply non-text input.
3. Expand the external tool surface with local-model, research, and social-integration tools where Copilot supports it cleanly.
4. Tighten prompt and instruction design around Copilot-specific tool invocation behavior rather than assuming Claude-style command patterns.
5. Add reliability rules around when the agent should use tools versus answer directly.

## Complexity

High. This is not one feature; it is a bundle of cross-cutting behaviors. The main challenge is adapting NanoClaw's Claude-centered ergonomics to Copilot's APIs and model behavior without overfitting to Claude-specific assumptions.
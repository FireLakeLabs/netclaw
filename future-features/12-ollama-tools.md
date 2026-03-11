# Ollama Tools

## Description

Ollama parity means giving the main Copilot-driven agent access to local models for cheap or offline subtasks such as summarization, translation, or classification. This is best treated as a tool provider rather than a replacement for the primary agent runtime.

Current baseline:

- NetClaw does not yet expose local-model tools.
- The new live custom-tool bridge makes this a much easier addition than it would have been earlier in the rewrite.
- Linux is a good target because Ollama is well supported there.

## High-Level Steps

1. Add an `IOllamaClient` or lightweight HTTP wrapper over the Ollama host API.
2. Expose a small tool set to the agent such as list-models and generate-with-model.
3. Add host configuration for the Ollama endpoint and model defaults.
4. Add prompt guidance so the main agent uses local models only for appropriate subtasks.
5. Add failure handling for model absence, host unavailability, and timeouts.

## Complexity

Medium. The technical path is straightforward, but the main product work is deciding how and when the primary agent should delegate to a local model.
# Roadmap

This is a working list of the next things that would make NetClaw feel less like a pile of useful experiments and more like a stable project.

## Near Term

- Keep Slack usable and finish the remaining setup and behavior rough edges.
- Keep the Copilot runtime stable and make the live tool surface more predictable.
- Improve setup ergonomics so local bring-up is less dependent on reading source.
- Keep docs, tests, and CI honest as features move.

## Short Term

- Add real configuration samples and tighten configuration validation.
- Expand operator workflows around registration, diagnostics, and task management.
- Add stronger integration coverage around the full host and message loop.
- Start the Linux-first container isolation work beyond Docker-only assumptions.

## Medium Term

- Add the next channels that fit .NET and Linux well, likely Telegram, Discord, and Gmail tool mode.
- Add multimodal features such as image and PDF ingestion.
- Add transcription behind a provider abstraction, with local Whisper as a later backend.
- Add a more explicit operational diagnostics story.

## Explicit Gaps In Code Today

- `ClaudeCode`, `Codex`, and `OpenCode` engines are still placeholder implementations.
- Container isolation is still effectively Docker-only.
- The future feature set described in `future-features/` is still mostly research, not shipped behavior.

## Rule Of Thumb

If a new feature makes the system harder to understand without making it easier to operate, it probably should wait.
# Coding Standards

## General Direction

Write ordinary, idiomatic C#.

Prefer code that is easy to read in six months over code that is clever for five minutes. Keep abstractions small, name things directly, and let the project structure do some of the organizational work.

## Language And Project Defaults

- Target the SDK and language level already used by the repo.
- Nullable reference types stay enabled.
- Treat warnings as errors.
- Keep implicit usings enabled unless there is a strong reason not to.

## Design Guidelines

- Keep `Domain` free of infrastructure details.
- Keep `Application` focused on orchestration, routing, and policy.
- Keep `Infrastructure` responsible for external systems, persistence, channels, and provider adapters.
- Keep `Host` focused on dependency wiring and hosted-service startup.
- Prefer constructor injection over service location.
- Prefer small services with one obvious responsibility.

## C# Style

- Use clear type and member names.
- Avoid one-letter variable names outside trivial loops.
- Prefer early returns over deep nesting.
- Prefer pattern matching and switch expressions where they make code clearer.
- Use `record` or `record struct` for small immutable value types when that matches the current project style.
- Use `sealed` for classes that are not intended for inheritance.
- Avoid static helpers when behavior belongs on a service with dependencies.

## Nullability And Validation

- Validate constructor and method inputs at the boundary where bad input becomes meaningful.
- Do not scatter null forgiveness operators to silence warnings.
- If a value is optional, model it as optional and handle it directly.

## Async And Concurrency

- Use async end to end for I/O.
- Accept `CancellationToken` on async methods that may block or perform I/O.
- Do not hide long-running work behind fire-and-forget tasks unless a hosted-service or queue boundary owns that lifecycle.
- Keep concurrency rules explicit. Group execution is serialized for a reason.

## Persistence And I/O

- Keep SQL straightforward and readable.
- Prefer repository methods that reflect actual domain operations instead of vague generic wrappers.
- Store timestamps in round-trip-safe formats.
- Keep filesystem and process execution inside infrastructure services.

## Logging

- Log enough to explain failure and lifecycle transitions.
- Avoid noisy logs that restate obvious control flow every tick.
- Include identifiers that help diagnose issues, such as task IDs, chat JIDs, group folders, or session IDs.

## Tests

- Add tests for functional behavior, not just implementation trivia.
- Prefer one clear reason for failure per test.
- Keep test names descriptive.
- Use focused in-memory fakes where that is simpler than mocking everything.
- Run focused tests for the area you changed and then run the full suite.
- Do not use `Task.Delay` for synchronization. Use `TaskCompletionSource`, `SemaphoreSlim`, or `ManualResetEventSlim` to coordinate with background work deterministically. CI will reject `Task.Delay` in test files.

## Formatting

- Use the repo formatter and analyzer defaults.
- Do not reformat unrelated files.
- Keep comments rare and useful.
- Prefer ASCII unless the file already needs something else.

## Documentation

- Update the relevant docs when behavior changes.
- Do not oversell the state of the project.
- Describe current behavior, known limitations, and intended use plainly.
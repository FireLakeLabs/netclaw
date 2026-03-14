# Copilot Review Instructions

## Test Timing

Flag any use of `Task.Delay` in test files as a bug. Tests must use deterministic synchronization (`TaskCompletionSource`, `SemaphoreSlim`, `ManualResetEventSlim`) instead of arbitrary delays to coordinate with background work. Timing-based waits cause flaky failures in CI.

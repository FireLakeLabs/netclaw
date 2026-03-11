# Contributing

## Ground Rules

- Keep changes small and explainable.
- Fix the thing you touched instead of sweeping in unrelated cleanup.
- Preserve the current shape of the repo unless there is a clear reason to move it.
- Add or update tests for functional changes.
- If behavior changes, update the docs that describe it.

## Expected Workflow

1. Understand the current behavior before editing.
2. Make the smallest change that solves the problem.
3. Add or update focused tests near the changed code.
4. Run the relevant focused test suite.
5. Run the full test suite.
6. Check formatting and analyzer output.
7. Update docs or status notes when the change affects how the system is used or understood.

## Minimum Verification Before Commit

Run these from the repo root.

```bash
dotnet --version
dotnet format NetClaw.slnx --verify-no-changes
dotnet build NetClaw.slnx
dotnet test
```

The expected SDK is pinned in `global.json`. If your local SDK does not match, fix that before chasing strange build failures.

## Focused Test Passes

Also run the most relevant focused suite for the area you changed. Common examples:

```bash
dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj --filter "AgentRuntimeServicesTests"
dotnet test tests/NetClaw.Infrastructure.Tests/NetClaw.Infrastructure.Tests.csproj --filter "SlackChannelTests"
dotnet test tests/NetClaw.Application.Tests/NetClaw.Application.Tests.csproj --filter "TaskSchedulerServiceTests"
dotnet test tests/NetClaw.Host.Tests/NetClaw.Host.Tests.csproj
dotnet test tests/NetClaw.IntegrationTests/NetClaw.IntegrationTests.csproj
```

If you changed runtime wiring, channel behavior, scheduling, host setup, or persistence, do not stop at focused tests. Run the full suite.

## Documentation Expectations

- Update `README.md` when the repo-level story changes.
- Update `AGENTS.md` if the short operator view changes.
- Update files under `docs/` for architecture, usage, or coding standards changes.
- Add a `status/step-xx-*.md` note for meaningful increments.

## Pull Request Expectations

- Describe the user-visible behavior change.
- Call out risk areas and what you tested.
- Be honest about what you did not test.

## Style Expectations

Follow the repo coding standards in [docs/coding-standards.md](docs/coding-standards.md). The short version is idiomatic C#, nullable enabled, warnings as errors, small cohesive services, and tests that explain behavior rather than implementation trivia.
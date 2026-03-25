# Step 32: Project Docs And CI

## Changes Made

- Rewrote the root `README.md` so it describes NetClaw plainly as a loose .NET port of NanoClaw and explains the current state of the project without overselling it.
- Added a terse root `AGENTS.md` with the short purpose, setup command, and common run path.
- Added a root `CONTRIBUTING.md` that documents the expected development workflow, focused test passes, full-suite verification, and documentation expectations.
- Added a new `docs/` folder with an architecture guide, a user guide, and a coding standards guide.
- Added `.editorconfig` and a GitHub Actions workflow so formatting, build, and test checks can run in CI.
- Normalized the repository with `dotnet format` so the new formatting gate reflects the actual repo state instead of failing on pre-existing whitespace and newline drift.

## Files And Projects Affected

- Repo docs and standards:
  - `README.md`
  - `AGENTS.md`
  - `CONTRIBUTING.md`
  - `docs/architecture.md`
  - `docs/user-guide.md`
  - `docs/coding-standards.md`
  - `.editorconfig`
  - `.github/workflows/ci.yml`
- Formatting normalization:
  - solution-wide whitespace, newline, encoding, and import-order updates from `dotnet format`

## Verification

- `dotnet format FireLakeLabs.NetClaw.slnx --verify-no-changes`
- `dotnet build FireLakeLabs.NetClaw.slnx`
- `dotnet test`
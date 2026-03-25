# Step 33: SDK Pinning, Config Samples, And Roadmap

## Changes Made

- Added `global.json` to pin the .NET SDK version used by local development and CI.
- Added `config-examples/` with example host configuration, mount allowlist, and sender allowlist files.
- Added a short `ROADMAP.md` so the repo has a visible list of current priorities and explicit code-level gaps.
- Added GitHub issue templates for bug reports and feature requests.
- Updated the root docs and CI workflow so the new SDK pin and configuration samples are part of the normal repo story.

## Files And Projects Affected

- Repo assets:
  - `global.json`
  - `ROADMAP.md`
  - `config-examples/appsettings.example.json`
  - `config-examples/mount-allowlist.json`
  - `config-examples/sender-allowlist.json`
  - `.github/ISSUE_TEMPLATE/bug_report.yml`
  - `.github/ISSUE_TEMPLATE/feature_request.yml`
  - `.github/ISSUE_TEMPLATE/config.yml`
- Updated docs and CI:
  - `README.md`
  - `CONTRIBUTING.md`
  - `.github/workflows/ci.yml`

## Verification

- `dotnet format FireLakeLabs.NetClaw.slnx --verify-no-changes`
- `dotnet build FireLakeLabs.NetClaw.slnx`
- `dotnet test`
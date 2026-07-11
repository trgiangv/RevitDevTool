# Verification

Prefer scripts under `scripts/agent/` so agents do not guess command strings.

## Test Reality

The current test projects are useful for smoke checks, parser contracts, and compile-time guardrails, but they are not deep behavioral assurance for the whole platform. Treat a green test run as one signal, not as proof that host integration, Revit/AutoCAD threading, MCP runtime dispatch, packaging, or Python environment behavior is fully safe.

When a change touches real behavior:

- Add a focused test if the code can be exercised without a live host.
- Add a targeted parser/contract test when changing serialized shapes, path normalization, registry identity, or bridge messages.
- Prefer a small regression test over broad placeholder coverage.
- If a live host is required and unavailable, document the missing manual/bridge verification clearly.
- Do not add brittle tests just to look comprehensive.

## Focused Checks

| Situation | Command |
|-----------|---------|
| Host compile for a year | `scripts/agent/build-host.ps1 -Year 2025` |
| Host compile for .NET Framework compatibility | `scripts/agent/build-host.ps1 -Year 2024` |
| All normal .NET tests | `scripts/agent/test-dotnet.ps1` |
| One .NET test project | `scripts/agent/test-dotnet.ps1 -Project tests/RevitDevTool.Execution.Tests/RevitDevTool.Execution.Tests.csproj` |
| Python parser tests | `scripts/agent/test-python.ps1` |
| Package pipeline | `scripts/agent/pack.ps1` |
| Collect local logs | `scripts/agent/collect-logs.ps1` |

## Indexing

GitNexus indexing is currently blocked by an analyzer `scopeResolution` failure. See `known-test-gaps.md`. Until fixed, inspect source directly with `rg` and keep docs current by source review.

## Frontend Sample

The only JS/TS app is `samples/PythonDemo/revit_dashboard_ui/`.

Run:

```powershell
npm run quality
```

## Reporting Failures

When verification cannot run, report the exact blocker: missing SDK, missing Pixi env, stale test path, Revit not running, named pipe unavailable, or missing sample build output.

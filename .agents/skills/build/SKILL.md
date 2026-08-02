---
name: build
description: >
  Compile, deploy, and test RevitDevTool after editing source/, tests/, or build/.
  Use when you changed .cs/.csproj/.xaml and need proof the solution still builds,
  or when deploy/MCP verification is required. Read this instead of guessing
  MSBuild flags.
---

# Build & Verify

Run proof **before** claiming done. Pick the smallest command that matches what you changed.

## Symptom → fix (fast path)

| Symptom | Likely cause | Do this |
|---------|--------------|---------|
| CS errors after editing `DevTools.*` | Shared multi-TFM break | Compile the **project you touched** (table below) |
| CS errors only on `net48` | Polyfill / API surface | Build same csproj with `-c Debug.Autodesk.2022` |
| MSB3027 / file locked / copy failed | Revit or AutoCAD running | `scripts/kill-host.ps1` then retry |
| Deploy silently didn't update DLL | Used `dotnet build` without deploy props | `scripts/kill-host.ps1`; `scripts/build-host.ps1 -Year 2025` |
| MCP tools = 0 in Cursor | Bad daemon `outputSchema` or stale bundle | Republish daemon; reload MCP — see `mcp-integration-test.md` |
| Parser / MCP test fails on missing DLL | Sample toolset not built | `dotnet build samples/McpToolsetDemo -c Debug.Autodesk.2025` |
| Parser test missing pixi env | `%APPDATA%\RevitDevTool\pixi-env` absent | `scripts/test-python.ps1` or `pixi install` at repo root |
| Unit test fails with UI/thread hint | `ConnectionState` needs main thread | See `known-test-gaps.md` — known `McpPipeConnectionTrackerTests` gap |
| Test looks for `RevitDevTool.sln` only | Stale local checkout | Root discovery accepts `.slnx` — see `known-test-gaps.md` |

## Compile-only (default after code edits)

Always pass deploy/repack off for API projects:

```text
-p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:IsRepackable=false
```

| You edited | Build command |
|------------|---------------|
| Shared `DevTools.*` / tests (multi-TFM) | `dotnet build <csproj> -c Debug --nologo` + props above if host csproj |
| `RevitDevTool` / Revit agents | `dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025` + props |
| `AcadDevTool` | `dotnet build source/AcadDevTool/AcadDevTool.csproj -c Debug.Autodesk.2025` + props |
| Unsure host year coverage | Also run `-c Debug.Autodesk.2022` and `.2027` (net48 + net10 spot-check) |
| `build/` or `install/` packaging | `-c Release` on affected project |

**One-liner examples:**

```powershell
dotnet build source/DevTools.Mcp.Server/DevTools.Mcp.Server.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:IsRepackable=false
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:IsRepackable=false
```

Focused MCP tests (no full solution):

```powershell
scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj
```

## Deploy & live host

| Goal | Commands |
|------|----------|
| Reload Revit add-in | `scripts/kill-host.ps1`; `scripts/build-host.ps1 -Year 2025` |
| Reload daemon (MCP stdio) | `dotnet publish source/DevTools.Daemon -c Release` (kills + deploys to bundle) |
| Package all years | `scripts/pack.ps1` |

## When proof is enough

- **Compile green** on touched projects → safe for shared/platform PRs.
- **+ focused test** when contracts/dispatch/parser changed (`scripts/test-dotnet.ps1 -Project …`).
- **+ live MCP checklist** when daemon/host wire or tool surface changed (`docs/agents/mcp-integration-test.md`).

If a test is listed in `known-test-gaps.md`, do not treat its failure as product regression without reading the gap. If live host unavailable, name the skipped checklist step.

## Reference

Full matrix: `docs/agents/build-matrix.md`. Extended commands: `docs/agents/verification.md`.

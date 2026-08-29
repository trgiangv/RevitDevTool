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
| MSB3027 / file locked / copy failed | Target host year is running | `scripts/kill-host.ps1 -HostApp Revit -Year 2025` (use the year being tested), then retry |
| Deploy silently didn't update DLL | Used `dotnet build` without deploy props | Stop only the target year; then `scripts/build-host.ps1 -Year 2025` |
| MCP tools = 0 in Cursor | Bad daemon `outputSchema` or stale bundle | Republish daemon; reload MCP — see `mcp-integration-test.md` |
| Host year starts but add-in missing / no pipe | Startup threw before FileLogProcessor | `%APPDATA%\RevitDevTool\{Year}\Logs\crash_*` then `docs/agents/verification.md` |
| Parser / MCP test fails on missing DLL | Sample toolset not built | `dotnet build samples/McpToolsetDemo -c Debug.Autodesk.2025` |
| Parser test missing pixi env | `%APPDATA%\RevitDevTool\pixi-env` absent | `scripts/test-python.ps1` or `pixi install` at repo root |
| Unit test fails with UI/thread hint | `ConnectionState` needs main thread | See `known-test-gaps.md` — known `McpPipeConnectionTrackerTests` gap |
| Test looks for `RevitDevTool.sln` only | Stale local checkout | Root discovery accepts `.slnx` — see `known-test-gaps.md` |

## Compile-only (default after code edits)

### When deploy / ILRepack flags matter

`Directory.Build.targets` imports `props/Revit.targets` / `AutoCad.targets` **only** if
the project sets `UseRevit=true` or `UseAutoCad=true`. Those targets own
`DeployRevitAddin` and `DeployAutoCadBundle`. `ILRepackable` lives in
`props/ILRepack.targets` (imported for every project; default false).

| Project | Needs compile-only `-p:…=false`? |
|---------|----------------------------------|
| Shared `DevTools.*` (no `UseRevit` / `UseAutoCad`) | **No** — flags are no-ops |
| `RevitDevTool`, `RevitDevTool.Core`, `DevTools.Mcp.Revit` | **Yes** — otherwise may deploy + ILRepack |
| `AcadDevTool`, `DevTools.Mcp.Acad` | **Yes** — same for AutoCAD bundle |
| Projects with their own `ILRepackable=true` (e.g. `DevTools.TestAdapter`) | Only `-p:ILRepackable=false` if you want to skip repack |

Do **not** paste deploy flags onto every shared library build.

Compile-only props (host / UseRevit|UseAutoCad only):

```text
-p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
```

`--nologo` is optional CLI noise reduction (hides the .NET banner). It does **not**
change build, deploy, or packaging. Prefer omitting it in docs/commands.

| You edited | Build command |
|------------|---------------|
| Shared `DevTools.*` / tests (multi-TFM) | `dotnet build <csproj> -c Debug` |
| `RevitDevTool` / `DevTools.Mcp.Revit` (compile only) | `dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025` + props above |
| `AcadDevTool` (compile only) | `dotnet build source/AcadDevTool/AcadDevTool.csproj -c Debug.Autodesk.2025` + props above |
| Unsure host year coverage | Also run `-c Debug.Autodesk.2022` and `.2027` (net48 + net10 spot-check) |
| `build/` or `install/` packaging | `-c Release` on affected project |

**One-liner examples:**

```powershell
# Shared library — no deploy props
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Debug
dotnet build source/DevTools.Mcp.Server/DevTools.Mcp.Server.csproj -c Debug

# Host entrypoint — compile only (do not deploy while host may be running)
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
```

Focused tests (no full solution). This repo is MTP except
`samples/ricaun.NUnit.SampleTests` (third-party VSTest comparison). Root
`global.json` has **no** `"test": { "runner": "Microsoft.Testing.Platform" }`
so that ricaun `dotnet test` stays VSTest. Do not treat in-repo tests as
VSTest (`FullyQualifiedName~`, `dotnet test` from repo root,
`scripts/test-dotnet.ps1` — removed).

| Surface | Command |
|---------|---------|
| In-repo `tests/*.Tests.csproj` | `dotnet run --project tests/<proj>/<proj>.csproj` then optional `-- --filter ClassName` |
| Product samples (`samples/DevTools.*.SampleTests`) | `cd` that folder (MTP `global.json`) then `dotnet test --project …` |
| `samples/ricaun.NUnit.SampleTests` | Comparison only. Not the product verify path. |

```powershell
dotnet run --project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj
dotnet run --project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj -- --filter ClassName
```

Do not force `--progress off`.

## Deploy & live host

| Goal | Commands |
|------|----------|
| Reload Revit add-in | `scripts/kill-host.ps1 -HostApp Revit -Year 2025`; `scripts/build-host.ps1 -Year 2025` |
| Reload daemon (MCP stdio) | `dotnet publish source/DevTools.Daemon -c Release` (kills + deploys to bundle) |
| Installer / all years | `scripts/pack.ps1` |
| TestAdapter NuGet | `scripts/pack-test-adapter.ps1` |

## When proof is enough

- **Compile green** on touched projects → safe for shared/platform PRs.
- **+ focused test** when contracts/dispatch/parser changed (`dotnet run --project tests/…/*.csproj`).
- **+ live MCP checklist** when daemon/host wire or tool surface changed (`docs/agents/mcp-integration-test.md`).

If a test is listed in `known-test-gaps.md`, do not treat its failure as product regression without reading the gap. If live host unavailable, name the skipped checklist step.

Never stop every Revit process. `kill-host.ps1` requires an exact host and year;
leave other running versions untouched.

## Reference

Full matrix: `docs/agents/build-matrix.md`. Extended commands: `docs/agents/verification.md`.

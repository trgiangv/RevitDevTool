---
name: build
description: >
  Compile, deploy, and test RevitDevTool after editing source/, tests/, or build/.
  Use when you changed .cs/.csproj/.xaml and need proof the solution still builds,
  or when deploy/MCP verification is required. Read this instead of guessing
  MSBuild flags.
---

# Build & Verify

Run proof **before** claiming done, in this order, and stop at the last step your
change actually reaches:

```text
1. Compile   the projects you touched
2. Test      focused in-repo tests (contracts, dispatch, parser, MSBuild surface)
3. Deploy    only when host/daemon runtime code changed
4. Pack      only when the installer or the TestAdapter NuGet surface changed
```

## 1. Compile

| You edited | Build command |
|------------|---------------|
| Shared `DevTools.*` / tests (multi-TFM) | `dotnet build <csproj> -c Debug` |
| `RevitDevTool` / `DevTools.Mcp.Revit` | `dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025` + compile-only props |
| `AcadDevTool` / `DevTools.Mcp.Acad` | `dotnet build source/AcadDevTool/AcadDevTool.csproj -c Debug.Autodesk.2025` + compile-only props |
| `build/` or `install/` packaging | `-c Release` on the affected project |
| Unsure about host-year coverage | Repeat with `-c Debug.Autodesk.2022` (net48) and `.2027` (net10) |

Compile-only props (host projects only):

```text
-p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
```

`Directory.Build.targets` imports `props/Revit.targets` / `AutoCad.targets` **only**
when the project sets `UseRevit=true` or `UseAutoCad=true`; those targets own
`DeployRevitAddin` / `DeployAutoCadBundle`. `ILRepackable` comes from
`props/ILRepack.targets` (imported everywhere, default false). So:

| Project | Needs the compile-only props? |
|---------|-------------------------------|
| Shared `DevTools.*` (no `UseRevit` / `UseAutoCad`) | **No** — the flags are no-ops |
| `RevitDevTool`, `RevitDevTool.Core`, `DevTools.Mcp.Revit`, `AcadDevTool`, `DevTools.Mcp.Acad` | **Yes** — otherwise the build deploys and ILRepacks |
| Projects with their own `ILRepackable=true` (e.g. `DevTools.TestAdapter`) | Only `-p:ILRepackable=false` to skip repack |

```powershell
# Shared library — no deploy props
dotnet build source/DevTools.Testing.Host/DevTools.Testing.Host.csproj -c Debug

# Host entrypoint — compile only (do not deploy while the host may be running)
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
```

## 2. Test

Everything in-repo is MTP except `samples/ricaun.NUnit.SampleTests` (third-party
VSTest comparison sample). Root `global.json` selects MTP; that sample has
its own `"runner": "VSTest"` and must be run from its folder. Never use VSTest
syntax (`FullyQualifiedName~`) on MTP projects.

| Surface | Command |
|---------|---------|
| In-repo `tests/*.Tests.csproj` | `dotnet run --project tests/<proj>/<proj>.csproj` then optional `-- --filter ClassName`. Line coverage: `-- --coverlet` (see `docs/agents/test-matrix.md`). `dotnet test --project` from repo root also works (MTP). |
| Product samples (`samples/DevTools.*.SampleTests`) | `dotnet test --project samples/…` from repo root |
| `samples/ricaun.NUnit.SampleTests` | Comparison only. `cd` that folder first. Not a verify path. |

```powershell
dotnet run --project tests/DevTools.Mcp.Core.Tests/DevTools.Mcp.Core.Tests.csproj
dotnet run --project tests/DevTools.TestAdapter.Tests/DevTools.TestAdapter.Tests.csproj
```

Do not force `--progress off`.

## 3. Deploy & live host

| Goal | Commands |
|------|----------|
| Reload Revit add-in | `scripts/kill-host.ps1 -HostApp Revit -Year 2025`; `scripts/build-host.ps1 -Year 2025` |
| Reload daemon (MCP stdio) | `dotnet publish source/DevTools.Daemon -c Release` (kills + deploys to the bundle) |

Never stop every Revit process. `kill-host.ps1` requires an exact host and year;
leave other running versions untouched.

## 4. Pack

| Artifact | Command |
|----------|---------|
| Installer / all years | `scripts/pack.ps1` |
| TestAdapter NuGet | `scripts/pack-test-adapter.ps1` |

Pack writes `output/nuget` (repo `NuGet.config` maps `RevitDevTool.TestAdapter`
there) and deletes `%USERPROFILE%\.nuget\packages\revitdevtool.testadapter\<version>`
so a consumer restore cannot pick up the previous extraction of the same version.
Consumer-side proof (restore from that nupkg + `--list-tests` on net48 / net8 /
net10) is in `docs/agents/host-testing.md`.

## When proof is enough

- **Compile green** on touched projects → enough for shared/platform PRs.
- **+ focused test** when contracts, dispatch, parser, or MSBuild surface changed.
- **+ live MCP checklist** when the daemon/host wire or tool surface changed
  (`docs/agents/mcp-integration-test.md`).

If a surface is live/opt-in/Skip in `docs/agents/test-matrix.md`, a headless Skip is
not a product regression. When a live host is unavailable, name the skipped step.

## Symptom → fix

| Symptom | Likely cause | Do this |
|---------|--------------|---------|
| CS errors after editing `DevTools.*` | Shared multi-TFM break | Compile the project you touched |
| CS errors only on `net48` | Polyfill / API surface | Same csproj with `-c Debug.Autodesk.2022` |
| MSB3027 / file locked | Target host year is running, or a second MTP testhost holds `tests/*/bin` | Host: `scripts/kill-host.ps1 -HostApp Revit -Year 2025`. Testhost: do not spawn a second Coverlet (`test-matrix.md`) |
| Deploy did not update the DLL | Built with the compile-only props | Stop only the target year, then `scripts/build-host.ps1 -Year 2025` |
| MCP tools = 0 in Cursor | Bad daemon `outputSchema` or stale bundle | Republish the daemon, reload MCP (`mcp-integration-test.md`) |
| Host starts but add-in missing / no pipe | Startup threw before FileLogProcessor | `%APPDATA%\RevitDevTool\{Year}\Logs\crash_*`, then `docs/agents/verification.md` |
| Parser / MCP test fails on a missing DLL | Sample toolset not built | `dotnet build samples/McpToolsetDemo -c Debug.Autodesk.2025` |
| Parser test cannot find the pixi env | `%APPDATA%\RevitDevTool\pixi-env` absent | `scripts/test-python.ps1` or `pixi install` at repo root |
| Unit test fails with a UI/thread hint | Dispatcher/UI assumed in host code | Headless path must run inline (`HostUiHelper.RunOnMainThread`), see `test-matrix.md` |

## Reference

Full matrix: `docs/agents/build-matrix.md`. Extended commands: `docs/agents/verification.md`.

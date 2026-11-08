---
name: build
description: >
  Compile, test, deploy, and pack RevitDevTool with the right MSBuild
  configuration and flags. Use after editing source/, tests/, or build/, when you
  need proof the change builds, or when a host, Daemon, or package must be redeployed.
---

# Build and Verify

Run proof in this order and stop at the last step your change reaches:

```text
1. Compile   the projects you touched
2. Test      focused in-repo tests (contracts, dispatch, parser, MSBuild surface)
3. Deploy    only when host or Daemon runtime code changed
4. Pack      only when the installer or the TestAdapter NuGet surface changed
```

When the task's edits are finished, run `pwsh -File scripts/format-lint.ps1` once; it formats the
changed `.cs` and `.xaml` files ([formatting-linting.md](../../../docs/agents/formatting-linting.md)).

## 1. Compile

| You edited | Build command |
|------------|---------------|
| Shared `DevTools.*` or tests (multi-TFM) | `dotnet build <csproj> -c Debug` |
| `RevitDevTool` / `DevTools.Mcp.Revit` | `dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025` + compile-only props |
| `AcadDevTool` / `DevTools.Mcp.Acad` | `dotnet build source/AcadDevTool/AcadDevTool.csproj -c Debug.Autodesk.2025` + compile-only props |
| `build/` packaging | `-c Release` on the affected project |
| Code sensitive to the host year | Repeat with `-c Debug.Autodesk.2022` (net48) and `.2027` (net10) |

Compile-only props, for host projects:

```text
-p:DeployAddin=false -p:ILRepackable=false
```

`Directory.Build.targets` imports `props/Revit.targets` / `AutoCad.targets` only
when the project sets `UseRevit=true` or `UseAutoCad=true`, and those targets own
`DeployAddin`. `ILRepackable` comes from `props/ILRepack.targets` (imported
everywhere, default false). So:

| Project | Compile-only props |
|---------|--------------------|
| Shared `DevTools.*` (no `UseRevit` / `UseAutoCad`) | Not needed; the flags do nothing |
| `RevitDevTool`, `RevitDevTool.Core`, `DevTools.Mcp.Revit`, `AcadDevTool`, `DevTools.Mcp.Acad` | Needed, otherwise the build deploys and ILRepacks |
| Projects with their own `ILRepackable=true` (e.g. `DevTools.TestAdapter`) | Only `-p:ILRepackable=false`, to skip repack |

```powershell
# Shared library — no deploy props
dotnet build source/DevTools.Testing.Host/DevTools.Testing.Host.csproj -c Debug

# Host entrypoint — compile only, safe while the host may be running
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployAddin=false -p:ILRepackable=false
```

## 2. Test

Everything in-repo runs on Microsoft Testing Platform (MTP); root `global.json`
selects it. Filter with MTP syntax (`--filter ClassName`); VSTest expressions
such as `FullyQualifiedName~` match nothing.

| Surface | Command |
|---------|---------|
| In-repo `tests/*.Tests.csproj` | `dotnet run --project tests/<proj>/<proj>.csproj`, optionally `-- --filter ClassName`. `dotnet test --project` from the repo root also works. Coverage: [test-matrix.md](../../../docs/agents/test-matrix.md#line-coverage) |
| Product samples (`samples/DevTools.*.SampleTests`) | `dotnet test --project samples/…` from the repo root |
| `samples/ricaun.NUnit.SampleTests` | Third-party VSTest comparison, not a verify path. Its own `global.json` sets VSTest; `cd` to that folder first |

```powershell
dotnet run --project tests/DevTools.Mcp.Core.Tests/DevTools.Mcp.Core.Tests.csproj
dotnet run --project tests/DevTools.TestAdapter.Tests/DevTools.TestAdapter.Tests.csproj
```

Keep the default progress output (no `--progress off`).

Run one testhost per test project. A second `dotnet run` on a project whose
testhost is live fails with `MSB3027` on `tests/*/bin`; wait for it or skip that project.

## 3. Deploy and live host

| Goal | Command |
|------|---------|
| Reload the Revit add-in | `scripts/build-host.ps1 -HostApp Revit -Year 2025` |
| Several hosts or years | `scripts/build-host.ps1 -HostApp Revit,AutoCAD -Year 2025,2026` ([build-matrix.md](../../../docs/agents/build-matrix.md#host-build-script)) |
| Reload the Daemon (MCP stdio) | `dotnet publish source/DevTools.Daemon -c Release` (stops it and deploys to the bundle) |

`scripts/build-host.ps1` stops only the `-HostApp` and `-Year` it builds. Never
stop every Revit process: other running versions may hold someone's work.

## 4. Pack

| Artifact | Command |
|----------|---------|
| Add-in payloads, `update-manifest.json`, bundle zip in `output/addin` | `dotnet run --project build -- pack addin` |
| Velopack `RevitDevTool-Setup.exe`, nupkg, `releases.*.json` in `output/daemon` | `dotnet run --project build -- pack daemon` |
| TestAdapter NuGet in `output/nettest` | `dotnet run --project build -- pack nettest` |
| Compile and host-free tests of one product, as CI runs them | `dotnet run --project build -- ci <daemon\|addin\|nettest\|pytest>` |

`dotnet run --project build` with no target prints the targets and products (ADR 0059 §10).
Every pack is `0.0.0-local.<UTC stamp>`, so it never collides with a published or
earlier local package. The repo `NuGet.config` maps `RevitDevTool.TestAdapter` to
`output/nettest` and the samples float on `0.0.0-local.*`, so the next sample build
restores the newest pack. Consumer-side proof (restore from that nupkg and
`--list-tests` on net48, net8, net10) is in `docs/agents/host-testing.md`.

## When proof is enough

- Compile green on touched projects: enough for shared and platform refactors.
- Add a focused test when contracts, dispatch, parsers, or the MSBuild surface changed.
- Add the live checklist in `docs/agents/mcp-integration-test.md` when the Daemon or host wire or tool surface changed.

A headless Skip listed in `docs/agents/test-matrix.md` is not a regression. When
a live host is unavailable, name the step you skipped.

## Symptom to fix

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| CS errors after editing `DevTools.*` | Shared multi-TFM break | Compile the project you touched |
| CS errors only on `net48` | Polyfill or API surface | Same csproj with `-c Debug.Autodesk.2022` |
| MSB3027, file locked | The target host year is running, or a second testhost holds `tests/*/bin` | Host: `scripts/build-host.ps1 -HostApp Revit -Year 2025`. Testhost: wait for the running one |
| `build/` fails with `'Solutions' does not contain a definition for 'RevitDevTool'` | A second checkout nested under the repo root (a git worktree or a copied repo) gives Sourcy two of every solution and project | Keep worktrees outside the repo folder; `git worktree list`, then `git worktree remove` the nested one |
| Deploy did not update the DLL | Built with the compile-only props | `scripts/build-host.ps1 -HostApp Revit -Year 2025` |
| MCP client lists 0 tools | Bad Daemon `outputSchema` or stale bundle | Republish the Daemon and reload MCP (`mcp-integration-test.md`) |
| Host starts but add-in missing, no pipe | Startup threw before FileLogProcessor | `%APPDATA%\RevitDevTool\{Year}\Logs\crash_*`, then `docs/agents/verification.md` |
| Parser or MCP test fails on a missing DLL | Sample toolset not built | `dotnet build samples/McpToolsetDemo -c Debug.Autodesk.2025` |
| Parser test cannot find the pixi env | `%APPDATA%\RevitDevTool\pixi-env` absent | `scripts/test-pyserver.ps1` or `pixi install` at repo root |
| Unit test fails with a UI or thread hint | Host code assumed a dispatcher | The headless path runs inline (`HostUiHelper.RunOnMainThread`); see `test-matrix.md` |

All host years and TFMs: `docs/agents/build-matrix.md`. More commands: `docs/agents/verification.md`.

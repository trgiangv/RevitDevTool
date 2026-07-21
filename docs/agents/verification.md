# Verification

Prefer scripts under `scripts/` and the Cursor compile hook. Do not guess command strings.

## Compile Hook (default)

After agent edits under `source/`, `tests/`, `build/`, or `install/`, the stop hook compiles queued projects.

Hook uses **3 representative years** (`2022`, `2025`, `2027` — one per TFM: net48 / net8 / net10), not the full six-year matrix. All `2022`–`2027` configs remain supported for manual and CI builds; see `build-matrix.md`.

| Edit target | Configs (compile-only, deploy off) |
|-------------|-------------------------------------|
| `UseRevit` projects | `{Debug\|Release}.Autodesk.2022`, `.2025`, `.2027` |
| `UseAutoCad` projects | same year matrix |
| Shared / multi-TFM | `Debug` (or `Release` if edits were under `build/`/`install/`) |

Manual compile-only when the hook did not run:

```powershell
dotnet build source/DevTools.Execution/DevTools.Execution.csproj -c Debug -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:IsRepackable=false
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:IsRepackable=false
```

## Deploy / package

| Situation | Command |
|-----------|---------|
| Kill host locks | `scripts/kill-host.ps1` / `-HostApp Revit\|AutoCAD` |
| Build + deploy one year | `scripts/kill-host.ps1`; `scripts/build-host.ps1 -Year 2025` |
| .NET tests | `scripts/test-dotnet.ps1` |
| One .NET test project | `scripts/test-dotnet.ps1 -Project <csproj>` |
| Python parser tests (this repo) | `scripts/test-python.ps1` (requires `pixi`; repo-root `pixi.toml`) |
| Package pipeline | `scripts/pack.ps1` |
| Collect logs | `scripts/collect-logs.ps1` |

## Client pytest (RevitDevTool.PyTest)

Always use **uv** from the PyTest repo root. Do not search for system Python or run bare `pytest`.

```powershell
cd c:\Users\truon\source\repos\RevitDevTool.PyTest
uv run pytest -v
uv run pytest tests/Revit/<file>.py -v
uv run pytest --host-version=2025 -v
```

## Test Reality

Smoke/parser tests are one signal, not full host/MCP/packaging proof. Add focused tests when behavior changes; if a live host is required and unavailable, document the blocker.

## Frontend Sample

`samples/PythonDemo/revit_dashboard_ui/`: `npm run quality`

## Reporting Failures

Report the exact blocker: missing SDK, missing Pixi env, stale test path, Revit not running, named pipe unavailable, or hook compile errors already injected.

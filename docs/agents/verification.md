# Verification

Commands and traps. Symptom-first table: `.agents/skills/build/SKILL.md`.
Known test failures: `known-test-gaps.md`. Do not invent MSBuild flags.

## After code edits

Compile the **csproj you changed**. Deploy/repack `-p:…=false` only for projects
with `UseRevit` / `UseAutoCad` (or their own `ILRepackable=true`). Shared
`DevTools.*` does not import those targets — omit the props. Details:
`.agents/skills/build/SKILL.md`.

```powershell
# Shared library
dotnet build <path/to/DevTools.*.csproj> -c Debug

# Host entrypoint (compile only)
dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false
```

| Project kind | Config |
|--------------|--------|
| `UseRevit` / `UseAutoCad` | `Debug.Autodesk.2025` (spot-check `2022` + `2027` if TFM-sensitive) |
| Shared multi-TFM `DevTools.*` | `Debug` |
| `build/` / `install/` | `Release` |

Host API matrix (all years): `build-matrix.md`.

## Scripts

| Situation | Command |
|-----------|---------|
| File lock / deploy failed | `scripts/kill-host.ps1 -HostApp Revit -Year <year>` |
| Build + deploy one year | Stop only that year; `scripts/build-host.ps1 -Year 2025` |
| .NET tests | `dotnet run --project tests/<project>/<project>.csproj` |
| MCP tests | `dotnet run --project tests/DevTools.Mcp.Tests/DevTools.Mcp.Tests.csproj` |
| Python parser (this repo) | `scripts/test-python.ps1` |
| Installer / bundle | `scripts/pack.ps1` |
| TestAdapter NuGet | `scripts/pack-test-adapter.ps1` |
| Daemon publish | `dotnet publish source/DevTools.Daemon -c Release` |

Build sample toolsets before parser/spike tests — see `known-test-gaps.md`.

## Proof bar

| Change type | Minimum proof |
|-------------|---------------|
| Refactor / local fix | Compile touched projects |
| Wire contract / parser | + focused test project (check gaps table first) |
| MCP daemon/host surface | + `mcp-integration-test.md` when host available |

Passing smoke tests ≠ live host proof. Name blockers with test name + missing path.

## Diagnostic logs

When investigating host or MCP failures, read the **newest** log for the process
under test — do not guess paths.

| Process | Default folder | Newest file |
|---------|----------------|-------------|
| Host (Revit / AutoCAD) | `%APPDATA%\RevitDevTool\{Year}\Logs\` | Highest `LastWriteTime` matching `log_*` (`.log` or `.json`) |
| Daemon | `%APPDATA%\RevitDevTool\mcp-server\` | Highest `LastWriteTime` matching `log_*.log` |

`{Year}` is the host product year (e.g. `2025`). Host folder may differ if the user
changed **Settings → Logging**; Daemon always uses `mcp-server\`. Match **PID** in the
filename when several sessions are open.

```powershell
# Host (adjust year)
Get-ChildItem "$env:APPDATA\RevitDevTool\2025\Logs\log_*" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

# Daemon
Get-ChildItem "$env:APPDATA\RevitDevTool\mcp-server\log_*.log" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

# Both trees — list 50 newest under %APPDATA%\RevitDevTool
scripts/collect-logs.ps1
```

Reproduce the failure before collecting; cite log path + relevant lines in the report.

## Frontend sample

`samples/PythonDemo/revit_dashboard_ui/`: `npm run quality`

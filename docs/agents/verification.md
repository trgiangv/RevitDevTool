# Verification

Command reference and diagnostic logs. Which projects to compile, with which
flags, and how much proof is enough: `.agents/skills/build/SKILL.md`. Known test
gaps and expected Skips: [test-matrix.md](test-matrix.md). Host years and TFMs:
[build-matrix.md](build-matrix.md).

## Commands

| Situation | Command |
|-----------|---------|
| Compile a shared library | `dotnet build <path/to/DevTools.*.csproj> -c Debug` |
| Compile a host entrypoint without deploying | `dotnet build source/RevitDevTool/RevitDevTool.csproj -c Debug.Autodesk.2025 -p:DeployAddin=false -p:ILRepackable=false` |
| File lock, or deploy one or more host years | `scripts/build-host.ps1 -HostApp Revit -Year 2025` (stops only those years, then builds) |
| Build and deploy several hosts and years | `scripts/build-host.ps1 -HostApp Revit,AutoCAD -Year 2025,2026` ([build-matrix.md](build-matrix.md#host-build-script)) |
| .NET tests | `dotnet run --project tests/<project>/<project>.csproj` |
| Execution tests | `dotnet run --project tests/DevTools.Execution.<Scope>.Tests/DevTools.Execution.<Scope>.Tests.csproj` (CSharp, FSharp, Python, IronPython, Pytest, Mcp, Services). Coverage: [test-matrix.md](test-matrix.md#line-coverage) |
| MCP tests | `dotnet run --project tests/DevTools.Mcp.<Module>.Tests/DevTools.Mcp.<Module>.Tests.csproj` (Core, Catalog, Adapter, Client, Server). Build the sample toolsets first or parser tests Skip |
| Python parser (this repo) | `scripts/test-pyserver.ps1` |
| A product as CI runs it | `dotnet run --project build -- ci <daemon\|addin\|nettest\|pytest>` |
| Add-in bundle / Daemon installer | `dotnet run --project build -- pack addin` / `pack daemon` |
| TestAdapter NuGet | `dotnet run --project build -- pack nettest` (writes a `0.0.0-local.<stamp>` nupkg to `output/nettest` with the runner at `tools/runner/DevTools.TestRunner.exe`). Runner changes need this pack; there is no runner-path override |
| Release check | `dotnet run --project build -- release <product> --dry-run`; steps in [release.md](release.md) |
| TestAdapter consumer surface (net48 / net8 / net10 build and host-free discovery) | `scripts/test-adapter-matrix.ps1` |
| Daemon publish | `dotnet publish source/DevTools.Daemon -c Release` |
| Frontend sample `samples/PythonDemo/revit_dashboard_ui/` | `pnpm check` |

Passing smoke tests are not live host proof. Name a blocker by test name and missing path.

## Diagnostic logs

Reproduce the failure, then read the newest log for the process under test and
cite its path and the relevant lines.

| Process | Default folder | Newest file |
|---------|----------------|-------------|
| Host startup crash (add-in failed to load) | `%APPDATA%\RevitDevTool\{Year}\Logs\` | Highest `LastWriteTime` matching `crash_*` |
| Host session (Revit / AutoCAD) | `%APPDATA%\RevitDevTool\{Year}\Logs\` | Highest `LastWriteTime` matching `log_*` (`.log` or `.json`) |
| Daemon | `%APPDATA%\RevitDevTool\logs\` | Highest `LastWriteTime` matching `log_*.log` |

`{Year}` is the host product year (e.g. `2025`). The host folder differs if the
user changed Settings → Logging; the Daemon always uses `logs\` (`Logging:File`).
Match the PID in the file name when several sessions are open. AutoClean deletes
only `log_*`, so `crash_*` stays until you delete it.

Assembly-isolation bind lines (`Testing assembly isolation`, command and script
isolation diagnostics) and MCP catalog addition counts appear only in Debug host
builds; a Release install logs only rejections. To see them, deploy that year in
Debug: `scripts/build-host.ps1 -HostApp Revit -Year 2025 -Mode Debug`.

```powershell
# Add-in failed during OnStartup / ApplicationInitialized
Get-ChildItem "$env:APPDATA\RevitDevTool\2025\Logs\crash_*" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

# Host session logs (adjust year)
Get-ChildItem "$env:APPDATA\RevitDevTool\2025\Logs\log_*" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

# Daemon
Get-ChildItem "$env:APPDATA\RevitDevTool\logs\log_*.log" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1

# Host logs, custom LogFolder, code_mode temp files, and Revit journals
scripts/collect-logs.ps1
```

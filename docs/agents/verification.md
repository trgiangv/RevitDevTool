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
| Add-in bundle / Daemon installer | `dotnet run --project build -- compile addin`, then `pack addin` / `pack daemon` |
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

### Model Context diagnostics

The Model Context panel logs through the host `ILogger` under the category
`RevitDevTool.Tools.ModelsContext.ModelsContextPane`. A failure the panel shows the
user is logged at Error with its exception. Everything else is Trace, which the
default minimum (Debug) drops before any formatting.

To see Trace lines, set Settings → Logging level to `Trace`, reproduce, then set it
back. They go wherever host log entries go, including the file log. Lines to look for:

- `Revit <Operation>: queue N ms, run M ms`: one Revit-thread call. A long queue
  means Revit was busy; a long run means the call itself stalled Revit. Inside a
  sliced operation only slices that run 250 ms or more get their own line.
- `Revit Read|Count|Catalog|Match|Values|Hidden <document>: N items in S slices,
  queue … run … (max …)`: one sliced operation, summed.
- `Snapshot`, `Snapshot reload`, `Parameter catalog`, `Value counts`, `Filter`,
  `Visibility`, `Context load`, `Selection sent/received`, `Explorer publish/expand`:
  panel steps with their size and time.
- `Filter <name> in <document>`: one document's match count and time;
  `Filter <name>: Live paused at N matches`: Live stopped sending above the limit.
- `Filter compiled <document>: … compile N ms, candidates M ms (cached|collected|skipped)`
  and `Match <document>: C candidates, N native, M matched`: one document's
  compile, its candidate ids (cached per document, categories and version), and
  the native and exact match counts.
- Every line names a document by its file name (`SourceId.Label`), for the
  Current model and links alike.
- `Selection mismatch`: Revit's selection echo differed from what the panel sent.
- `Parameters merged`, `Deltas applied`: parameter list refresh (catalogs,
  parameters, cached scopes) and one batch of model edits applied to snapshots.
- `<Operation> <subject>: error ignored` and `<Operation> <subject>: N of M failed`:
  errors the panel skipped (the exception is attached).

`dotnet run --project tests/DevTools.ModelsContext.Sandbox -- --measure --trace` prints the same panel
Trace lines to the console without Revit.

Known gap: host log routing is global. Any `ILogger` entry at or above the minimum
level also reaches the user output panel through `NotifyLoggerProvider`
(`IsEnabled` is true for every level but None) and can open the RevitDevTool pane
(`PanelController.OnTraceReceived`). The minimum level is one key for every
provider, with no per-category filter. So a Model Context Error, or every Trace
line while the level is `Trace`, also shows in the output panel. This stays as it
is: Model Context Trace lines show only at the `Trace` setting.

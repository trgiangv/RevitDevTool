# RevitDevTool Processor

Desktop UI for Revit batch execution built with Avalonia + Semi.Avalonia.
It integrates the existing in-process Console core (`BatchRunner`, `ConfigService`, `RevitDiscovery`, `RevitLauncher`).

## What it does

- Load and resolve batch config JSON into an execution plan.
- Run jobs using attach/launch strategies from the existing Console core.
- Show realtime progress per host instance.
- Stream host logs (`PipeLogEntry`) and diagnostics.
- Surface crash-risk diagnostics (`[CrashRisk]`) in UI.
- Show batch result summary and per-job details.

## Architecture

- UI: `MainWindow.axaml` + `MainWindowViewModel`
- Service boundary: `Services/IBatchExecutionService`, `Services/BatchExecutionService`
- Core integration: `RevitDevTool.Console` and `RevitDevTool.Bridge` project references
- Theme: `App.axaml` with `SemiTheme` and local token/style dictionaries under `Theme/`

## Build

```bash
dotnet build source/RevitDevTool.Processor/RevitDevTool.Processor.csproj -c Release
```

## Run

```bash
dotnet run --project source/RevitDevTool.Processor/RevitDevTool.Processor.csproj
```

## UX Flow

1. Select config file (`*.json`).
2. Load/Preview plan.
3. Adjust mode/parallel/launch overrides.
4. Run and monitor progress/logs/results.
5. Cancel if needed.

## Notes

- Cancellation follows current Console behavior (best effort from orchestrator side).
- Launch mode includes Win32 startup dialog resolving and crash watcher logic from Console services.

# RevitDevTool Console

CLI for batch processing Revit files through named pipes (`EngineHost` inside Revit).
Supports both attach to existing Revit instances and launching new ones, with Win32 startup dialog auto-resolve and crash-risk monitoring.

## Prerequisites

- Revit 2022+ installed (launcher resolves `C:\Program Files\Autodesk\Revit <version>\Revit.exe`)
- RevitDevTool addin available in target Revit instances

## Build and Run

```bash
dotnet build source/RevitDevTool.Console/RevitDevTool.Console.csproj -c Release
dotnet run --project source/RevitDevTool.Console/RevitDevTool.Console.csproj -- <command> [options]
```

## Commands

### `status` - list running instances and installed versions

```bash
dotnet run --project source/RevitDevTool.Console/RevitDevTool.Console.csproj -- status
```

### `info` - inspect `.rvt` files from config

```bash
dotnet run --project source/RevitDevTool.Console/RevitDevTool.Console.csproj -- info --config batch.json
```

Prints basic file info including Revit version, worksharing mode, external links, and non-link references.

### `sample` - generate sample config

```bash
dotnet run --project source/RevitDevTool.Console/RevitDevTool.Console.csproj -- sample
dotnet run --project source/RevitDevTool.Console/RevitDevTool.Console.csproj -- sample > my-batch.json
```

### `run` - execute batch

```bash
dotnet run --project source/RevitDevTool.Console/RevitDevTool.Console.csproj -- run \
  --config <path>       # -c, required
  --mode <mode>         # -m, single|multi|parallel
  --parallel <n>        # -p, instances per version in parallel mode
  --launch              # force launch mode
  --dry-run             # resolve plan only, no execution
  --json                # print BatchResult as JSON
```

## Runtime Features (current behavior)

### Connection and dispatch

- Attach mode discovers existing pipes (`RevitDevTool_revit_<version>_<pid>`)
- Launch mode starts Revit and waits for the exact expected pipe by PID
- Processing modes:
  - `sequentialSingle`: one instance for all jobs
  - `sequentialMulti` (default): one-by-one, routed by `hostVersion`
  - `parallel`: concurrent jobs, round-robin across N instances per version

### Win32 startup dialog auto-resolve (launch mode)

- Polls top-level dialog windows (`#32770`) for the launched Revit PID
- Targets known add-in security/loading dialogs by title keywords:
  - `add-in`, `addin`, `questionable add-in`, `unsigned add-in`
- Clicks preferred safe buttons:
  - `always load`, `load`, `ok`, `yes`, `close`, `continue`
- Fails fast if a matched dialog is found but no whitelisted button is found after retries

### Crash-risk detection and fail-fast

- Win32 crash watcher runs during:
  - launch wait (before pipe ready)
  - job execution wait (before job response)
- Detects:
  - process exited/disappeared unexpectedly
  - Revit crash dialogs (including WER-related dialogs)
- Emits explicit error context (`[crash-watcher:<version>:<pid>] ...`)
- Batch-level health report emits `[CrashRisk]` when:
  - a managed PID remains alive after shutdown flow
  - an unexpected Revit PID appears (not baseline, not managed)

### Shutdown self-healing

- `closeHost=true` triggers graceful shutdown request over pipe
- Waits for `ShutdownAck`, then waits for process exit
- If still alive, orchestrator retries `CloseMainWindow` and logs risk if still abnormal

### Host telemetry and logs

- `LogChunk` messages from Revit are printed to console:
  - `[HostLog] [Level] message`
  - includes source prefix when available (`[HostLog:<source>]`)
- Progress messages are streamed via `PipeProgress`

## Common Scenarios

### Attach to running Revit

```bash
dotnet run --project source/RevitDevTool.Console/RevitDevTool.Console.csproj -- run -c batch.json
```

Use `strategy.connectionMode = "attach"` (default).

### Launch Revit automatically

```bash
dotnet run --project source/RevitDevTool.Console/RevitDevTool.Console.csproj -- run -c batch.json --launch
```

Equivalent to `strategy.connectionMode = "launch"`.

### Dry-run only

```bash
dotnet run --project source/RevitDevTool.Console/RevitDevTool.Console.csproj -- run -c batch.json --dry-run
```

### Machine-readable batch result

```bash
dotnet run --project source/RevitDevTool.Console/RevitDevTool.Console.csproj -- run -c batch.json --json
```

## Batch Config Schema

```json
{
  "strategy": {
    "connectionMode": "attach",
    "mode": "sequentialMulti",
    "parallelCount": 2,
    "launchTimeoutSeconds": 120,
    "timeoutPerFileSeconds": 1800
  },
  "defaults": {
    "hostVersion": "2025",
    "script": "path/to/script.py",
    "headless": true,
    "audit": false,
    "detachFromCentral": "detachAndPreserveWorksets",
    "workset": "openAllWorksets",
    "allowOpeningLocalByWrongUser": true,
    "ignoreExtensibleStorageSchemaConflict": true,
    "closeDocument": true,
    "closeHost": false
  },
  "files": [
    { "path": "F:/Project1.rvt" },
    { "path": "F:/Project2.rvt", "headless": false, "closeDocument": false }
  ]
}
```

### `strategy`

| Field | Default | Description |
|---|---|---|
| `connectionMode` | `"attach"` | `"attach"` (use running host) or `"launch"` (start new host) |
| `mode` | `"sequentialMulti"` | `sequentialSingle`, `sequentialMulti`, `parallel` |
| `parallelCount` | `2` | instances per version in parallel mode |
| `launchTimeoutSeconds` | `120` | max launch/pipe wait |
| `timeoutPerFileSeconds` | `1800` | per-job orchestration timeout |

### `defaults` and per-file overrides

Per-file entries inherit from `defaults`.

| Field | Default | Description |
|---|---|---|
| `hostVersion` | auto-detect | target Revit version, e.g. `"2025"` |
| `script` | required | script path (`.py` or `.dll`) |
| `headless` | `true` | open by Application API (`true`) or UI API (`false`) |
| `audit` | `false` | audit open |
| `detachFromCentral` | `"detachAndPreserveWorksets"` | detach behavior |
| `workset` | `"openAllWorksets"` | workset open mode |
| `allowOpeningLocalByWrongUser` | `true` | open local even with user mismatch |
| `ignoreExtensibleStorageSchemaConflict` | `true` | ignore schema conflict |
| `closeDocument` | `true` | close document after job |
| `closeHost` | `false` | close Revit host after applicable jobs |

## CLI override priority

```text
CLI arg > JSON strategy > hardcoded default
```

Only `--mode`, `--parallel`, and `--launch` override strategy from CLI.

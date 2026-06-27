# Plugin Internals

## Architecture Overview

```
pytest (local)                      Host (remote)
  ├─ plugin.py (hooks)              ├─ RevitDevTool add-in
  ├─ connection.py (lifecycle)      │   ├─ Named Pipe server
  ├─ bridge.py (Named Pipe RPC)  ──►│   ├─ PytestDependencyService
  ├─ discovery.py (find host)      │   ├─ pythonnet execution
  ├─ reporting.py (result mapping)  │   └─ BridgePipeConnection
  ├─ suite_lock.py (Windows Mutex)  │
  ├─ suite_leasing.py (instance)    │
  └─ dialog_resolver.py (UI auto)   │
```

## Named Pipe Protocol

Wire format: `[4-byte LE body length][UTF-8 JSON body]`

Matches `DevTools.Ipc.BridgePipeConnection` on C# side.

Pipe name pattern: `{Host}_{Version}_{PID}` (e.g. `Revit_2025_12345`, `AutoCad_2026_7890`, `Rhino_8.0_9999`)

## Connection Flow

1. `plugin.pytest_runtestloop` calls `_ensure_bridge(session, host_name)`
2. `connection.ensure_bridge()` resolves bridge via:
   - Reuse existing connected bridge
   - Explicit pipe (`--host-pipe`)
   - Auto-discovery: scan `//./pipe` for `{Host}_{Version}_{PID}` patterns
   - Lease store: reconnect to previously-leased instance
   - Auto-launch: start host + dialog resolver + wait for pipe
3. Suite mutex prevents parallel pytest processes on same suite

## Bridge RPC Methods

| Method | Request | Response |
|--------|---------|----------|
| `tests/discover` | `DiscoverRequest(workspace_root, test_root, pytest_args)` | `DiscoverResponse(rootdir, nodeids, collection_errors)` |
| `tests/run` | `RunRequest(workspace_root, test_root, nodeids, pytest_args)` | `RunResponse(exit_code, summary, results, collection_errors)` |

## Output Capture Flow

```
test_foo.py              Host (PytestRunner.py)           Named Pipe             Local pytest
  print("x")  ────►  _BridgePlugin.pytest_runtest_     ────►  progress        ────►  reporting.py
  assert ...          logreport() captures each phase          notification           _emit_streaming_report()
                      → _CaseResult(stdout, stderr,            per CaseResult         → pytest_runtest_logreport
                         traceback, message, outcome)                                 → terminal output
                      → _echo_to_log_viewer()
                         (Host Log Viewer)
                      → _emit_progress() → JSON
```

**PytestRunner.py** (`_BridgePlugin`) hooks into:
- `pytest_runtest_logreport` — captures each phase result (setup/call/teardown)
- `pytest_collectreport` — captures collection errors
- `pytest_collection_modifyitems` — collects discovered nodeids

Each `_CaseResult.to_dict()` → JSON → `__progress_callback__` → C# `PytestRequestHandler.CreateProgressCallback()` → `SendNotification("notifications/tests/progress", json)` → pipe → local `reporting.py._emit_streaming_report()`.

**Streaming vs Batch:**
- CLI mode: `on_notification` callback emits live `pytest_runtest_logreport` per test
- IDE adapter (detected via `vscode_pytest` plugin or `TEST_RUN_PIPE` env): streaming disabled, all results emitted in batch after `RunResponse` returns

## CaseResult Fields

| Field | Content |
|-------|---------|
| `nodeid` | `tests/test_foo.py::test_bar` |
| `outcome` | `passed`, `failed`, `skipped`, `error`, `xfailed`, `xpassed` |
| `phase` | `setup`, `call`, `teardown` |
| `duration_ms` | Execution time in milliseconds |
| `stdout` | Captured `print()` output from the test |
| `stderr` | Captured stderr |
| `message` | First line of error or xfail reason |
| `traceback` | Full traceback text on failure |

## Suite Leasing

Prevents multiple pytest sessions from fighting over the same host instance:

- `SuiteMutex` — Windows named Mutex per suite key
- `SuiteLeaseStore` — JSON file mapping suite → host PID/pipe
- Lease survives across test reruns, cleared on PID death

## Dialog Resolver

During auto-launch, hosts may show security dialogs for unsigned add-ins.
`StartupDialogResolver` runs a background thread that:
- Monitors host process windows
- Clicks safe actions: "Always Load", "Load Once"
- Avoids destructive: "Do Not Load", "Cancel", "No"

## Troubleshooting

### "Could not connect to host"

1. Verify host is running with RevitDevTool installed
2. Check pipe exists: `ls //./pipe/ | findstr Revit` (or `AutoCad`, etc.)
3. Try explicit pipe: `pytest --host-pipe=Revit_2025_<pid>`
4. Check `--host-version` matches running instance

### "Suite is already running in another pytest process"

Another pytest session holds the suite mutex. Kill the other process or wait.

### Tests timeout

Increase timeout: `pytest --host-timeout=120`

### PEP 723 packages not found

Ensure `conftest.py` has the `# /// script` block with correct dependency syntax.
RevitDevTool reads this at session start and installs packages before executing tests.

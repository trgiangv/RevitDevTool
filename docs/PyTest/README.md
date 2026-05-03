# PyTest Bridge Architecture

Architecture documentation for the pytest remote execution bridge between the `revitdevtool_pytest` plugin and RevitDevTool's Named Pipe server.

---

## Overview

```mermaid
flowchart TB
    subgraph Client["Client Side (pytest process)"]
        Plugin["revitdevtool_pytest plugin\n- plugin.py\n- bridge.py\n- connection.py\n- reporting.py\n- suite_lock.py"]
    end

    subgraph Pipe["Named Pipe (IPC)"]
        Proto["Frame protocol:\n[4-byte LE length][UTF-8 JSON]"]
    end

    subgraph Server["Server Side (inside Revit)"]
        PipeSrv["DevToolsPipeServer"]
        Handler["PytestRequestHandler"]
        ExecSvc["PytestExecutionService"]
        Runner["PytestRunner.py\n(embedded resource)"]
    end

    Client -->|"tests/discover\ntests/run"| Pipe
    Pipe --> PipeSrv
    PipeSrv --> Handler
    Handler -->|"Prepare deps"| ExecSvc
    Handler -->|"Execute in Python scope"| ExecSvc
    ExecSvc --> Runner
    Runner -->|"JSON result"| ExecSvc
    ExecSvc -->|"BridgeMessage"| Handler
    Handler -->|"JSON response"| PipeSrv
    PipeSrv -->|"response + notifications"| Pipe
    Pipe --> Client
```

---

## Client Side: `revitdevtool_pytest`

| File | Responsibility |
|------|---------------|
| **plugin.py** | pytest plugin entry point. Registers CLI + INI options (`--revit-version`, `--revit-launch`, `--revit-timeout`, `--revit-pipe`). Reads config from `[tool.pytest.ini_options]` in `pyproject.toml` (or `pytest.ini`/`tox.ini`/`setup.cfg`), with CLI overrides. Hooks into session lifecycle.
| **bridge.py** | `RevitBridge` — synchronous Named Pipe client. Connect/disconnect, frame-based RPC |
| **connection.py** | Bridge lifecycle: `ensure_bridge()` — connect, lease, auto-launch Revit if needed |
| **suite_lock.py** | Windows Mutex-based locking prevents multiple pytest processes on same Revit |
| **suite_leasing.py** | `SuiteLeaseStore` — exclusive test execution lease management |
| **reporting.py** | Maps remote `CaseResult[]` back to pytest `TestReport`. `run_remote_session` dispatches discover + run |
| **models.py** | Data models: `DiscoverRequest`, `RunRequest`, `CaseResult`, `BridgeResponse` |
| **dialog_resolver.py** | Handles Revit startup dialog detection and resolution |
| **constants.py** | Shared constants: pipe names, timeouts, phase/outcome enums |

## Server Side: `DevTools.Execution/External/Testing/`

| File | Responsibility |
|------|---------------|
| **PytestRequestHandler.cs** | Handles `tests/discover` and `tests/run` bridge messages |
| **PytestExecutionService.cs** | Core execution — serializes request, runs `PytestRunner.py` in Python scope, deserializes response |
| **PytestDependencyService.cs** | Ensures pytest + required packages installed before execution |
| **PytestPathResolver.cs** | Resolves test root and workspace root paths |
| **PytestContracts.cs** | Shared contracts: request/response types |

---

## Execution Flow

### Discovery

```mermaid
sequenceDiagram
    participant Plugin as pytest Plugin
    participant Bridge as RevitBridge
    participant Pipe as DevToolsPipeServer
    participant Handler as PytestRequestHandler
    participant Exec as PytestExecutionService
    participant Python as PytestRunner.py

    Plugin->>Bridge: discover_tests(workspace_root, test_root)
    Bridge->>Pipe: tests/discover (JSON)
    Pipe->>Handler: HandleDiscoverAsync()
    Handler->>Handler: Prepare dependencies
    Handler->>Exec: Discover(request)
    Exec->>Python: run("pytest --collect-only")
    Python-->>Exec: node IDs
    Exec-->>Handler: DiscoverResponse
    Handler-->>Pipe: JSON response
    Pipe-->>Bridge: response
    Bridge-->>Plugin: DiscoverResponse (node IDs)
```

### Run

```mermaid
sequenceDiagram
    participant Plugin as pytest Plugin
    participant Bridge as RevitBridge
    participant Pipe as DevToolsPipeServer
    participant Handler as PytestRequestHandler
    participant Exec as PytestExecutionService
    participant Python as PytestRunner.py
    participant Plugin_in as _BridgePlugin

    Plugin->>Bridge: run_tests(workspace_root, test_root, nodeids)
    Bridge->>Pipe: tests/run (JSON)
    Pipe->>Handler: HandleRunAsync()
    Handler->>Handler: Prepare dependencies
    Handler->>Exec: Run(request, progressCallback)
    Exec->>Python: set __pytest_request_json__, exec PytestRunner.py
    Python->>Python: pytest.main(args, plugins=[_BridgePlugin])
    loop Each test
        Plugin_in->>Python: pytest_runtest_logreport()
        Python-->>Exec: progress notification (JSON)
        Exec-->>Handler: SendNotification()
        Handler-->>Pipe: notification to client
    end
    Python-->>Exec: final JSON result
    Exec-->>Handler: RunResponse
    Handler-->>Pipe: JSON response
    Pipe-->>Bridge: response
    Bridge-->>Plugin: RunResponse (summary + results)
```

---

## Wire Protocol

Frame format: `[4-byte LE body length][UTF-8 JSON body]`

| Type | Structure |
|------|-----------|
| **Request** | `{"type":"request","id":"...","method":"...","params":{...}}` |
| **Response** | `{"type":"response","id":"...","result":{...}}` |
| **Error** | `{"type":"response","id":"...","error":{"message":"..."}}` |
| **Notification** | `{"type":"notification","method":"...","params":{...}}` |

---

## Related Documentation

- **[Execution Architecture](../Execution/README.md)** — Execution engine and Named Pipe server
- **RevitDevTool.PyTest** — [README](https://github.com/trgiangv/RevitDevTool.PyTest)

---

_Last updated: 2026-05-03_

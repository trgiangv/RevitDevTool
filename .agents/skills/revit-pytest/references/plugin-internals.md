# Plugin Internals

Collect locally, execute on the host, report locally. Pipe is `DevTools_{Host}_{Version}_{PID}` — not `DevToolsMcp_*`.

## Session

```mermaid
flowchart LR
    Collect[Local collect] --> Connect[ensure_bridge]
    Connect --> Split{items}
    Split -->|test_*.py| CPython["tests/run\nPytestRunner.py"]
    Split -->|test_*_ipy.py| IPy["ipytests/run\nIpyTestDriver.py"]
    CPython --> Report[Local logreport]
    IPy --> Report
```

`test_*_ipy.py` is pytest routing only. Host unittest does not care about that name.

| Local | Host |
|-------|------|
| `plugin.py` hooks | `DevToolsPipeServer` |
| `connection.py` + `discovery.py` | `PytestRequestHandler` / `IpyTestRequestHandler` |
| `bridge.py` RPC | `PytestRunner.py` (CPython) / `IpyTestDriver.py` (IPy) |
| `reporting.py` | PEP 723 only on `tests/run` |
| `suite_lock.py` + `suite_leasing.py` | same PID per host+version+workspace |

## Connect

First match wins. `--force-launch` skips reuse / explicit pipe / discover and always launches.

```mermaid
flowchart TD
    Start[ensure_bridge] --> Reuse{bridge already connected?}
    Reuse -->|yes| Done[use it]
    Reuse -->|no| Pipe{--host-pipe?}
    Pipe -->|yes| Explicit[connect that name]
    Pipe -->|no| Lease{lease PID still alive?}
    Lease -->|yes| Reconnect[reconnect leased pipe]
    Lease -->|no| Free{free DevTools_* instance?}
    Free -->|yes| Assign[connect + lease]
    Free -->|no| Launch[start host, wait for that PID pipe]
    Explicit --> Done
    Reconnect --> Done
    Assign --> Done
    Launch --> Done
```

`SuiteMutex` is the same key as the lease (one pytest process at a time). CPython and IronPython trees in one workspace reuse the PID; one invocation still cannot mix two `conftest.py` trees.

## Capture

Per-test stdout only. No session StringIO. `make_report` always attaches `Captured stdout` / `Captured stderr`.

```mermaid
flowchart LR
    subgraph CPython
        P1["print()"] --> Cap["--capture=sys"]
        Cap --> CR1[CaseResult.stdout]
    end
    subgraph IronPython
        P2["print"] --> Tee[per-test tee]
        Tee --> CR2[CaseResult.stdout]
    end
    CR1 --> Wire[pipe CaseResult]
    CR2 --> Wire
    Wire --> Report["make_report sections"]
```

CLI streams `notifications/tests/progress` into live `logreport`. IDE (`vscode_pytest` or `TEST_RUN_PIPE`) waits for the batch `RunResponse`. IPy is always batch.

## Wire

Frame: `[4-byte LE length][UTF-8 JSON]`. Same request shape for both run methods.

| Method | Host | Notes |
|--------|------|-------|
| `tests/run` | `PytestRunner.py` `pytest.main` | PEP 723 prepare first |
| `ipytests/run` | `IpyTestDriver.py` unittest | no pixi; response may set `engine` |

`CaseResult`: `nodeid`, `outcome` (`passed`/`failed`/`skipped`/`error`/`xfailed`/`xpassed`), `phase`, `duration_ms`, `stdout`, `stderr`, `message`, `traceback`.

## Dialogs

On launch, `StartupDialogResolver` clicks "Always Load" / "Load Once". It does not click "Do Not Load", "Cancel", or "No".

## Troubleshooting

**Could not connect** — host running with add-in; `ls //./pipe/` matches `DevTools_Revit_…`; `--host-pipe=DevTools_Revit_2025_<pid>`; `--host-version` matches the instance.

**Suite already running** — another pytest holds the mutex. Kill it or wait.

**Timeout** — `uv run pytest --per-test-timeout=120`

**PEP 723 packages missing** — `# /// script` on CPython `conftest.py` / `test_*.py`, not on `test_*_ipy.py`.

# Pytest Bridge: Independent In-Host Test Execution

The pytest bridge is a direct compatibility protocol, separate from MCP and DevTools.Daemon.

```text
pytest CLI -> RevitDevTool.PyTest local collection
  -> DevTools_{Host}_{Version}_{PID}
  -> four-byte little-endian frame + UTF-8 BridgeMessage
  -> tests/run
  -> notifications/tests/progress
  -> PytestRunResponse final response
```

The plugin collects node IDs locally, then asks a live host to execute the selected IDs. It does not initialize MCP, use an MCP progress notification, or traverse the daemon.

## Direct wire contract

| Item | Current contract |
|---|---|
| Endpoint | `DevTools_{Host}_{Version}_{PID}` |
| Envelope | UTF-8 `BridgeMessage` in a four-byte little-endian length frame |
| Request | `tests/run` with `workspace_root`, `test_root`, `nodeids`, and `pytest_args` |
| Progress | `notifications/tests/progress` carries case-result data before the final response |
| Final result | `PytestRunResponse` with `exit_code`, `summary`, `results`, `collection_errors`, and `rootdir` |

`PytestRequestHandler` exposes `tests/run` only. Earlier `tests/discover` documentation is stale: collection is local to the plugin, while selected node IDs are remotely executed through `tests/run`.

Before in-host execution, `PytestDependencyService` prepares PEP 723 dependencies. The handler then executes `PytestExecutionService` inside `IHostContextExecutor` under `ExecutionGuardMode.Suppress`; embedded `PytestRunner.py` runs pytest inside the host Python runtime and returns the final typed response.

## Compatibility and verification

`DevTools.Ipc` intentionally retains the direct pytest envelope, framing, pipe helpers, and property names required by this lane. This is not an MCP compatibility adapter. Live verification requires an already running/launchable host and its Python environment; protocol or ordering changes require coordinated testing with the separate `RevitDevTool.PyTest` repository.

# Pytest host execution

`RevitDevTool.PyTest` is a pytest plugin: it owns local collection, pytest
reporting, host discovery/launch, suite leasing, and direct Python client
behavior. For selected tests requiring host APIs, it opens a standard MCP
session directly to the host and invokes the reserved `pytest_run` tool. It
does not traverse `DevTools.Daemon` or McpGateway.

```text
pytest CLI -> local collection -> RevitDevTool.PyTest
  -> direct named-pipe MCP session -> DevTools_{Host}_{Version}_{PID}
  -> pytest_run -> PytestRunner.py in the host -> pytest reports
```

## Contract

The canonical pipe grammar is exactly `DevTools_{Host}_{Version}_{PID}`. Host
and version are nonblank and contain no underscore; PID is positive. The
plugin parses the pipe, initializes MCP, then cross-checks server metadata so a
matching name cannot silently select the wrong host. Multiple daemon and
pytest sessions may use the same pipe concurrently.

`pytest_run` receives `workspace_root`, `test_root`, selected `nodeids`, and
`pytest_args`. Its final `PytestRunResponse` returns `exit_code`, `summary`,
per-case results, collection errors, and `rootdir`. Test failures, skips, and
collection outcomes are domain data in that result and become ordinary pytest
reports. Infrastructure failures such as dependency preparation, missing host
context, runner failure, serialization failure, or host shutdown are MCP tool
errors with stable `pytest_*` codes.

The tool sends standard MCP progress for the request token. Optional per-case
notifications use `notifications/devtools/pytest/case` when the client
advertises `experimental.devtools.pytest.caseEvents.version = "1"`; the final
result works without this capability. Cancellation is sent through MCP. On a
client deadline or Ctrl+C, the plugin can close only its own session after a
short grace period; it never kills the CAD/BIM host.

## Ownership and source map

| Concern | Owner |
|---|---|
| Local collection, discovery, lease, connection, reporting | `RevitDevTool.PyTest` |
| Named-pipe identity | `DevTools.Ipc/HostPipeName.cs` and plugin `pipe_name.py` |
| Host MCP accept loop and built-in tool | `DevTools.Execution/External/Mcp/` |
| Dependency preparation and in-host pytest execution | `PytestDependencyService`, `PytestExecutionService`, `PytestRunner.py` |

Old four-byte framed bridge requests, `BridgeMessage`, `tests/run`, and
`notifications/tests/progress` are intentionally unsupported. Historical
plans mentioning them are superseded by ADR 002.

## Verification limits

Unit tests prove contract parsing, connection/session behavior, and reporting.
Live proof additionally needs a compatible running or launchable host and its
embedded Python environment. Preserve that distinction when reporting results.

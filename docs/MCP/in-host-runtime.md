# In-host MCP runtime

Every supported host exposes one standard Model Context Protocol server on its
canonical named pipe:

```text
DevTools_{HostApp}_{HostVersion}_{PID}
```

`HostMcpServerHostedService` owns the accept loop and creates an independent
SDK `McpServer` for each connection. A disconnect disposes only that MCP
session; it does not disrupt the daemon or pytest clients connected to the
same host. The pipe carries MCP exclusively. Pre-release framed bridge clients
are intentionally unsupported.

```text
DevTools.Daemon or RevitDevTool.PyTest
  -> standard named-pipe MCP client session
  -> DevTools_{HostApp}_{HostVersion}_{PID}
  -> independent host McpServer session
  -> SDK primitive / IHostContextExecutor when required
```

## Pipe identity

`.NET` `HostPipeName` is the canonical formatter and parser. A valid name has
exactly four underscore-separated segments: literal `DevTools`, a nonblank host
segment, a nonblank version segment, and a positive invariant-culture PID. Host
and version cannot contain `_`; whitespace-only segments and zero/negative PIDs
are invalid. The Python plugin's `pipe_name.py` independently mirrors this
strict grammar and has parity tests for accepted and rejected names. The daemon
and Python plugin parse the name before connection and cross-check initialized
server metadata against the parsed host and version. They reject a mismatch
rather than connect to an ambiguously named server.

## Catalog and host execution

`HostMcpServerOptionsFactory` publishes SDK tools, prompts, resources, and
resource templates. Built-in names are reserved; duplicate dynamic names or
resource URIs are rejected with diagnostics instead of shadowing a primitive.
Registry paths remain persisted and invalid paths are pruned by the catalog
flow.

Host adapters own API threading, transactions, document context, and rendering.
Shared MCP dispatch uses `IHostContextExecutor` only when a primitive needs a
host API context. This keeps Revit and AutoCAD dependencies out of the shared
runtime.

## Pytest built-in

`pytest_run` is a reserved in-host MCP tool, not a daemon broker alias. Its
request carries locally collected `workspace_root`, `test_root`, `nodeids`, and
`pytest_args`; its final response is `PytestRunResponse` with `exit_code`,
`summary`, `results`, `collection_errors`, and `rootdir`. Domain test failures
are returned in that response. Dependency preparation, host-context,
serialization, runner, and host-shutdown failures use the documented
infrastructure error codes instead.

The tool emits normal MCP progress for a request progress token. It emits
`notifications/devtools/pytest/case` only when initialize advertises
`experimental.devtools.pytest.caseEvents.version = "1"`; final results do not
depend on that capability. Cancellation is forwarded to host execution, but a
client timeout or Ctrl+C may close only its own MCP session after a short grace
period. It never terminates the host process.

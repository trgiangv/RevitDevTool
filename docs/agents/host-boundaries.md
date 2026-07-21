# Host boundaries

## Ownership

- `source/DevTools.Execution/` owns shared execution runtime (providers, orchestrator, script engines).
- `source/DevTools.Execution.Pytest/` owns host-side pytest runner, `pytest_run` tool, and embedded `PytestRunner.py`.
- `source/DevTools.Execution.Abstractions/` owns host-neutral execution
  contracts such as `IHostContextExecutor` and `TextHighlightRange`.
- `source/DevTools.Ipc/` owns current-user named-pipe security and the
  canonical `HostPipeName` contract; it owns no MCP DTOs or legacy framed lane.
- `source/DevTools.Mcp/` owns host-neutral catalog and Broker routing.
- `source/DevTools.Mcp.Hosting/` owns the in-host named-pipe MCP server accept loop
  (`HostMcpServerHostedService`) — not Execution.
- `source/DevTools.Daemon/` owns the external MCP server, host sessions,
  gateway lifecycle, and product-neutral `IHostDriver` routing.
- Host projects own Revit/AutoCAD APIs, API-thread implementations,
  transactions, and rendering.
- WPF UI concerns (`DevTools.UI`, Presentation) must not be referenced by Execution.

## DI composition

Hosts call, in order:

1. `AddExecutionCore` — providers/orchestrator/script runtimes
2. `AddInHostMcpServer` — catalog + built-in execute tools + pipe server
3. `AddPytestHostRunner` — pytest MCP (from `DevTools.Execution.Pytest`)

`AddExecutionServices()` remains a shim for (1)+(2) only.

## Runtime boundaries

- One host data pipe, `DevTools_{Host}_{Version}_{PID}`, carries standard MCP
  sessions for both daemon catalog/tool access and direct pytest `pytest_run`.
- `RevitDevTool.PyTest` owns local collection and reporting; its direct MCP
  session never traverses the daemon or gateway.
- `DevToolsDaemon_Control` is a separate control-only pipe.
- Gateway `machine_id` binds an HTTP MCP session at initialize time; Broker
  `hostId` selects a host PID only within that daemon.

## Wave 2 decorator seams (in-host)

- Primary: `McpHostExecutionPrimitives` in `DevTools.Mcp`
- Built-in guard: `BuiltInToolExecution` in `DevTools.Mcp`
- Pipe accept: `HostMcpServerHostedService` in `DevTools.Mcp.Hosting`

## Review checklist

- Keep host API threading, transactions, document context, and rendering in
  host projects.
- Keep shared libraries compatible with Autodesk 2022–2024 (`net48`) as well
  as current `net8.0-windows` and `net10.0-windows` paths.
- Use a focused host build and state any unavailable live-host/threading or
  packaging evidence precisely.

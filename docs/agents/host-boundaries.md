# Host boundaries

## Ownership

- `source/DevTools.Execution/` owns shared execution, the host MCP server, and
  pytest host execution services.
- `source/DevTools.Execution.Abstractions/` owns host-neutral execution
  contracts such as `IHostContextExecutor`.
- `source/DevTools.Ipc/` owns current-user named-pipe security and the
  canonical `HostPipeName` contract; it owns no MCP DTOs or legacy framed lane.
- `source/DevTools.Mcp/` owns host-neutral catalog and Broker routing.
- `source/DevTools.Daemon/` owns the external MCP server, host sessions,
  gateway lifecycle, and product-neutral `IHostDriver` routing.
- Host projects own Revit/AutoCAD APIs, API-thread implementations,
  transactions, and rendering.

## Runtime boundaries

- One host data pipe, `DevTools_{Host}_{Version}_{PID}`, carries standard MCP
  sessions for both daemon catalog/tool access and direct pytest `pytest_run`.
- `RevitDevTool.PyTest` owns local collection and reporting; its direct MCP
  session never traverses the daemon or gateway.
- `DevToolsDaemon_Control` is a separate control-only pipe.
- Gateway `machine_id` binds an HTTP MCP session at initialize time; Broker
  `hostId` selects a host PID only within that daemon.

## Review checklist

- Keep host API threading, transactions, document context, and rendering in
  host projects.
- Keep shared libraries compatible with Autodesk 2022–2024 (`net48`) as well
  as current `net8.0-windows` and `net10.0-windows` paths.
- Use a focused host build and state any unavailable live-host/threading or
  packaging evidence precisely.

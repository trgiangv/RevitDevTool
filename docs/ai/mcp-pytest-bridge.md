# MCP And PyTest Bridge Digest

Deep sources: `docs/MCP/README.md` and `docs/PyTest/README.md`.

## MCP

- IPC transport layer: `source/DevTools.Ipc/`
- MCP protocol library: `source/DevTools.Mcp/`
- Execution abstractions: `source/DevTools.Execution.Abstractions/`
- Standalone daemon: `source/DevTools.Daemon/`
- In-host runtime: `source/DevTools.Execution/External/`
- MCP catalog store: `DevTools.Mcp.McpCatalogStore`
- MCP bridge handler: `DevTools.Mcp.Handlers.McpBridgeRequestHandler`
- MCP primitive dispatcher: `DevTools.Mcp.Dispatch.IMcpPrimitiveDispatcher` (interface in Mcp, impl in Execution)
- MCP routing (daemon): `DevTools.Mcp.Routing.CatalogService`
- Bridge client: `DevTools.Daemon.Mcp.HostBridgeClient` (implements `IHostBridgeClient`)

External MCP clients talk to `DevTools.Daemon`, which discovers any host pipe (`Revit_*`, `AutoCad_*`, `Civil3D_*`, etc.) via `InstanceManager`. Daemon built-in tools: `list_host_instances`, `launch_host`, `read_file_info`, `open_model`, `list_machines` — multi-host (Revit + AutoCAD-family). In-host built-in tools registered in `ExecutionExtensions.cs`: `execute_csharp_code`, `open_document`. The in-host registry/dispatch runtime is fully shared. The bridge handler lives in `DevTools.Mcp` (protocol layer) and depends on `IMcpPrimitiveDispatcher` for actual execution dispatch.

## PyTest Bridge

- Pytest contracts: `source/DevTools.Pytest/Contracts/PytestContracts.cs`
- Pytest bridge methods: `source/DevTools.Pytest/PytestBridgeMethods.cs`
- Server side execution: `source/DevTools.Execution/External/Testing/`
- Embedded runner: `source/DevTools.Execution/Resources/scripts/PytestRunner.py`
- Protocol routes include `tests/discover` and `tests/run`.
- The client pytest process talks to the host through a framed named pipe.

## Change Checklist

- For MCP parser changes, verify parser library tests and at least one sample catalog path.
- For runtime registry/dispatch changes, verify both standalone server assumptions and in-host pipe flow.
- For pytest bridge changes, verify discovery and run paths separately when possible.
- If a live host is required and unavailable, report the named pipe/host blocker precisely.

# MCP And PyTest Bridge Digest

Deep sources: `docs/architecture/MCP/README.md` and `docs/architecture/PyTest/README.md`.

## MCP

- IPC transport layer: `source/DevTools.Ipc/`
- MCP protocol library: `source/DevTools.Mcp/`
- Execution abstractions: `source/DevTools.Execution.Abstractions/`
- Standalone daemon: `source/DevTools.Daemon/`
- In-host runtime: `source/DevTools.Execution/External/`
- MCP catalog store: `DevTools.Mcp.McpCatalogStore`
- MCP bridge handler: `DevTools.Mcp.Handlers.McpBridgeRequestHandler`
- MCP primitive dispatcher: `DevTools.Mcp.Dispatch.IMcpPrimitiveDispatcher` (interface in Mcp, impl in Execution)
- MCP routing (daemon): `DevTools.Mcp.Routing.Catalog.CatalogService`
- Bridge client: `DevTools.Daemon.Mcp.HostBridgeClient` (implements `IHostBridgeClient`)

External MCP clients talk to `DevTools.Daemon` (via `--stdio` or Gateway), which discovers any host pipe (`DevTools_Revit_*`, `DevTools_AutoCad_*`, `DevTools_Civil3D_*`, etc.) via `InstanceManager`. Daemon tools include infrastructure (`list_host_instances`, `launch_host`, `read_file_info`, `open_model`, `list_machines`) and symmetric catalog tools (`list_dynamic_tools`, `call_dynamic_tool`, `list_dynamic_resources`, `read_dynamic_resource`, `list_dynamic_prompts`, `get_dynamic_prompt`, `refresh_dynamic_catalog`). In-host built-in primitives: tools (`execute_csharp_code`, `execute_python_code`, `open_document`, `navigate_history`), resources (`revit://csharp-cheatsheet`, `revit://python-cheatsheet`, `revit://model/context`, `revit://model/warnings`, `revit://version`, `revit://view/screenshot`), prompts (`revit_code`). See [`docs/architecture/MCP/tools.md`](../architecture/MCP/tools.md) for full catalog. The bridge handler lives in `DevTools.Mcp` (protocol layer) and depends on `IMcpPrimitiveDispatcher` for actual execution dispatch.

## PyTest Bridge

- Pytest contracts: `source/DevTools.Execution/External/Testing/PytestContracts.cs`
- Pytest bridge methods: `source/DevTools.Execution/External/Testing/PytestBridgeMethods.cs`
- Server side execution: `source/DevTools.Execution/External/Testing/`
- Embedded runner: `source/DevTools.Execution/Resources/scripts/PytestRunner.py`
- Protocol route: `tests/run` (with optional `discover_only` flag in the request payload for collection-only mode).
- The client pytest process talks to the host through a framed named pipe.

## Change Checklist

- For MCP parser changes, verify parser library tests and at least one sample catalog path.
- For runtime registry/dispatch changes, verify both standalone server assumptions and in-host pipe flow.
- For pytest bridge changes, verify discovery and run paths separately when possible.
- If a live host is required and unavailable, report the named pipe/host blocker precisely.

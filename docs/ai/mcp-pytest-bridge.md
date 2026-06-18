# MCP And PyTest Bridge Digest

Deep sources: `docs/MCP/README.md` and `docs/PyTest/README.md`.

## MCP

- Parser library: `source/DevTools.McpParser/`.
- Standalone daemon: `source/DevTools.Daemon/`.
- In-host runtime: `source/DevTools.Execution/External/Mcp/`.
- Registry store: `ToolRegistryStore`.
- Providers: `DotnetToolRegistryProvider` and `PythonToolRegistryProvider`.
- Dispatchers: tool, prompt, and resource dispatch.
- Bridge client: `HostBridgeClient` (generic, formerly `RevitBridgeClient`).

External MCP clients talk to `DevTools.Daemon`, which discovers any host pipe (`Revit_*`, `AutoCad_*`, `Civil3D_*`, etc.) via `InstanceManager`. Daemon built-in tools: `list_host_instances`, `launch_host`, `read_file_info`, `open_model`, `list_machines` — multi-host (Revit + AutoCAD-family). In-host built-in tools registered in `ExecutionExtensions.cs`: `execute_csharp_code`, `open_document`. The in-host registry/dispatch runtime is fully shared.

## PyTest Bridge

- Server side: `source/DevTools.Execution/External/Testing/`.
- Embedded runner: `source/DevTools.Execution/Resources/scripts/PytestRunner.py`.
- Protocol routes include `tests/discover` and `tests/run`.
- The client pytest process talks to the host through a framed named pipe.

## Change Checklist

- For MCP parser changes, verify parser library tests and at least one sample catalog path.
- For runtime registry/dispatch changes, verify both standalone server assumptions and in-host pipe flow.
- For pytest bridge changes, verify discovery and run paths separately when possible.
- If a live host is required and unavailable, report the named pipe/host blocker precisely.

# MCP And PyTest Bridge Digest

Deep sources: `docs/architecture/MCP/README.md` and `docs/architecture/PyTest/README.md`.

## MCP

- IPC transport layer: `source/DevTools.Ipc/`
- MCP platform: `source/DevTools.Mcp.Core/`, `Catalog/`, `Adapter/`, `Client/`, `Server/`
- Execution abstractions: `source/DevTools.Execution.Abstractions/`
- Standalone daemon: `source/DevTools.Daemon/`
- In-host runtime: `source/DevTools.Execution/External/`
- Host spec pipe: `HostMcpPipeServer` + `McpHandler` on `DevToolsMcp_{Host}_{Version}_{PID}`
- Pytest/control pipe: `DevToolsPipeServer` on `DevTools_{Host}_{Version}_{PID}`
- Connected catalog index: `DevTools.Mcp.Client.ConnectedHostCatalog` (owned by `HostBroker`)
- In-host registry: `DevTools.Mcp.Catalog.McpCatalogStore`
- Primitive dispatcher: `DevTools.Mcp.Core.Invocation.IMcpPrimitiveDispatcher` (impl in Execution)

External MCP clients talk to `DevTools.Daemon` (via `--stdio` or Gateway). The daemon
exposes infrastructure tools plus exactly `search_dynamic` and `invoke_dynamic`.
Fixed prompts (`revit_code`, `acad_code`) are daemon-owned. Host capabilities stay
in `HostCatalog` and are refreshed on SDK list-changed notifications for that host
only; external daemon collections do not advertise `ListChanged`.

In-host built-ins: tools (`execute_csharp_code`, `execute_python_code`,
`open_document`, `navigate_history`) and resources (cheatsheets, model context,
warnings, version, screenshots). See [`docs/architecture/MCP/tools.md`](../architecture/MCP/tools.md).

## PyTest Bridge

- Pytest contracts: `source/DevTools.Execution/External/Testing/PytestContracts.cs`
- Pytest bridge methods: `source/DevTools.Execution/External/Testing/PytestBridgeMethods.cs`
- Server side execution: `source/DevTools.Execution/External/Testing/`
- Embedded runner: `source/DevTools.Execution/Resources/scripts/PytestRunner.py`
- Protocol route: `tests/run` (optional `discover_only`) over length-prefixed `BridgeMessage` frames
- The client pytest process talks to the host through the pytest Named Pipe only

## Change Checklist

- For MCP parser changes, verify parser/contract tests and at least one sample catalog path.
- For host SDK / broker changes, verify `tests/DevTools.Mcp.Tests` (catalog, stream, named-pipe, framing).
- For pytest bridge changes, verify discovery and run paths separately when possible.
- Never mix SDK NDJSON with pytest `BridgeMessage` framing on the same pipe.
- If a live host is required and unavailable, report the named pipe/host blocker precisely.

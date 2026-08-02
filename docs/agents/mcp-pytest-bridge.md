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
in `ConnectedHostCatalog` and are refreshed on host list-changed notifications for that host
only; external daemon collections do not advertise `ListChanged`.

In-host built-ins: tools (`execute_csharp_code`, `execute_python_code`,
`open_document`, `navigate_history`) and resources (cheatsheets, model context,
warnings, version, screenshots). See [`docs/architecture/MCP/tools.md`](../architecture/MCP/tools.md).

## PyTest bridge (host side, this repo)

- Contracts: `source/DevTools.Execution/External/Testing/PytestContracts.cs`
- Methods: `PytestBridgeMethods.cs`
- Server: `PytestExecutionService`, `DevToolsPipeServer` on `DevTools_{Host}_{Version}_{PID}`
- Runner: `source/DevTools.Execution/Resources/scripts/PytestRunner.py`
- Wire: `tests/run` over length-prefixed `BridgeMessage` — **not** the MCP `DevToolsMcp_*` pipe

## Change checklist

- MCP parser: `scripts/test-dotnet.ps1 -Project tests/DevTools.Mcp.Tests/...` + build `samples/McpToolsetDemo` if needed.
- Host wire / broker: `DevTools.Mcp.Tests` (catalog, stream, named-pipe, framing).
- Pytest bridge: sync `PytestContracts.cs` ↔ `models.py` when wire shape changes; run in-repo tests if present.
- Never mix MCP NDJSON with `BridgeMessage` on the same pipe.
- See `known-test-gaps.md` for tests that fail without pixi, built samples, or live host.

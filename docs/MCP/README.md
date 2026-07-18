# MCP Runtime V2 Architecture

External MCP clients use a host-agnostic, standard Model Context Protocol (MCP) runtime. The standalone **DevTools.Daemon** owns the external server and connects to each host through a separate standard-MCP named-pipe session.

```text
External MCP client
  -> DevTools.Daemon MCP server
  -> devtools_search/devtools_invoke cached broker
  -> DevTools.Daemon McpClient
  -> standard MCP over DevTools.Mcp.v2.{pid} named pipe
  -> host McpServer
  -> SDK primitive
  -> IHostContextExecutor when host API context is required
```

The protocol server/client, initialization, errors, progress, list operations, and list-changed notifications use the ModelContextProtocol SDK. `DevTools.Ipc` deliberately does not own MCP DTOs; it retains the independent direct pytest envelope and framing compatibility lane.

## External surface

The default `Broker` surface is stable when hosts connect, disconnect, or reload catalogs. It has exactly six tools:

| Tool | Purpose |
|---|---|
| `devtools_search` | Search the daemon's cached host catalog for tools, resources, and prompts. |
| `devtools_invoke` | Invoke a `tool:`, `resource:`, or `prompt:` target returned by search. |
| `launch_host` | Start a supported host through its daemon host driver. |
| `open_model` | Open a model using extension-based host selection. |
| `read_file_info` | Read supported offline model metadata. |
| `list_machines` | Query authenticated gateway machines after a daemon has been selected. |

Search does not contact a host; invocation makes one host MCP request. A known, uniquely resolved target can be invoked in one external call. When a target exists on multiple local hosts, `devtools_invoke` returns the candidate PIDs and requires `hostId`.

`Native` is an opt-in surface mode. It keeps the six daemon tools and also exposes host primitives as namespaced SDK proxies. It is for clients that honor MCP list-changed notifications. The default `Broker` mode intentionally does **not** flatten runtime-changing primitives, so clients that load a tool list once keep a valid surface.

## Boundaries and identity

- Built-in primitive names are reserved. Dynamic duplicate names or resource URIs are rejected and surfaced as catalog diagnostics; they do not silently shadow an existing primitive.
- .NET and Python registry paths remain persisted, and invalid paths are pruned by the host catalog flow.
- New daemon product support is an `IHostDriver` implementation. Drivers own host-product membership, file extensions, launching, and offline metadata behavior; MCP broker tools stay product-neutral.
- Revit/AutoCAD API calls remain in host adapters. SDK primitives use `IHostContextExecutor` only where a host API context is required.

## Gateway routing is outside the broker

```text
Authenticated client -> GET /machines -> choose machine_id
  -> x-target-machine on MCP initialize and every later HTTP request
  -> selected DevTools.Daemon
  -> broker hostId selects one local host PID
```

`machineId` is a gateway identity selected by the client before MCP initialization. `hostId` is an integer PID scoped to the already selected daemon and never selects a gateway machine. With multiple gateway machines, an unpinned `/mcp` request is rejected before it can reach `list_machines`; therefore `list_machines` is post-selection convenience, not a bootstrap mechanism. Gateway tunnel register/heartbeat and the local daemon control pipe are separate retained contracts, not part of the removed MCP bridge.

## Pytest is a separate supported lane

Pytest does not initialize MCP or traverse `DevTools.Daemon`. It is independently process-triggered:

```text
pytest CLI -> RevitDevTool.PyTest local collection
  -> DevTools_{Host}_{Version}_{PID}
  -> four-byte framed BridgeMessage tests/run request
  -> notifications/tests/progress
  -> PytestRunResponse final response
```

See [PyTest](../PyTest/README.md) for its direct compatibility contract.

## Documentation map

| Document | Contents |
|---|---|
| [Daemon](daemon.md) | Daemon ownership, lifecycle, and host sessions. |
| [Transport](transport.md) | Stdio, gateway routing, and machine selection. |
| [Tools](tools.md) | Broker, Native, targets, and host selection. |
| [In-host runtime](in-host-runtime.md) | SDK server, registry, identity, and host execution. |
| [Workflows](workflows.md) | Practical agent workflows. |

## Source map

| Area | Path |
|---|---|
| Daemon MCP server/session manager | `source/DevTools.Daemon/Mcp/` |
| Daemon host drivers | `source/DevTools.Daemon/Hosts/` |
| Shared MCP routing/catalog | `source/DevTools.Mcp/Routing/` |
| Host MCP server | `source/DevTools.Execution/External/Mcp/` |
| Direct pytest bridge | `source/DevTools.Execution/External/Testing/` |
| Direct pytest pipe/envelope | `source/DevTools.Ipc/` |

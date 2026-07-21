# MCP Runtime V2 Architecture

External MCP clients use a host-agnostic, standard Model Context Protocol (MCP) runtime. The standalone **DevTools.Daemon** owns the external server and connects to each host through a separate standard-MCP named-pipe session.

```text
External MCP client
  -> DevTools.Daemon MCP server
  -> devtools_search/devtools_invoke cached broker
  -> DevTools.Daemon McpClient
  -> standard MCP over DevTools_{Host}_{Version}_{PID} named pipe
  -> host McpServer
  -> SDK primitive
  -> IHostContextExecutor when host API context is required
```

The protocol server/client, initialization, errors, progress, list operations, and list-changed notifications use the ModelContextProtocol SDK. `DevTools.Ipc` owns the canonical host-pipe name helper; it does not own MCP DTOs or a second pytest frame protocol.

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

## Broker readiness and failure contract

`devtools_search` returns a cached, immutable publication. It never waits for or fetches a host catalog. Its `catalogs` array reports every known host publication as one of these states:

| State | Meaning |
|---|---|
| `refreshing` | A connected generation has no completed first fetch yet. |
| `ready` | The current generation has a successfully fetched catalog. |
| `stale` | The current generation retains its last successful catalog after a refresh failure. |
| `unavailable` | No usable catalog exists for the current generation. |

The daemon identifies a publication by both canonical pipe name and session generation. A reconnect, including PID reuse, never dispatches a snapshot from the old generation. The session manager maintains a PID index for O(1) `hostId` lookup; catalog replacement deliberately rebuilds and swaps the full immutable snapshot. That is preferred over incremental mutation while the expected host count is below 20.

`devtools_invoke` accepts `timeoutSeconds` from **1** through **900**, default **300**. Its structured error payload has exactly these statuses: `host_selection_required`, `host_mismatch`, `target_not_found`, `host_disconnected`, `connection_lost`, `timed_out`, and `host_failed`. `mayHaveExecuted` is false for selection/mismatch/not-found/disconnected results and true once a request could have reached the selected host. The broker never performs an automatic retry after dispatch: retrying an operation with an unknown execution outcome is unsafe.

`launch_host` first waits for the host session connection, then waits at most 10 seconds for that exact generation's first catalog result. `ready` and `stale` yield `connected_catalog_ready`; `refreshing` or `unavailable` yield `connected_catalog_pending` and callers should search again. This readiness barrier overlaps the startup-dialog wait; it is not a guarantee that a cold host will have a usable catalog within ten seconds.

## Boundaries and identity

- Built-in primitive names are reserved. Dynamic duplicate names or resource URIs are rejected and surfaced as catalog diagnostics; they do not silently shadow an existing primitive.
- .NET and Python registry paths remain persisted, and invalid paths are pruned by the host catalog flow.
- New daemon product support is an `IHostDriver` implementation. Drivers own host-product membership, file extensions, launching, and offline metadata behavior; MCP broker tools stay product-neutral.
- Revit/AutoCAD API calls remain in host adapters. SDK primitives use `IHostContextExecutor` only where a host API context is required.

## Gateway routing is outside the broker

```text
Authenticated client -> GET /machines -> choose machine_id
  -> x-target-machine on MCP initialize
  -> retain mcp-session-id for later HTTP requests
  -> selected DevTools.Daemon
  -> broker hostId selects one local host PID
```

`machineId` is a gateway identity selected only while initializing a new HTTP
session. The resulting `mcp-session-id` is bound to that daemon and generation;
later requests use the session header, not repeated machine selection. `hostId`
is an integer PID scoped to the already selected daemon and never selects a
gateway machine. With multiple gateway machines, an unpinned `/mcp` initialize
is rejected before it can reach `list_machines`; therefore `list_machines` is
post-selection convenience, not a bootstrap mechanism. Gateway tunnel
register/heartbeat and the local daemon control pipe are separate retained
contracts, not part of the removed MCP bridge.

## Pytest is a separate supported workflow

Pytest collection remains local to the `RevitDevTool.PyTest` plugin. For tests that require the running host, the plugin opens a direct standard-MCP session to the canonical host pipe and calls the reserved `pytest_run` tool. It does not traverse `DevTools.Daemon` or the gateway:

```text
pytest CLI -> RevitDevTool.PyTest local collection
  -> DevTools_{Host}_{Version}_{PID}
  -> standard MCP initialize + pytest_run
  -> optional notifications/devtools/pytest/case
  -> PytestRunResponse final tool result
```

Case events require the client capability `experimental.devtools.pytest.caseEvents.version = "1"`; final results work without it. See [PyTest](../PyTest/README.md) for the plugin workflow.

## Documentation map

| Document | Contents |
|---|---|
| [Daemon](daemon.md) | Daemon ownership, lifecycle, and host sessions. |
| [Transport](transport.md) | Stdio, gateway routing, and machine selection. |
| [Tools](tools.md) | Broker, Native, targets, and host selection. |
| [Inspector](inspector.md) | How to attach MCP Inspector (daemon/gateway; no stdio-pipe shim). |
| [In-host runtime](in-host-runtime.md) | SDK server, registry, identity, and host execution. |
| [Workflows](workflows.md) | Practical agent workflows. |

## Source map

| Area | Path |
|---|---|
| Daemon MCP server/session manager | `source/DevTools.Daemon/Mcp/` |
| Daemon host drivers | `source/DevTools.Daemon/Hosts/` |
| Shared MCP routing/catalog | `source/DevTools.Mcp/Routing/` |
| Host MCP server | `source/DevTools.Execution/External/Mcp/` |
| Host pytest MCP tool | `source/DevTools.Execution/External/Mcp/BuiltIn/` |
| Canonical host pipe helper | `source/DevTools.Ipc/HostPipeName.cs` |

# MCP and Pytest Bridge Digest

Deep sources: `docs/MCP/README.md` and `docs/PyTest/README.md`.

## MCP Runtime V2

- Daemon external server/session owner: `source/DevTools.Daemon/Mcp/`.
- Host server: `source/DevTools.Execution/External/Mcp/Hosting/HostMcpServerHostedService.cs`.
- Shared catalog/routing: `source/DevTools.Mcp/Routing/`.
- Daemon-to-host endpoint: standard MCP over canonical `DevTools_{Host}_{Version}_{PID}`.
- Daemon session: `HostMcpSession` wraps SDK `McpClient`; the host owns SDK `McpServer` sessions.
- Default surface is fixed Broker mode: `devtools_search`, `devtools_invoke`, `launch_host`, `open_model`, `read_file_info`, `list_machines`.
- Search uses cached host snapshots; invoke makes one host request. `hostId` is a daemon-local host PID used only for disambiguation.
- Native mode is opt-in, keeps daemon tools, and projects namespaced host SDK primitives. It needs clients that honor list-changed notifications.
- `devtools_search` is cache-only. Catalog publications are immutable and generation-scoped (`refreshing`, `ready`, `stale`, `unavailable`); stale publications retain the last successful snapshot.
- `devtools_invoke.timeoutSeconds` is 1--900 seconds (default 300). Its only structured statuses are `host_selection_required`, `host_mismatch`, `target_not_found`, `host_disconnected`, `connection_lost`, `timed_out`, and `host_failed`. Do not add automatic dispatch retries.
- `HostSessionManager` has O(1) PID lookup. Full immutable catalog rebuilds are intentional for the fewer-than-20-host operating envelope.
- `launch_host` waits up to 10 seconds for the first catalog publication for the exact connected generation. `ready`/`stale` map to `connected_catalog_ready`; other states map to `connected_catalog_pending`.
- Built-in primitive names are reserved; dynamic duplicate names/URIs are rejected with diagnostics. .NET/Python configured path persistence and invalid-path pruning are preserved.
- Add a daemon host product through `IHostDriver`, not host-specific broker branches.

Gateway selection is an initialize-time transport decision: call authenticated
`GET /machines`, choose a `machine_id`, then send `x-target-machine` on
initialize. The returned `mcp-session-id` retains the binding for later HTTP
requests. `list_machines` is post-selection only; it cannot bootstrap an
unpinned connection when multiple gateway machines are online. Do not overload
`hostId` with `machine_id`.

## Direct host pytest workflow

- Client: separate `RevitDevTool.PyTest` pytest plugin; collection is local.
- Endpoint: `DevTools_{Host}_{Version}_{PID}`.
- Transport: a direct standard-MCP client session; it never traverses `DevTools.Daemon`.
- Public tool: reserved `pytest_run`; local node IDs are executed in the host through this tool.
- Progress: normal MCP progress plus optional `notifications/devtools/pytest/case` case events when the client declares `experimental.devtools.pytest.caseEvents.version = "1"`; final result: `PytestRunResponse`.
- Server: `HostMcpServerHostedService`, `PytestRunTool`, `PytestDependencyService`, `PytestExecutionService`, and embedded `PytestRunner.py`.
- `DevTools.Ipc` supplies the canonical pipe-name contract only; do not add a second framed pytest or MCP bridge protocol.

## Change and verification checklist

- MCP protocol/catalog/dispatch changes: preserve SDK semantics, cached Broker behavior, identity rules, .NET/Python registry persistence, and host-safe execution. Verify a focused .NET suite and report unavailable named-pipe/live-host evidence.
- Pytest changes: preserve `pytest_run`, progress-before-final ordering, final response shape, direct-to-host isolation, and optional capability-gated case events. Do not reintroduce framed bridge, `tests/run`, or `tests/discover` prose. Verify the separate sibling with frozen `uv` where possible; do not edit its unknown-owned lockfile.
- Gateway typechecking is static only. It does not prove opaque relay, multi-machine conflict handling, request-size limits, deadlines, or reconnect behavior.

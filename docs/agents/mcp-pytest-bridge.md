# MCP and Pytest Bridge Digest

Deep sources: `docs/MCP/README.md` and `docs/PyTest/README.md`.

## MCP Runtime V2

- Daemon external server/session owner: `source/DevTools.Daemon/Mcp/`.
- Host server: `source/DevTools.Execution/External/Mcp/Hosting/HostMcpServerHostedService.cs`.
- Shared catalog/routing: `source/DevTools.Mcp/Routing/`.
- Daemon-to-host endpoint: standard MCP over `DevTools.Mcp.v2.{pid}`.
- Daemon session: `HostMcpSession` wraps SDK `McpClient`; the host owns SDK `McpServer` sessions.
- Default surface is fixed Broker mode: `devtools_search`, `devtools_invoke`, `launch_host`, `open_model`, `read_file_info`, `list_machines`.
- Search uses cached host snapshots; invoke makes one host request. `hostId` is a daemon-local host PID used only for disambiguation.
- Native mode is opt-in, keeps daemon tools, and projects namespaced host SDK primitives. It needs clients that honor list-changed notifications.
- Built-in primitive names are reserved; dynamic duplicate names/URIs are rejected with diagnostics. .NET/Python configured path persistence and invalid-path pruning are preserved.
- Add a daemon host product through `IHostDriver`, not host-specific broker branches.

Gateway selection is a prior transport decision: call authenticated `GET /machines`, choose a `machine_id`, then send `x-target-machine` on initialize and every later HTTP request. `list_machines` is post-selection only; it cannot bootstrap an unpinned connection when multiple gateway machines are online. Do not overload `hostId` with `machine_id`.

## Direct pytest lane

- Client: separate `RevitDevTool.PyTest` pytest plugin; collection is local.
- Endpoint: `DevTools_{Host}_{Version}_{PID}`.
- Envelope: four-byte little-endian UTF-8 `BridgeMessage` frame.
- Public route: `tests/run` only; local node IDs are remotely executed through this request.
- Progress: `notifications/tests/progress`; final response: `PytestRunResponse`.
- Server: `DevToolsPipeServer`, `PytestRequestHandler`, `PytestDependencyService`, `PytestExecutionService`, and embedded `PytestRunner.py`.
- Pytest does not initialize MCP or traverse `DevTools.Daemon`.
- `DevTools.Ipc` deliberately retains the direct pytest envelope/framing compatibility lane; it does not own MCP DTOs.

## Change and verification checklist

- MCP protocol/catalog/dispatch changes: preserve SDK semantics, cached Broker behavior, identity rules, .NET/Python registry persistence, and host-safe execution. Verify a focused .NET suite and report unavailable named-pipe/live-host evidence.
- Pytest changes: preserve frame/envelope, `tests/run`, progress-before-final ordering, and final response shape. Do not reintroduce stale `tests/discover` prose. Verify the separate sibling with frozen `uv` where possible; do not edit its unknown-owned lockfile.
- Gateway typechecking is static only. It does not prove opaque relay, multi-machine conflict handling, request-size limits, deadlines, or reconnect behavior.

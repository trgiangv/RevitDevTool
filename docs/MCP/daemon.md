# DevTools.Daemon

DevTools.Daemon is the host-agnostic external MCP server. It owns external transport, authentication, gateway tunnel lifecycle, host MCP session discovery, cached catalog coordination, and product-neutral file/launch tooling.

## Modes

| Startup | Behavior |
|---|---|
| `DevTools.Daemon.exe --stdio` | A headless MCP server on stdin/stdout. It has its own discovery/session/catalog lifecycle and exits at client disconnect. |
| `DevTools.Daemon.exe` | Single-instance WPF tray host with dashboard, authentication, gateway tunnel, and local control pipe. |

Stdio and tray processes do not proxy through one another. Each discovers host V2 pipes and maintains its own SDK sessions.

## Runtime ownership

`HostSessionManager` discovers `DevTools.Mcp.v2.{pid}` endpoints, connects a `HostMcpSession` SDK client, observes disconnects/list-changed notifications, and reconnects with bounded backoff. `HostCatalogCoordinator` serializes cached catalog refreshes. The coordinator retains a last successful snapshot for a still-connected host when a refresh fails and removes it only when that host disconnects.

`McpEngine` creates the stable six-tool Broker surface. In default `Broker` mode, host primitives are searchable/invokable cached targets rather than externally flattened runtime tools. Opt-in `Native` mode adds namespaced host SDK proxies and requires an external client that honors list-changed notifications.

Daemon host-product additions implement `IHostDriver`. A driver owns product membership, supported extensions, executable launch behavior, and offline file metadata; it avoids product branches in broker or MCP routing.

## Gateway and control contracts

The tray daemon maintains the authenticated gateway tunnel and preserves register/heartbeat fields (`type`, `machine_id`, `machine_name`, `host_apps`). It transports standard MCP JSON-RPC frames; it is independent of the daemon-to-host V2 pipe.

The retained local `DevToolsDaemon_Control` pipe is a tray-to-host control contract for status, auth, sign-in/out, connected hosts, and dashboard actions. It is not an MCP bridge.

For remote routing, clients select a gateway machine with `x-target-machine` before MCP initialization and on every later HTTP request. The daemon broker's `hostId` only selects a local PID after that machine is selected.

## Source map

| Area | Path |
|---|---|
| Hosting and gateway lifecycle | `source/DevTools.Daemon/Hosting/` |
| MCP engine, sessions, cached catalog | `source/DevTools.Daemon/Mcp/` |
| Host drivers | `source/DevTools.Daemon/Hosts/` |
| Authentication | `source/DevTools.Daemon/Auth/` |
| Dashboard | `source/DevTools.Daemon/Dashboard/` |

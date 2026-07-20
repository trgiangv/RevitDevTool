# DevTools.Daemon

DevTools.Daemon is the host-agnostic external MCP server. It owns external
transport, authentication, gateway tunnel lifecycle, discovery of canonical
host pipes (`DevTools_{Host}_{Version}_{PID}`), cached broker catalogs, and
product-neutral launch/file tooling.

`DevTools.Daemon.exe --stdio` runs an independent local server over stdin/stdout.
The WPF tray process also owns an authenticated gateway tunnel; neither mode
proxies through the other.

## Logical gateway sessions

The gateway WebSocket is a v2 carrier, not an MCP session. When it receives
`session.open`, the daemon creates one `GatewayMcpSession`: independent SDK
server options, channels, cancellation state, initialization state, and
disposal. `mcp.message` is delivered only to the named session and serialized
responses are wrapped with that same `session_id`. Therefore two clients can
both use JSON-RPC ID `1` without sharing server state.

`session.close`, daemon shutdown, and tunnel loss deterministically dispose
only the affected server (or all on carrier shutdown). A malformed text
envelope is dropped before dispatch; a well-formed duplicate `session.open` or
`mcp.message` for an unknown session receives `session.closed` with
`unknown_session`. The daemon accepts/sends only v2 envelopes; raw v1 relay
frames are not compatible.

## Routing boundaries

The client selects a gateway `machine_id` with `x-target-machine` while
initializing a remote MCP session. `hostId` remains a daemon-local host-process
PID selected only by Broker invocation after that machine is selected. The
daemon-to-host standard named pipe (`DevTools_{Host}_{Version}_{PID}`) and the
local `DevToolsDaemon_Control` pipe are separate contracts. Discovery uses the
`DevTools_*` prefix and validates the canonical identity before connecting.

## Catalog lifecycle and launch readiness

`HostSessionManager` keeps a generation-aware PID index, so broker dispatch
looks up `hostId` in O(1) time. Each catalog publication is identified by pipe
name plus generation; an old reconnect completion cannot replace the active
generation. `CatalogService` publishes complete immutable snapshots rather
than incrementally mutating a shared catalog. With fewer than 20 expected
hosts, the full rebuild keeps concurrent readers simple and deterministic.

The catalog state returned by `devtools_search.catalogs` is `refreshing`,
`ready`, `stale`, or `unavailable`. A failed refresh retains a previous
successful generation snapshot as `stale`; without one it is `unavailable`.
`launch_host` waits for a connection and then gives the exact connected
generation a 10-second first-catalog barrier. It returns
`connected_catalog_ready` for `ready`/`stale`, otherwise
`connected_catalog_pending`; callers must search again when pending.

## Source map

| Area | Path |
|---|---|
| Gateway carrier/session manager | `source/DevTools.Daemon/Hosting/` |
| MCP engine and cached catalog | `source/DevTools.Daemon/Mcp/` |
| Host drivers | `source/DevTools.Daemon/Hosts/` |
| Authentication | `source/DevTools.Daemon/Auth/` |

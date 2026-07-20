# DevTools.Daemon

DevTools.Daemon is the host-agnostic external MCP server. It owns external
transport, authentication, gateway tunnel lifecycle, host-pipe discovery,
cached broker catalogs, and product-neutral launch/file tooling.

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
daemon-to-host standard named pipe (`DevTools.Mcp.v2.{pid}`) and retained local
`DevToolsDaemon_Control` pipe are separate contracts.

## Source map

| Area | Path |
|---|---|
| Gateway carrier/session manager | `source/DevTools.Daemon/Hosting/` |
| MCP engine and cached catalog | `source/DevTools.Daemon/Mcp/` |
| Host drivers | `source/DevTools.Daemon/Hosts/` |
| Authentication | `source/DevTools.Daemon/Auth/` |

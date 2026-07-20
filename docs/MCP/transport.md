# MCP transport and gateway routing

## Local transports

`DevTools.Daemon.exe --stdio` is an MCP server over stdin/stdout. Its host
connections and the pytest plugin's host connections are independent standard
MCP sessions on `DevTools_{Host}_{Version}_{PID}`. The tray/control UI uses
`DevToolsDaemon_Control` for control contracts only; it never carries MCP tool
traffic.

```text
RevitDevTool.PyTest -> direct named pipe MCP session -> DevTools_{HostApp}_{HostVersion}_{PID} -> pytest_run
DevTools.Daemon     -> direct named pipe MCP session -> same pipe name -> host catalog/tools
Tray/control UI      -> DevToolsDaemon_Control -> control contracts only
```

## Remote Streamable HTTP

External clients use McpGateway `/mcp` with an authenticated Streamable HTTP
session. `POST` starts or sends a JSON-RPC message, `GET` opens the session's
SSE stream, and `DELETE` cancels/closes the session. An initialize request uses
`x-target-machine` only to bind the random `mcp-session-id` to one daemon and
tunnel generation. Later requests identify that session with `mcp-session-id`;
they do not repeat machine selection. A supplied target header after binding
must match the binding.

```text
External client -> Streamable HTTP session -> McpGateway -> tunnel v2 -> one daemon McpServer
```

The gateway returns the session and protocol headers on session responses. A
missing required post-initialize session header is `400`; unknown, closed,
expired, or generation-invalidated sessions are `404`. `hostId` is a separate
integer PID inside the selected daemon. If several machines are connected, an
unbound initialize requires `x-target-machine`; `GET /machines` lists
candidates before initialization.

Gateway enforces JWT authentication, its exact-origin allowlist for browser
requests, a 1 MiB POST body limit, and per-user rate limiting. It correlates
requests by `(session_id, JSON-RPC id)`, routes server notifications to the
session's SSE stream, expires idle requests after 360 seconds, hard-expires
them after 900 seconds, and expires unused sessions after 30 minutes.

The carrier is tunnel v2 only. One WebSocket text message contains exactly one
JSON envelope with `v: 2`; raw JSON-RPC, newline framing, and v1 fallback are
unsupported. `register`, `registered`, and `heartbeat` manage machine
generations; `session.open`/`session.opened` create an independent daemon MCP
server; `mcp.message` carries opaque scoped JSON-RPC; and
`session.close`/`session.closed` dispose one logical session. A reconnect
invalidates only sessions bound to its previous generation. Malformed daemon
envelopes are dropped before dispatch; well-formed unknown daemon sessions are
closed with `unknown_session`.

# MCP transport and gateway routing

## Local stdio

`DevTools.Daemon.exe --stdio` is a standalone MCP server over stdin/stdout. It
discovers host `DevTools.Mcp.v2.{pid}` named pipes and owns its own broker
catalog lifecycle.

## Remote Streamable HTTP

An authenticated client uses McpGateway `/mcp` as follows:

1. `POST` JSON-RPC `initialize` without `Mcp-Session-Id`; optionally send
   `x-target-machine` to select the daemon.
2. Receive and retain `Mcp-Session-Id` and `MCP-Protocol-Version`.
3. Use session-bound `POST` for messages, `GET` for SSE notifications, and
   `DELETE` to cancel/close. Missing post-initialize session is `400`; unknown,
   closed, expired, and replacement-invalidated sessions are `404`.

`x-target-machine` is bootstrap identity. A later supplied value must match the
binding. `hostId` is a separate integer PID inside the selected daemon. When
multiple machines are online, an unpinned initialize is rejected; `GET
/machines` can discover candidates before initialization.

The gateway applies exact-origin allowlist CORS, JWT validation, a 1 MiB POST
limit, and per-user rate limiting. It exposes session/protocol/request headers
to browsers. Each daemon send is one JSON WebSocket text message containing one
opaque v2 envelope (not newline-delimited framing):

| Frame | Meaning |
|---|---|
| `register`, `registered`, `heartbeat` | Machine registration and generation control. |
| `session.open`, `session.opened` | Create one daemon MCP server per visible session. |
| `mcp.message` | Opaque JSON-RPC scoped by `session_id`. |
| `session.close`, `session.closed` | Dispose that session with a stable reason. |

Requests correlate on `(session_id, JSON-RPC id)` and server messages deliver
to one session SSE stream. Request idle expiry is 360 seconds, hard expiry is
900 seconds, and unused sessions expire after 30 minutes. A same-machine
reconnect increments generation and invalidates only prior-generation sessions;
an old socket close cannot remove the replacement.

There is no v1 tunnel compatibility or raw unscoped JSON-RPC fallback. A live
two-client smoke run requires configured OIDC credentials, an allowed browser
origin, and a daemon tunnel; static checks cannot prove that deployment path.
Malformed daemon text envelopes are dropped before dispatch. A well-formed
duplicate open or `mcp.message` for an unknown daemon session receives
`session.closed: unknown_session`; an unknown Gateway HTTP session is `404`.

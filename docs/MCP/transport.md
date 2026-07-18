# MCP Transport and Routing

## Local stdio

`DevTools.Daemon.exe --stdio` runs a standalone MCP server over stdin/stdout for local clients. It creates its own MCP engine and host-session lifecycle; it has no tray UI, control pipe, or gateway tunnel.

```text
Local MCP client -> stdin/stdout -> DevTools.Daemon --stdio
  -> cached broker -> standard MCP over DevTools.Mcp.v2.{pid} -> host
```

## Gateway transport

The tray daemon opens an authenticated WebSocket tunnel to McpGateway. The gateway relays opaque MCP JSON-RPC between the selected daemon and a cloud client. Register and heartbeat frames carry `machine_id`, `machine_name`, and `host_apps`; they are gateway control messages, not daemon-to-host MCP messages.

```text
Authenticated client -> GET /machines -> choose machine_id
  -> x-target-machine on MCP initialize and every later HTTP request
  -> McpGateway -> selected DevTools.Daemon
  -> broker hostId selects one local host PID
```

The two identities must stay separate:

| Identity | Scope | Selection |
|---|---|---|
| `machine_id` | A gateway-connected computer | Client sends `x-target-machine` before initialization and on every MCP request. |
| `hostId` | One host process on the selected daemon | Broker tool argument; integer PID. |

With one online gateway machine, the gateway can auto-select it. With multiple machines, an unpinned MCP request is rejected before daemon dispatch. Call authenticated `GET /machines` first, then pin the MCP connection. `list_machines` is a post-selection daemon tool and cannot bootstrap an unpinned multi-machine connection.

## Verification boundary

Static typechecking does not prove gateway relay behavior, multi-machine conflict handling, request-size limits, deadlines, or reconnects. Those need a configured authenticated gateway and live daemon tunnel. The gateway’s 1 MiB public-request limit and 180-second response deadline remain gateway-owned constraints; progress notifications do not extend the deadline.

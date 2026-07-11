# Transport Modes

The Daemon supports two independent transport modes. Stdio runs as a separate process; Gateway runs inside the tray host.

```mermaid
flowchart LR
    subgraph Stdio Mode
        Client1["MCP client<br/>Claude Desktop / Cursor"]
        Daemon1["DevTools.Daemon --stdio<br/>direct StreamServerTransport"]
        Client1 <-->|stdin/stdout| Daemon1
    end

    subgraph Gateway Mode
        Client2["AI client<br/>ChatGPT / Perplexity"]
        Gateway["McpGateway<br/>Cloudflare Workers"]
        Daemon2["DevTools.Daemon (tray)<br/>WebSocket tunnel"]
        Client2 -->|"POST /mcp<br/>Streamable HTTP"| Gateway
        Gateway <-->|"WebSocket<br/>NDJSON frames"| Daemon2
    end
```

| Mode | Trigger | Transport | Use case |
|------|---------|-----------|----------|
| **Stdio** | `--stdio` arg | Direct `StreamServerTransport` on process stdin/stdout | Local MCP clients (Claude Desktop, Cursor, VS Code) |
| **Gateway** | Auto on sign-in (tray host only) | Outbound WebSocket to Cloudflare relay | Remote AI clients (ChatGPT, Perplexity) |

## Stdio Mode

When an AI client spawns `DevTools.Daemon.exe --stdio`, a **new process** runs a self-contained MCP server directly on stdin/stdout. It does **not** proxy to the tray app — it boots its own `McpEngine`, `InstanceManager`, and `DiscoveryHostedService` independently.

Key properties:
- Bypasses the `SingleInstance` mutex entirely — multiple stdio processes can coexist with each other and with the tray app.
- Each stdio process discovers host pipes independently (same `DevTools_{Host}_{Version}_{PID}` scan).
- Auth tokens are read from the shared DPAPI file on disk (sign-in via tray or browser still works).
- Process exits when the MCP client disconnects (stdin EOF or cancellation).
- No control pipe, no gateway tunnel, no tray icon in this mode.

## Gateway Mode

Outbound WebSocket connection to the McpGateway (Cloudflare Workers + Durable Objects):

1. `GatewayTunnelClient` connects to `wss://<gateway>/tunnel` with Bearer token
2. Sends `register` frame with `machine_id`, `machine_name`, `host_apps`
3. Wraps WebSocket frames as NDJSON streams via custom adapters
4. Runs full MCP server over that transport
5. Auto-reconnects with exponential backoff (1s → 15s max) on failure
6. Sends periodic `heartbeat` frames with updated `host_apps`

```mermaid
sequenceDiagram
    participant Daemon as DevTools.Daemon
    participant GW as McpGateway (CF Worker)
    participant AI as AI Client

    Daemon->>GW: WebSocket connect (Bearer JWT)
    GW-->>Daemon: 101 Upgrade
    Daemon->>GW: {"type":"register","machine_id":"...","machine_name":"...","host_apps":[...]}

    AI->>GW: POST /mcp (Bearer JWT, x-target-machine: <id>)
    GW->>Daemon: WS text frame (JSON-RPC)
    Daemon-->>GW: WS text frame (response)
    GW-->>AI: HTTP 200 {result}

    loop Every 30s
        Daemon->>GW: {"type":"heartbeat","host_apps":[...]}
    end
    Note over Daemon,GW: Auto-reconnect on disconnect
```

## Multi-Machine Routing

One user can have Daemons on multiple machines connected to the same Gateway. The Gateway's Durable Object maintains a `Map<machine_id, WebSocket>`:

- **Single machine** → AI requests auto-route (no header needed)
- **Multiple machines** → AI must include `x-target-machine: <machine_id>` header
- **Discovery** → `GET /machines` or `list_machines` MCP tool lists connected machines

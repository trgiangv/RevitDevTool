# MCP Integration Architecture

Model Context Protocol integration lets external AI clients (Claude Desktop, Cursor, ChatGPT, Perplexity, etc.) talk to running host applications through the standalone **DevTools.Daemon** and the in-host named-pipe runtime.

The entire MCP stack is host-agnostic. The Daemon discovers and connects to any host pipe (`Revit_*`, `AutoCad_*`, `Civil3D_*`, etc.) via generic `HostBridgeClient`.

Last updated: 2026-06-18

## Documentation

| Document | Contents |
|----------|----------|
| [Daemon](daemon.md) | Architecture, lifecycle, auth, control pipe API, configuration |
| [Transport](transport.md) | Stdio proxy, Gateway WebSocket, multi-machine routing |
| [Tools](tools.md) | Built-in daemon tools, in-host tools, dynamic tool registry |
| [In-Host Runtime](in-host-runtime.md) | Runtime shape, registry flow, dispatch flow, parser library |

## Source Map

| Area | Path |
|------|------|
| **Daemon (primary)** | `source/DevTools.Daemon/` |
| Parser/contracts | `source/DevTools.McpParser/` |
| In-host runtime | `source/DevTools.Execution/External/Mcp/` |
| Pipe server | `source/DevTools.Execution/External/DevToolsPipeServer.cs` |
| Registry UI | `source/DevTools.Presentation/ViewModels/McpRegistryViewModel.cs` |
| Gateway relay | Separate repo: `McpGateway` (Cloudflare Workers + Durable Objects) |

## Verification

Current MCP tests mostly cover parser and contract shapes. When changing MCP behavior:

- Add focused parser/contract tests for schema or identity changes
- Build the host that owns the changed runtime
- State live-host or named-pipe verification gaps when they cannot be run

## Related

- `docs/ai/mcp-pytest-bridge.md`
- `docs/Execution/README.md`
- `docs/PyTest/README.md`
- McpGateway repo: `docs/setup-guide.md`, `docs/architecture.md`, `docs/api.md`

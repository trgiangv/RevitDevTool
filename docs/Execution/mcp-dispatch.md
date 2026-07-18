# MCP Dispatch: SDK Host Primitive Execution

Host MCP execution is standard ModelContextProtocol SDK execution, not a custom `BridgeMessage` route.

```text
DevTools.Daemon McpClient
  -> DevTools.Mcp.v2.{pid}
  -> HostMcpServerHostedService / SDK McpServer
  -> SDK tool, prompt, resource, or resource-template primitive
  -> IHostContextExecutor when a host API context is needed
```

`HostMcpServerHostedService` accepts named-pipe sessions and creates an SDK `McpServer` with collections from `HostMcpServerOptionsFactory`. The corresponding daemon `HostMcpSession` is an SDK `McpClient` that uses standard initialization, list, invocation, notification, cancellation, and error behavior.

## Catalog and dispatch

- Hosts expose SDK primitives from built-ins and configured .NET/Python registrations.
- Built-in names are reserved; duplicate dynamic names and resource URIs are rejected with catalog diagnostics.
- `IHostContextExecutor` and `ExecutionGuardMode.Suppress` preserve host-safe execution for primitives requiring host API access.
- The daemon caches standard SDK list results. Broker search is cache-only; broker invocation resolves a local PID and sends one host MCP request.
- Native daemon mode creates namespaced SDK proxies from those snapshots and therefore requires clients that handle list-changed notifications. Broker mode is the stable default.

## Host boundary

The shared MCP stack is host-neutral. Revit/AutoCAD API calls, host threading, transactions, document context, and render adapters belong in host implementations. A host integration supplies the required adapters and primitive registrations without adding host API references to shared protocol/routing libraries.

## Not the pytest bridge

The separate pytest compatibility lane keeps `BridgeMessage`, four-byte framing, `DevTools_{Host}_{Version}_{PID}`, `tests/run`, and `notifications/tests/progress`. It does not initialize MCP or use the daemon. See [Pytest bridge](pytest-bridge.md).

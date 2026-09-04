# MCP toolset isolation (`Catalog/Isolation`)

Single owner for **ALC identity** when external MCP toolsets load beside an ILRepacked host.
Discovery and invoke code delegate here; they do not duplicate reflected MCP shape handling.

## What lives here

| Type | Role |
|------|------|
| `McpToolsetIsolationPlan` | MCP contract `Pin` list + sibling/resolver sources for `AssemblyIsolationPlan` |
| `McpToolsetContext` / `McpToolsetContextManager` | Per-DLL isolation sessions |
| `MetadataAssemblyPathCollector` | Metadata-only assembly resolution paths (incl. host MCP locations) |
| `McpToolsetContext` | Per-DLL isolation session and identity boundary |

## ADR ownership

- [0019](../../../docs/decisions/0019-ilrepack-and-polyfill-isolated-alc.md) — ILRepack + isolated toolset ALC
- [0023](../../../docs/decisions/0023-shared-assembly-isolation-kernel.md) — isolation kernel (`AssemblyIsolationPlan`); **this folder configures it, does not change it**
- [0027](../../../docs/decisions/0027-mcp-product-surface.md) — MCP product surface (envelope hop; ALC is an invoke backend)

## Out of scope (by design)

- `AssemblyIsolationPlan` kernel API or bind policy
- ILRepack targets, toolset csproj packaging (`ExcludeAssets=runtime`)
- Wire encoding (`McpInvocationResponse` → SDK `CallToolResult`) — Adapter layer

The ALC boundary is protocol JSON. Runtime MCP objects are serialized with the
SDK contract and deserialized by the receiving side; this module must not grow a
second content-block object model or a name-based reflection reader.

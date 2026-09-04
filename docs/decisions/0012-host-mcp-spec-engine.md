# 0012 Host MCP Spec Engine (No SDK on Host)

Date: 2026-08-02

## Status

Accepted — **partially superseded** by [0027](0027-mcp-product-surface.md).

Rules **3** (host removes the MCP SDK entirely) and **7** (ILRepack
must not embed `ModelContextProtocol*`) are withdrawn. The host named
pipe still must not run SDK `McpServer` / `McpSession`; SDK **types
and constants** are allowed on the host. Rules 1–2 and 4–6 stand.

## Context

In-process host (Revit/AutoCAD) and third-party .NET toolsets both referenced the
official MCP C# SDK. ILRepack embedded MCP into `RevitDevTool.dll` while toolsets
used compile-only MCP refs (`ExcludeAssets=runtime`). That created unbounded
assembly-identity workarounds (`ReturnMapper`, `InputRequiredBridge`,
`HostMcpAssemblyResolution`, collectible `AssemblyLoadContext`, …) without a
stable fix.

`DevTools.Mcp.Core` already defines spec-shaped DTOs (`McpInvocationResponse`,
`McpContent`). The host adapter previously converted through SDK `CallToolResult` and
`McpServerTool` at every boundary.

## Decision

1. **Daemon + external clients** keep the official MCP SDK (`DevTools.Daemon`,
   `DevTools.Mcp.Server`, `DevTools.Mcp.Client`).
2. **Third-party toolsets** keep the official MCP SDK (`[McpServerTool]`,
   `CallToolResult`, MRTR exceptions).
3. **Withdrawn ([0027](0027-mcp-product-surface.md)).** Host may
   reference and ILRepack SDK types. The named pipe still must not run
   `McpServer` / `McpSession`.
4. **Toolset invoke boundary** is JSON-only: host reflects and invokes toolset
   methods; results serialize in the toolset assembly domain and deserialize into
   `McpInvocationResponse`. Host never pattern-matches SDK `ContentBlock` types.
5. **MRTR** on the host hop: pass through `arguments`, `inputResponses`, and
   `requestState` on SDK `CallToolRequestParams`. Daemon SDK re-throws
   `InputRequiredException` to external clients. Host does not implement
   high-level `ElicitAsync` / `MrtrContext` suspension. Product loop:
   [0027](0027-mcp-product-surface.md).
6. **Built-in host tools** live in `DevTools.Mcp.Revit` and
   `DevTools.Mcp.Acad` (`IBuiltInMcpTool` / `IBuiltInMcpResource`), registered
   per host in `Composition/*ServiceRegistration`. Sample toolsets (`RevitMcpToolSet`, etc.) are
   dynamic catalog entries only — not replacements for host built-ins.
7. **Withdrawn ([0027](0027-mcp-product-surface.md) +
   [0019](0019-ilrepack-and-polyfill-isolated-alc.md)).** Host may ILRepack
   MCP. Do not exclude `ModelContextProtocol*` by filename.

> **0027:** Items 3 and 7 are withdrawn. Host pipe still must not run
> `McpServer` / `McpSession`. SDK types may live on the host and may be
> ILRepacked. See [0027](0027-mcp-product-surface.md).

## Consequences

Positive:

- Host pipe stays spec-first (`IMcpHandler`); no SDK session in the CAD process.
- **Toolset ALC** (`McpToolsetContext` + `ToolsetResultSerializer` JSON bridge)
  retained; toolsets keep compile-only MCP refs (`ExcludeAssets=runtime`).
- Host image carries MCP for ALC bind ([0019](0019-ilrepack-and-polyfill-isolated-alc.md)).
- Wire behavior is testable with golden JSON (conformance-style).

Tradeoffs:

- Host must track MCP spec wire changes (mitigated by conformance tests).
- One-time migration across `Adapter`, `Catalog`, `Execution`, `Core`.
- Host Tasks extension on pipe deferred unless explicitly scoped later.

## References

- Plan: [`docs/plans/completed/2026-08-02-host-mcp-spec-engine.md`](../plans/completed/2026-08-02-host-mcp-spec-engine.md)
- Boundaries: [`docs/architecture/MCP/platform-boundaries.md`](../architecture/MCP/platform-boundaries.md)
- Product: [`docs/product/mcp.md`](../product/mcp.md)

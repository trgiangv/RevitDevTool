# 0012 Host MCP Spec Engine (No SDK on Host)

Date: 2026-08-02

## Status

Accepted

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
3. **Host in-process runtime** removes the MCP SDK entirely. The host implements
   MCP wire methods on the named pipe (`DevToolsMcp_*`) using spec-aligned DTOs
   in `DevTools.Mcp.Core` — not SDK types.
4. **Toolset invoke boundary** is JSON-only: host reflects and invokes toolset
   methods; results serialize in the toolset assembly domain and deserialize into
   `McpInvocationResponse`. Host never pattern-matches SDK `ContentBlock` types.
5. **MRTR** on the host wire: pass through `arguments`, `inputResponses`, and
   `requestState` on `McpInvocationRequest`. Daemon SDK re-throws
   `InputRequiredException` to external clients. Host does not implement
   high-level `ElicitAsync` / `MrtrContext` suspension.
6. **Built-in host tools** live in `DevTools.Mcp.Revit` and
   `DevTools.Mcp.Acad` (`IBuiltInMcpTool` / `IBuiltInMcpResource`), registered
   per host in `Composition/*ServiceRegistration`. Sample toolsets (`RevitMcpToolSet`, etc.) are
   dynamic catalog entries only — not replacements for host built-ins.
7. **ILRepack** on host must not embed `ModelContextProtocol*` or its transitive
   MCP stack after migration completes.

## Consequences

Positive:

- Eliminates SDK/type-identity conflicts on the **host wire** path.
- **Toolset ALC** (`McpToolsetContext` + `ToolsetResultSerializer` JSON bridge)
  retained; toolsets keep compile-only MCP refs.
- Smaller host bundle; toolsets own their MCP runtime (siblings or ILRepack).
- Wire behavior is testable with golden JSON (conformance-style).

Tradeoffs:

- Host must track MCP spec wire changes (mitigated by conformance tests).
- One-time migration across `Adapter`, `Catalog`, `Execution`, `Core`.
- Host Tasks extension on pipe deferred unless explicitly scoped later.

## References

- Plan: [`docs/plans/completed/2026-08-02-host-mcp-spec-engine.md`](../plans/completed/2026-08-02-host-mcp-spec-engine.md)
- Boundaries: [`docs/architecture/MCP/platform-boundaries.md`](../architecture/MCP/platform-boundaries.md)
- Product: [`docs/product/mcp.md`](../product/mcp.md)

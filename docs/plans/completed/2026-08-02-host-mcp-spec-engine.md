# Execution Plan: Host MCP Spec Engine

Date: 2026-08-02

## Status

Completed 2026-08-02 (phases 0–3). Phase 4 golden tests landed in
[2026-08-31 host-wire](2026-08-31-mcp-sdk-2-2-host-wire.md).
[0027](../../decisions/0027-mcp-product-surface.md) later withdrew “strip SDK from
host”; pipe still has no `McpServer` session ([0012](../../decisions/0012-host-mcp-spec-engine.md)).

## Outcome

Host in-process MCP (`DevToolsMcp_*` pipe) runs without `ModelContextProtocol`
packages. Daemon and toolsets keep the official SDK. Tool invoke round-trips
through `McpInvocationResponse` JSON boundary; live Revit 2025 toolsets
(`get_demo_status`, `revit_get_status`, `test_forwarder_calltoolresult`) pass
without assembly-resolve workarounds.

## Context

- Decision: [`docs/decisions/0012-host-mcp-spec-engine.md`](../decisions/0012-host-mcp-spec-engine.md) (later partially superseded by [0027](../decisions/0027-mcp-product-surface.md))
- Current boundaries: [`docs/architecture/MCP/platform-boundaries.md`](../architecture/MCP/platform-boundaries.md)
- Stop-gap work: collectible toolset ALC (`McpToolsetContext`) shares `ModelContextProtocol*`
  with host default context; host wire moves to spec DTOs separately.

## Scope

In scope:

- Spec DTOs in `DevTools.Mcp.Core/Protocol/`
- `McpHandler` replacing SDK `McpServer` on host pipe
- `ToolsetInvoker` JSON boundary replacing `DotnetToolset*` bridge stack
- Remove MCP SDK package refs from host projects (`Core`, `Catalog`, `Adapter`, `Execution`, host apps)
- Golden wire tests for `tools/list`, `tools/call`, MRTR pass-through fields

Out of scope:

- Daemon SDK replacement
- Gateway auth / tunnel changes
- Python toolset parser rewrite (already non-SDK invoke path)
- Host Tasks extension on pipe (defer)

## Approach

### Phase 0 — ADR + DTO sketch ✅

- ADR 0012, this plan, `DevTools.Mcp.Core/Protocol/*` types.

### Phase 1 — Wire handler (dual-run)

- Add `McpHandler` behind feature flag; keep SDK server until parity.
- Implement `initialize`, `tools/list`, `tools/call` for built-in tools via
  `McpInvocationResponse`.
- Daemon `HostSession` integration test against wire handler.

### Phase 2 — Toolset JSON boundary (ALC invoke retained)

- `ToolsetInvoker` + reflection path kept for tests; **production .NET toolset invoke**
  stays on ALC stack: `DotnetToolsetToolInvoker` → `DotnetToolsetReturnMapper` →
  `McpInvocationResponse`.
- Keep: collectible `McpToolsetContext` (per-toolset `AssemblyLoadContext`),
  `DotnetToolset*` ALC invoke stack, `ToolsetMrtrBridge` for MRTR `Meta` on wire.
- Live verify on Revit 2025 Release toolsets.

### Phase 3 — Remove host SDK

- Drop `CatalogMcpServerTool`, `HostMcpServerFactory` SDK collections.
- `IMcpPrimitiveDispatcher` returns `McpInvocationResponse` only.
- Remove `ModelContextProtocol` from host csproj files; verify ILRepack size.
- MRTR: map toolset `InputRequiredException` by reflection → `McpInvocationResponse.Meta`.
- Rename `Wire/` → `Host/` (`McpHandler`, `McpCatalogMapper`, …).

### Phase 4 — Conformance CI

- Golden JSON fixtures under `tests/DevTools.Mcp.Tests/Wire/`.
- Document in `docs/architecture/MCP/platform-boundaries.md`.

## Risks And Recovery

- **Dual-run drift:** Phase 1 flag; compare `tools/call` output SDK vs wire handler.
- **Regression on daemon:** `HostSession` tests before removing SDK host server.
- **Rollback:** Re-enable SDK `HostMcpPipeServer` until Phase 3 tag; keep deleted
  bridge files in git history only.

## Progress

- [x] Phase 0: ADR 0012, plan, Core Protocol DTO sketch
- [x] Phase 1: `McpHandler` + `McpPipeSession` dual-run (flag `DEVTOOLS_MCP_SPEC_WIRE=1`, removed)
- [x] Phase 2: `ToolsetInvoker` + delete bridge stack
- [x] Phase 3: Remove host SDK pipe server (`HostMcpServerFactory`, `CatalogMcpServerTool`); wire-only pipe; dispatcher returns `McpInvocationResponse`; MRTR via `McpInvocationResponse.Meta`; rename `Wire/` → `Host/`
- [x] Phase 4: Conformance golden tests (see 2026-08-31 host-wire)

## Decisions

- 2026-08-02: Host built-in tools and resources stay in `DevTools.Agents.Revit` /
  `DevTools.Agents.Acad` (`IBuiltInMcpTool` / `IBuiltInMcpResource`); not migrated
  to `*McpToolSet` sample DLLs.
- 2026-08-02: MRTR re-throw stays on daemon SDK; host wire pass-through only.

## Validation

- Focused: `dotnet test tests/DevTools.Mcp.Tests` wire + toolset tests
- Integration: `uv run pytest` MCP client tests (RevitDevTool.PyTest sibling)
- Live: `search_dynamic` + `invoke_dynamic` on Revit 2025 demo + Revit toolsets
- Host compile: hook matrix 2022/2025/2027 after Phase 3

## Result

Host pipe is `McpHandler` (no SDK `McpServer` session). Spec-shaped invoke
round-trips through `McpInvocationResponse`. 0027 later allowed SDK **types**
on the host and ILRepack of MCP; do not resume “remove MCP packages from host”
from this file. Remaining SDK-free contract work is follow-on after
[S5](2026-09-03-mcp-layer-identity-s5.md).

# 0028 Host And ALC Toolset Progress Notifications

Date: 2026-08-31

## Status

Accepted

Companion to [0027](0027-mcp-sdk-host-wire-adoption.md). Product
contract, not a protocol-vocab change.

## Context

[`docs/architecture/MCP/platform-boundaries.md`](../architecture/MCP/platform-boundaries.md)
pass-through rule 2 says `invoke_dynamic` forwards `progressToken` on
`CallToolRequestParams`. The SDK 2.0 gap matrix marks Progress
notifications ✅ via `CallToolRequestServiceProvider` (stale name for
`ToolsetInvocationServices`).

Neither claim matches code on the dynamic/ALC path.

Two independent breaks:

1. `InvokeDynamicTool.InvokeToolAsync` builds `CallToolRequestParams`
   with `Name`, `Arguments`, `InputResponses`, and `RequestState` only.
   It does not copy `context.Params.Meta` or `ProgressToken`. The host
   never sees a client progress token on `invoke_dynamic`.

2. Even if Meta were forwarded, `ToolsetInvocationServices` reports
   progress through an `McpServer` sitting on `SdkNoopTransport`, whose
   `SendMessageAsync` completes without sending. `McpHandler` /
   `McpPipeSession` do not emit `notifications/progress`.

Daemon **fixed** tools (real SDK `McpServer` on stdio / gateway) can
still emit progress. That path is unchanged.

Forwarding Meta from `invoke_dynamic` without a host emitter creates a
false sense of completeness. Spec `progressToken` lived on deleted
`McpInvocationRequest` as unread `long?` ([0027](0027-mcp-sdk-host-wire-adoption.md)
Phase 2). `invoke_dynamic` still does not copy a client token onto host
`CallToolRequestParams`.

## Decision

1. **Progress notifications are not supported** on:
   - `invoke_dynamic` → host `tools/call`
   - isolated .NET toolsets (ALC)
   - Python toolsets
   - host built-ins invoked through the named-pipe handler

2. **Progress remains supported** only on daemon-owned SDK tools
   (the six `McpEngine` tools and any future daemon-local tool that
   uses the real `McpServer` transport).

3. Living docs must mark host/ALC progress ❌ / ⏸, not ✅. The
   `invoke_dynamic` forward-`progressToken` sentence is withdrawn until
   an implementation that covers **both** breaks lands together.

4. Do not ship Meta forwarding as a standalone “fix”. If progress is
   implemented later, one change set must:
   - copy `Meta` (including `progressToken`) from daemon
     `RequestContext` onto host `CallToolRequestParams`;
   - deserialize host `tools/call` params with SDK
     `CallToolRequestParams` so `_meta.progressToken` accepts string or
     number ([0027](0027-mcp-sdk-host-wire-adoption.md) Phase 2);
   - emit `notifications/progress` from `McpHandler` / `McpPipeSession`
     (not into `SdkNoopTransport`).

5. Track that work as gap **G5** if scheduled. It is not in 0027
   Phases 1–4.

## Alternatives Considered

1. **Forward Meta now, document “token is accepted”.**
   Rejected. Token acceptance without notifications is not the product
   feature clients expect.

2. **Leave the gap matrix ✅ and treat this as a small bug.**
   Rejected. Agents and reviewers will keep assuming the path works.

## Consequences

Positive:

- Product surface matches runtime.
- 0027 Phase 2 can delete the dead `ProgressToken` field without
  pretending to enable progress.

Tradeoffs:

- Host long-running tools have no standard MCP progress channel until
  G5. Use tool result text / structured output instead.

## Follow-Up

- Phase 0 of [0027](0027-mcp-sdk-host-wire-adoption.md) (gap matrix +
  `platform-boundaries.md`) landed 2026-08-31.
- G5 only when a host progress emitter is in scope.

## References

- [0027 MCP SDK adoption boundary](0027-mcp-sdk-host-wire-adoption.md)
- [`source/DevTools.Mcp.Server/Tools/InvokeDynamicTool.cs`](../../source/DevTools.Mcp.Server/Tools/InvokeDynamicTool.cs)
  (`InvokeToolAsync`)
- [`source/DevTools.Mcp.Catalog/Discovery/SdkNoopTransport.cs`](../../source/DevTools.Mcp.Catalog/Discovery/SdkNoopTransport.cs)

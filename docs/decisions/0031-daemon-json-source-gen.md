# 0031 Daemon JSON Is Source-Generated

Date: 2026-09-03
Amended: 2026-09-27

## Status

Accepted. Daemon-owned wires use source-generated `System.Text.Json`.
[0032](0032-daemon-mewui-and-aot.md) dropped Native AOT; source-gen stays
because those wires must not depend on reflection serializers. Envelope
`tools/call` shape still follows [0027](0027-mcp-product-surface.md).

Do not rename `ToolHelpers` to `McpJson`.

## Context

Daemon and MCP DTOs used to serialize through reflection. The MCP C# SDK
keeps its protocol graph **internal** and `McpJsonUtilities.DefaultOptions`
is read-only, so app DTOs must `Insert(0, context)` on a **copy**. Facade
collapse landed; remaining `object?` on invoke/batch DTOs is Follow-Up, not
a live plan. Source-gen was first required so 0032 could aim at Native AOT.
AOT is no longer a Daemon target. The wire policy remains.

## Decision

1. **Source-gen is required on Daemon-owned wires** (control pipe, settings,
   daemon tool DTOs). Do not switch those wires back to reflection. This is
   not an AOT milestone.
2. **Two MCP facades, not one options object per assembly:**
   - Protocol: `ToolHelpers.ProtocolOptions` (= SDK `DefaultOptions`)
   - Daemon DTOs: `McpToolJson.Options` (chained `McpServerJsonContext`)
3. **One `JsonSerializerContext` per named wire** (control vs settings vs
   logs vs pytest vs MTP). A new context means a new wire, not a new
   project. Do not merge those lifetimes.
4. **`tools/call` stays spec keys only** (`content`, `structuredContent`,
   `isError`, `resultType`, `_meta`). No `UseStructuredContent` on envelope
   tools until 0027 is revisited.

Foreign ALC JSON (`RuntimeJsonOptions`) is not a third wire — it must not
produce client `tools/call` bytes.

## Alternatives Considered

1. **Drop source-gen because Daemon AOT was dropped.** Rejected — the wires
   still need a stable serializer that does not walk properties by reflection.
2. **One mega-context for all JSON.** Rejected — different wires, TFMs, and
   compatibility lifetimes.

## Follow-Up

- `InvokeCapabilityResponse.Result` / `ResourceReadResult.Result` are still
  `object?`. JIT batch `reads[]` writes through `McpToolJson.Options` (SDK
  chain) and works. Bare `McpServerJsonContext` typeinfo throws — keep that
  characterization until the union is `JsonElement?` (or equivalent) without
  reflection. Capture batch goldens first; counts/shape may change.
- Landed work: [`2026-09-03-stj-facade-0028`](../plans/completed/2026-09-03-stj-facade-0028.md)

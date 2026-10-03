# 0027 MCP Product Surface — Daemon Envelope, Not Full Protocol

Date: 2026-08-31
Amended: 2026-09-04; **2026-10-03** (host pipe — see below)

## Status

Accepted. Does not change [0010](0010-daemon-sole-mcp-host.md).
Partially supersedes [0012](0012-host-mcp-spec-engine.md) (SDK types and
ILRepack allowed).
Pin: **ModelContextProtocol 2.2.0** (`2026-07-28`).

### As implemented (2026-10-03)

[0039](0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md) decision 3 landed: the host
named pipe runs SDK **`McpServer`** per connection (`McpPipeServer` in Catalog).
External clients still never initialize that pipe — only `ProcessSessions` does.
Product locator wire field is **`id`** (`CatalogId`, `dci2.*`), not `capabilityId`.
Search filter field is **`processId`**. Architecture:
[`docs/architecture/MCP/README.md`](../architecture/MCP/README.md).

Product contract: [`docs/product/mcp.md`](../product/mcp.md).
Layer map: [`docs/architecture/MCP/platform-boundaries.md`](../architecture/MCP/platform-boundaries.md).
Feature status: [`docs/architecture/MCP/sdk-gap-matrix.md`](../architecture/MCP/sdk-gap-matrix.md).

## Context

The product client (Cursor) talks to **Daemon**. `tools/list` is a small
**envelope**: infrastructure (`list_host_instances`, `launch_host`,
`read_file_info`, `list_machines`) plus `search_dynamic` / `invoke_dynamic`.
Host CAD capabilities never appear there. The agent loop is search → opaque
`id` → invoke → text / image / `dryRun` / execute error tags, then retry.

That loop is why most of the MCP spec is **not** product work. Gaps are
**use-case limits**, not unfinished adoption.

The CAD process is only the **execution hop** behind `invoke_dynamic`
(named pipe `DevToolsMcp_*`). It is not a second MCP server the client
initializes. Isolated .NET toolsets (ALC) are one optional backend of that
hop — not a client-visible flow.

## Decision

1. **The shipped MCP product is the Daemon envelope.** Clients never call
   host tool names. Host catalog stays behind opaque `id`. One overlapping
   dynamic toolset at a time.

2. **Stabilize that hop’s schema and errors**, not the rest of the protocol.
   Wire bytes are SDK `CallToolRequestParams` / `CallToolResult` /
   list / `ReadResourceResult`. Failures are compact daemon types
   (`validation_error`, `stale_capability`, `invocation_failed`) plus host
   execute tags (`[COMPILATION ERROR]`, `[RUNTIME ERROR]`, `[ROLLBACK]`).
   Stale locators retry by `research_then_reinvoke`. Destructive tools: warning
   + `dryRun`, not elicitation.

3. **The host pipe is not a client-visible MCP session.** External agents still
   use only the Daemon envelope. Behind `invoke_dynamic`, the hop is a full SDK
   **`McpServer`** on `StreamServerTransport` (one server per pipe connection).
   ILRepack of `ModelContextProtocol*` is allowed
   ([0019](0019-ilrepack-and-polyfill-isolated-alc.md)). *Historical:* rule 3
   previously rejected in-host `McpServer`; that choice was reversed in
   [0039](0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md) decision 3.

4. **Out of product scope** (do not schedule as “SDK gaps”):
   - `notifications/progress` on `invoke_dynamic` / host / toolsets
   - Gateway / Cursor elicitation, `ElicitAsync`, Python `Resolve(Elicit)`
   - `resources/subscribe`, `completions`, `ToolUse` / `ToolResult` blocks
   - MCP Tasks on `search_dynamic` / `invoke_dynamic`
   - Host tools on daemon `tools/list`
   - `UseStructuredContent` on envelope tools until Cursor accepts an explicit
     `OutputSchema` (inferred schema from `JsonElement` drops `tools/list`)

   Keep `CallToolPassthroughAsync` + `InputRequiredResult` so a tool throw
   does not corrupt the hop. That is plumbing, not a product workflow.

5. **Keep until a named unblocker:** `McpClientPassthrough` (no public
   no-auto-MRTR send), `DynamicToolResults` (manual structured content).
   `InvokeDynamicMrtrState` in `requestState` is correct, not a workaround.

## Alternatives Considered

1. **Full MCP on the host pipe** (`McpServer` + `initialize`). Rejected for
   *client* exposure — agents never talk to that pipe. **Amended 2026-10-03:**
   the host still runs `McpServer` for the daemon client leg; main-thread
   marshalling stays in tool handlers.
2. **Finish the spec** (progress, elicitation, subscribe) as remaining adoption.
   Rejected — the agent loop does not use them.
3. **Strip the SDK from the host.** Rejected — the hop already serializes SDK
   types; excluding them only shrinks the add-in.
4. **A separate ADR for ALC progress or elicitation.** Rejected — ALC is an
   invoke backend, not a product surface.

## Consequences

Agents inherit the envelope loop and the refuse list. They do not re-propose
client-visible host MCP, envelope `UseStructuredContent`, or spec-completeness
workstreams.

Tradeoff: spec-capable connectors that expect elicitation or host progress
will not get them on `invoke_dynamic`.

## Follow-Up

- Daemon UI: [0032](0032-daemon-mewui-and-aot.md) (HandyControl WPF; AOT
  dropped). JSON source-gen: [0031](0031-daemon-json-source-gen.md).
- ALC packaging (S1/S2) stays in the completed S5 plan
  ([`2026-09-03-mcp-layer-identity-s5`](../plans/completed/2026-09-03-mcp-layer-identity-s5.md))
  — not this ADR.

## References

- [0010](0010-daemon-sole-mcp-host.md), [0012](0012-host-mcp-spec-engine.md),
  [0019](0019-ilrepack-and-polyfill-isolated-alc.md)
- `RequestFactory.cs`, `ToolExecutionTransport.cs`

# 0029 MCP Use-Case Limits — Stabilize Host↔Daemon Schema, Not Full Protocol

Date: 2026-08-31

## Status

Accepted

Companion to [0027](0027-mcp-sdk-host-wire-adoption.md) and
[0028](0028-host-alc-progress-notifications.md). Narrows what remaining
SDK/MRTR items are **product work**.

## Context

The 2026-08-02 MRTR plan and the SDK gap matrix still present **G3**
(Gateway elicitation E2E), **G4** (host `IsMrtrSupported` / stateful
backcompat), **G5** (host/ALC progress), high-level `ElicitAsync`, and
Python `Resolve(Elicit[T])` as open adoption gaps.

Live evidence (Cursor MCP, Revit 2024 net48 + 2025 net8, Python
toolset, 2026-08-31):

- The working agent loop is `search_dynamic` → `capabilityId` →
  `invoke_dynamic` (tools, resources, execute, undo). Clients do not
  pause for MCP elicitation dialogs.
- Destructive product policy is already **warning + `dryRun`** (G2=B),
  not `input_required` for bulk delete.
- Real failures were **schema drift** (dumping python-sdk in-process
  helper fields instead of `ReadResourceResult`) and **error/UI
  robustness**, not missing protocol features.
- Cursor `tools/list` is the daemon six-tool envelope. Host catalog must
  not appear there. `UseStructuredContent` on those envelope tools remains
  forbidden ([0027](0027-mcp-sdk-host-wire-adoption.md)).

Interactive MRTR (client fulfills `inputRequests`, retries with
`inputResponses`) does not match this loop: the CAD session is
synchronous on the host thread, the agent already retries from
compilation/runtime text, and Gateway/Cursor elicitation is unproven
and not required for the shipped workflow.

## Decision

1. **Product use case (in scope).** External AI client (Cursor, etc.) →
   Daemon stdio → named pipe `DevToolsMcp_*` → host `McpHandler` →
   dispatcher. Envelope tools: `search_dynamic`, `invoke_dynamic`, plus
   infrastructure (`list_host_instances`, `launch_host`, …). Host
   capabilities stay behind opaque `capabilityId`.

2. **Stabilize these, in this order:**
   - **Host↔daemon JSON-RPC schema** — SDK `CallToolRequestParams`,
     `CallToolResult`, `ListToolsResult` / resources / `ReadResourceResult`
     via `McpJsonUtilities.DefaultOptions`. `ToolInvoke.py` dumps SDK
     `ReadResourceResult` / `CallToolResult`. Helper dataclasses must not
     reach the pipe. ALC PascalCase stays a Catalog JSON bridge concern.
   - **Error contract** — daemon compact types (`validation_error`,
     `stale_capability`, `invocation_failed`) plus host execute tags
     (`[COMPILATION ERROR]`, `[RUNTIME ERROR]`, `[ROLLBACK]`). Failures
     must not crash the host pane or drop the pipe. Stale locators stay
     `research_then_reinvoke`.
   - **Catalog locators** — versioned `capabilityId`; one overlapping
     dynamic toolset at a time.
   - **Content pass-through** — text, image, structured content, embedded
     resource / resource link as already mapped.

3. **MRTR is plumbing, not a product workflow.** Keep
   `CallToolPassthroughAsync` and `InputRequiredResult` serialization so
   a tool that throws `InputRequiredException` does not corrupt the hop
   (Phase 4 `McpInvocationResponse.InputRequired` stays in-process). Do
   **not** schedule Gateway elicitation (G3), host legacy
   `IsMrtrSupported` resolve against the daemon (G4), product delete
   confirm via elicitation (G2=A), ALC `ElicitAsync`, or Python
   `Resolve(Elicit[T])` as delivery work.

4. **Explicit non-goals (not incomplete adoption):**
   - `resources/subscribe`, `completions`
   - `ToolUse` / `ToolResult` content blocks
   - Host/ALC `notifications/progress` (G5 / [0028](0028-host-alc-progress-notifications.md))
   - MCP Tasks on `search_dynamic` / `invoke_dynamic`
   - Projecting host tools onto daemon `tools/list`
   - Official Inspector as the primary verifier (Cursor MCP is the
     product client; Inspector CLI is optional)

5. **Gap matrix rows G3, G4, G5, live Python MRTR E2E, and
   `outputSchema` on daemon envelope tools** are **deferred by this
   decision**, not medium-priority remaining work.

## Alternatives Considered

1. **Finish the MRTR plan (G3 then G4 then G2=A).**
   Rejected. Elicitation is not how agents operate this product; it
   fights host-thread execution and Cursor’s envelope-tool model.

2. **Strip `InputRequiredResult` from the host wire.**
   Rejected. Spec-shaped JSON and ALC low-level throw already exist;
   deleting them is churn without a safer hop.

3. **Adopt remaining SDK features (subscribe, completions, progress)
   for “completeness”.**
   Rejected. Same class of over-fit as MRTR-as-UX.

## Consequences

Positive:

- Agents and implementers stop treating G3/G4/G5 as the next 0027
  phases.
- Effort stays on wire identity and errors — the class of bug that
  blocked live `resources/read`.

Tradeoffs:

- Spec-capable clients that expect elicitation or host progress will
  not get those on `invoke_dynamic`. Document that; use tool result
  text / `dryRun` / `navigate_history` instead.

## Follow-Up

- Living map: [`sdk-gap-matrix.md`](../architecture/MCP/sdk-gap-matrix.md)
  and [`platform-boundaries.md`](../architecture/MCP/platform-boundaries.md)
  mark G3/G4/G5 ⏸.
- MRTR session plan closed:
  [`2026-08-02-mrtr-implementation.md`](../plans/completed/2026-08-02-mrtr-implementation.md).
- Product contract: [`product/mcp.md`](../product/mcp.md) — MRTR
  pass-through only; no elicitation workflow claim.

## References

- Live Cursor MCP (Revit 2024 net48 + 2025 net8, 2026-08-31) recorded in
  [`2026-08-31-mcp-sdk-2-2-host-wire.md`](../plans/completed/2026-08-31-mcp-sdk-2-2-host-wire.md).
  A 2025 re-run after wire cleanup confirmed `text/plain` resource reads.
- [0027](0027-mcp-sdk-host-wire-adoption.md) Phase 4 (in-process discriminator)
- [0028](0028-host-alc-progress-notifications.md) (host progress unsupported)

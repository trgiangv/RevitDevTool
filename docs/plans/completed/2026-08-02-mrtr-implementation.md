# Plan: MRTR implementation

Date: 2026-08-02  
Closed: 2026-08-31

## Status

**Completed (closed, not fully delivered).** G1 wire + ALC low-level MRTR landed.
Product delete confirm locked to **warning-first (G2=B)**. **G3** (Gateway
elicitation) and **G4** (host `IsMrtrSupported` / stateful resolve) are **not
product** — MRTR stays hop plumbing. Policy:
[0027](../../decisions/0027-mcp-product-surface.md).
## Prerequisite reading

1. [`docs/architecture/MCP/platform-boundaries.md`](../../architecture/MCP/platform-boundaries.md) — ALC + MRTR wire.
2. [`docs/architecture/MCP/sdk-gap-matrix.md`](../../architecture/MCP/sdk-gap-matrix.md) — MRTR row.
3. csharp-sdk:
   - `AIFunctionMcpServerTool.CreateAIFunctionFactoryOptions` (`IsAugmentedWith`)
   - `RequestServiceProvider<T>.IsAugmentedWith`
   - `McpServerImpl.InvokeWithInputRequiredResultHandlingAsync` / `ConfigureMrtr` / `MrtrContext`
   - `InputRequiredException` (low-level) vs `ElicitAsync` (high-level / implicit MRTR)
4. python-sdk:
   - `mcp/server/mcpserver/resolve.py` (`Resolve` / `Elicit[T]` → `InputRequiredResult`)
   - `mcp/client/_input_required.py` + `ClientSession.call_tool(..., allow_input_required=...)`
5. Adoption context: [`2026-08-02-mcp-advanced-features-adoption.md`](../completed/2026-08-02-mcp-advanced-features-adoption.md) Phase 3.

## Goal

End-to-end MRTR for **at least one** host **.NET toolset (ALC)** path:

```text
External client
  → invoke_dynamic (daemon InputRequiredException + InvokeDynamicMrtrState)
  → HostSession.CallToolPassthroughAsync
  → McpHandler → ToolsetInvoker
  → tool throws InputRequiredException (round 1)
  → client retry with inputResponses + requestState
  → same ALC tool reads Params.InputResponses / RequestState → CallToolResult
```

without `__mcp*` hacks or Core tool-specific MRTR metadata.

## Non-goals (this session)

- Re-enabling `McpMrtrMeta`, `DotnetToolProtocolBridge`, or daemon argument augmentation.
- MRTR on `execute_csharp_code` / `execute_python_code` (Tasks Optional preferred).
- ALC support for **high-level** csharp-sdk implicit MRTR (`ElicitAsync` / `MrtrContext`
  handler suspension across round-trips). Document as unsupported on sync ALC invoker.
- Full python-sdk `Resolve(Elicit[T])` inside the Python toolset bridge (optional
  follow-up; see G1-Py).
- `resources/subscribe`, completions, ToolUse blocks.

## SDK alignment (what “correct” means)

| Pattern | csharp-sdk | python-sdk | DevTools target this session |
|---------|------------|------------|------------------------------|
| Low-level throw `InputRequiredException` + read `Params.InputResponses` / `RequestState` | ✅ | Manual / return `InputRequiredResult` | ✅ ALC .NET |
| High-level `ElicitAsync` / `MrtrContext` suspend | ✅ (stateful + MRTR client) | `Resolve(Elicit[T])` | ❌ document unsupported on ALC sync path |
| Server backcompat resolve when `!ClientSupportsMrtr` but stateful | ✅ `InvokeWithInputRequiredResultHandlingAsync` | N/A (different client API) | Verify only (G4); daemon still passthrough |
| Client single round-trip (no auto-retry) | raw send / no CallToolAsync loop | `allow_input_required=True` | ✅ `CallToolPassthroughAsync` (done) |
| Client auto-retry loop | `CallToolAsync` | `_input_required` driver | External connector / not daemon→host |

**ALC contract (locked after G1):**

1. Mirror SDK bind: `RequestContext<CallToolRequestParams>`, `McpServer`,
   `IProgress<ProgressNotificationValue>`, `ClaimsPrincipal` via the same
   `IsAugmentedWith` set as `RequestServiceProvider<CallToolRequestParams>`.
2. Keep `ToolsetInvoker` + `ToolsetResultSerializer` (JSON bridge — do **not** pattern-match
   foreign SDK `ContentBlock` types on the host wire).
3. Tools use **low-level** MRTR only: throw `InputRequiredException`; on retry read
   `context.Params.InputResponses` / `RequestState`; check `server.IsMrtrSupported`
   before throw when product cares about legacy clients.
4. Sync-complete on host thread remains required — no suspended handler continuation
   inside ALC.

## Current inventory (already shipped)

| Piece | Status |
|-------|--------|
| `IHostSession.CallToolPassthroughAsync` | Done — single JSON-RPC round per call |
| `HostToolCallOutcome` + `InputRequired` branch | Done |
| `InvokeDynamicTool` MRTR forward + `InvokeDynamicMrtrState` | Done |
| `InputRequiredException` propagate through host wire + dispatcher | Done |
| Mock tests `InvokeDynamicSdkHarnessTests` MRTR cases | Done — daemon hop only |
| ALC `CallToolResult` text/structured mapping | Done — do not regress |
| `ToolsetInvocationServices` | Done — **invoke-time** mirror of SDK provider |
| `DotnetToolsetAiFunctionFactory` `IsAugmentedWith` | Done — create-time bind mirrors SDK |
| ALC MRTR round-trip automated test | Done — T-ALC-10..15 |
| Python bridge MRTR fields / `InputRequiredResult` | Done — wire bridge + unit tests; `Resolve(Elicit)` deferred |
## Gaps to close

### G1 — Isolated .NET toolset MRTR (split)

#### G1-a — Create-time parameter bind (root cause)

**Problem:** `DotnetToolsetAiFunctionFactory.ConfigureParameterBinding` only consults
`options.Services.IsService(type)`. csharp-sdk always binds augmented types via
`RequestServiceProvider<CallToolRequestParams>.IsAugmentedWith` **before** DI.

Without that, ALC tools cannot reliably inject `RequestContext` / `McpServer` to
implement the SDK low-level MRTR sample (`InputRequiredException` docs).

**Fix layer:** `DevTools.Mcp.Catalog` — `DotnetToolsetAiFunctionFactory` only.
Prefer a local `IsAugmentedWith` duplicate (same four types) rather than
`InternalsVisibleTo` on SDK.

**Acceptance:** Tool method with
`RequestContext<CallToolRequestParams>` + `McpServer` parameters binds from
`ToolsetInvocationServices` at invoke; those params are excluded from input schema.

#### G1-b — Retry visibility (not “apply responses into Arguments”)

**Clarification vs earlier draft:** csharp-sdk does **not** merge `InputResponses`
into `AIFunctionArguments`. Handlers read `context.Params.InputResponses`.

`ToolsetInvoker` correctly puts full `RequestContext` into
`AIFunctionArguments.Services`. After G1-a, retry works if the tool reads Params.

**Acceptance:** Stub tool: round 1 throws `InputRequiredException` with elicitation
request + `requestState`; round 2 with `InputResponses` + echoed `RequestState`
returns success `CallToolResult` through invoker + `ToolsetResultSerializer`.

#### G1-c — High-level MRTR unsupported on ALC

**Problem:** `ElicitAsync` / `MrtrContext` suspension requires the handler task to stay
alive across retries inside `McpServerImpl`. ALC `InvokeSync` demands sync completion.

**Acceptance:** Architecture note in `platform-boundaries.md` + this plan; optional
negative test: documenting that calling `ElicitAsync` from an ALC tool is unsupported
(do not implement suspend). Prefer low-level throw in samples/stubs.

#### G1-Py — Python toolset MRTR (wire done; Resolve deferred)

**Shipped:** `PythonToolPayloadNormalizer`, `PythonResultParser` `input_required` →
`InputRequiredException`, `ToolInvoke.py` MRTR `tools/call` routing, unit tests
`PythonToolsetMrtrBridgeTests` (T-PY-01/02).

**Still deferred:** full python-sdk `Resolve(Elicit[T])` inside the
embedded bridge (T-PY-03); live Python.NET E2E.

### G2 — Product: destructive confirm

| Option | Description |
|--------|-------------|
| **A — MRTR elicitation** | `revit_delete_elements` throws `InputRequiredException` when count &gt; threshold and `dryRun=false`, gated on `IsMrtrSupported` |
| **B — Warning-first (current)** | Structured warning JSON; `dryRun=true` for preview |

Adoption chose **B** pending ALC + Gateway risk. **Recorded 2026-08-02:** G2 stays **B
(warning-first)** until G3/G4 evidence; do not implement A in this track.
### G3 — Gateway / cloud double-hop

**Status:** Open — unvalidated whether `input_required` from daemon reaches Cursor/ChatGPT through
`McpGateway` Worker relay (opaque JSON-RPC relay today — no special MRTR handling).
**Acceptance:** Checklist (stdio mock first, then optional Gateway WebSocket); note
connector needs protocol `2026-07-28` + elicitation fulfillment.

### G4 — `IsMrtrSupported` / legacy / stateful backcompat

**Status:** Open — host stateful backcompat vs daemon passthrough not yet spiked live.
| Scenario | Expected (csharp-sdk) | DevTools verify |
|----------|----------------------|-----------------|
| External client MRTR + daemon passthrough + host MRTR | Incomplete result forwarded | Done (harness) |
| Host tool throws MRTR; daemon↔host negotiated MRTR | Host returns `input_required`; daemon forwards | Spike / live |
| Host tool throws MRTR; daemon client **without** MRTR but **stateful** pipe | Host may **server-resolve** elicitation against daemon | **Must verify** — may hang/fail if daemon has no elicitation handler |
| Tool checks `!IsMrtrSupported` | Structured warning / soft fail | Sample policy when G2=A |

## Proposed implementation order

```text
1. G1-a: DotnetToolsetAiFunctionFactory IsAugmentedWith (+ schema exclusion tests)
2. G1-b: ALC MRTR stub + catalog/dispatcher round-trip tests (matrix T-ALC-*)
3. G1-c: Document high-level ElicitAsync unsupported on ALC; optional negative note
4. G4 spike: host stateful backcompat vs passthrough (matrix T-HOST-*)
5. Product decision G2 (blocked on 1–4 evidence)
6. If G2=A: samples + Scenario 10; else keep warning Scenario 10
7. G3: Gateway checklist (after product path exists or using demo stub)
8. G1-Py: only if Python parity required for G2=A
```

## Test / mock matrix

Legend: **M** = mock/unit (no live host), **I** = in-process integration, **L** = live
host/checklist, **D** = deferred.

### Layer A — Daemon wire (exists; keep green)

| ID | Case | Type | Fixture | Assert vs SDK |
|----|------|------|---------|---------------|
| T-D-01 | Host returns `input_required` → `invoke_dynamic` throws `InputRequiredException` | M | `McpSdkTestHarness` + `MrtrElicitationConfirm` | Same shape as csharp `InputRequiredResult` |
| T-D-02 | Daemon wraps `capabilityId` + args + host `requestState` in `InvokeDynamicMrtrState` | M | same | Opaque blob; host state preserved |
| T-D-03 | Retry forwards `InputResponses` + host `RequestState` on `CallToolRequestParams` | M | `InvokeMrtrRetry` | Matches python retry args / csharp `RequestParams` |
| T-D-04 | Success path still pass-through Content / StructuredContent / Meta | M | existing harness | No re-wrap |

**File:** `InvokeDynamicSdkHarnessTests` (already). Filter: `InvokeDynamicSdkHarness`.

### Layer B — ALC bind (new; G1-a)

| ID | Case | Type | Fixture | Assert vs SDK |
|----|------|------|---------|---------------|
| T-ALC-01 | Method params `RequestContext<CallToolRequestParams>`, `McpServer` are **excluded** from `AIFunction.JsonSchema` | M | Factory create on stub method | Matches `AIFunctionMcpServerTool` schema exclusion |
| T-ALC-02 | Invoke binds `RequestContext` identity equal to call request | M | `DotnetToolsetToolInvoker.InvokeSync` + stub reading context | Same as SDK `RequestServiceProvider` |
| T-ALC-03 | Invoke binds `McpServer` from `request.Server` | M | stub reading `server.IsMrtrSupported` | Same as SDK |
| T-ALC-04 | `IProgress<ProgressNotificationValue>` binds (token / nop) | M | optional | Parity with `ToolsetInvocationServices` |
| T-ALC-05 | Ordinary args still bind from `Params.Arguments` | M | stub with `string name` | No regression |

**Files (new):** `DotnetToolsetAiFunctionFactoryTests.cs` and/or
`DotnetToolsetToolInvokerTests.cs` under `tests/DevTools.Mcp.Tests/`.

**Stub pattern:** static/instance methods in test assembly (same ALC constraints as
production: sync return; throw `InputRequiredException` for MRTR cases). Prefer
**same-process** method resolver stub over full isolated ALC load unless proving
type-identity separately (mapper already covers ALC-shaped JSON).

### Layer C — ALC low-level MRTR round-trip (new; G1-b)

| ID | Case | Type | Fixture | Assert vs SDK |
|----|------|------|---------|---------------|
| T-ALC-10 | Round 1: no `InputResponses` → throw `InputRequiredException` with elicitation `InputRequest` + `requestState` | M/I | Stub tool via invoker or thin dispatcher harness | Matches csharp `InputRequiredException` ctor sample |
| T-ALC-11 | Round 2: `InputResponses` + echoed `RequestState` → success `CallToolResult` | M/I | Same stub | Matches low-level retry contract |
| T-ALC-12 | Round 2 result survives `ToolsetResultSerializer` (text + structured if any) | M | Feed round-2 raw return | No empty Content regression |
| T-ALC-13 | `InputRequiredException` bubbles through `McpPrimitiveDispatcher` / host wire (not converted to error `CallToolResult`) | I | Dispatcher or handler test | Same as built-in path |
| T-ALC-14 | Missing `InputResponses` on retry with required key → tool-defined error or second `input_required` (document stub policy) | M | Stub | Align with chosen stub semantics; csharp low-level is tool-authored |
| T-ALC-15 | Serializer regression: ALC-shaped `CallToolResult` still maps after MRTR success | M | `ToolsetResultSerializerTests` | Keep existing |

**Recommended stub tool (test-only or `McpToolsetDemo`):**

```csharp
// Pseudocode — mirror csharp-sdk InputRequiredException example
static string TestMrtrConfirm(McpServer server, RequestContext<CallToolRequestParams> context)
{
    if (context.Params?.InputResponses is { Count: > 0 })
        return "confirmed";
    if (!server.IsMrtrSupported)
        return "mrtr_unsupported";
    throw new InputRequiredException(
        inputRequests: /* elicitation confirm */,
        requestState: "demo-round1");
}
```

### Layer D — High-level MRTR / Python (document or defer)

| ID | Case | Type | Status |
|----|------|------|--------|
| T-ALC-20 | ALC tool calling `ElicitAsync` is unsupported | D/doc | G1-c — do not implement suspend |
| T-PY-01 | Python payload includes `inputResponses`/`requestState` | M | Done — `PythonToolsetMrtrBridgeTests` |
| T-PY-02 | Python `InputRequiredResult` → host `InputRequiredException` | M | Done — same |
| T-PY-03 | python-sdk `Resolve(Elicit[T])` inside toolset | D | Out of scope — would need host/bridge redesign |

### Layer E — Host / double-hop / legacy (G4)

| ID | Case | Type | Fixture | Assert |
|----|------|------|---------|--------|
| T-HOST-01 | Built-in or demo tool on **full** SDK path (non-ALC) MRTR over named pipe | L/spike | Host + daemon stdio | Baseline: SDK path works end-to-end |
| T-HOST-02 | ALC demo stub MRTR over pipe after G1 | L | `McpToolsetDemo` or Revit stub | Completes G1 live proof |
| T-HOST-03 | Daemon as client: host `IsMrtrSupported` / negotiate version | L/spike | Log capabilities | Record whether MRTR negotiated on pipe |
| T-HOST-04 | If MRTR **not** negotiated: does host attempt legacy elicitation to daemon? | L/spike | Capture hang/error | Decide sample `IsMrtrSupported` gating |

### Layer F — Product + Gateway (G2/G3)

| ID | Case | Type | Notes |
|----|------|------|-------|
| T-PROD-01 | Scenario 10 warning-first (current B) | L | `mcp-integration-test.md` — keep until G2=A |
| T-PROD-02 | Scenario 10 MRTR delete (only if G2=A) | L | Expect `input_required` then confirm |
| T-GW-01 | Stdio daemon MRTR mock through Gateway DO relay | L/checklist | Opaque frame relay; confirm `resultType` preserved |
| T-GW-02 | Connector without elicitation handler | L | Soft failure / timeout documented |

### Coverage map (SDK feature → test IDs)

| SDK behavior | Must cover |
|--------------|------------|
| Augmented DI bind (`IsAugmentedWith`) | T-ALC-01..04 |
| Low-level `InputRequiredException` | T-ALC-10, T-ALC-13 |
| Retry `InputResponses` + `RequestState` | T-ALC-11, T-D-03 |
| Result mapping after ALC invoke | T-ALC-12, T-ALC-15 |
| Daemon opaque capability wrap | T-D-01, T-D-02 |
| Implicit `ElicitAsync` MRTR | Explicit **non-goal** (T-ALC-20 doc) |
| python-sdk Resolve MRTR | Explicit **defer** (T-PY-*) |
| Stateful backcompat resolve | T-HOST-03, T-HOST-04 |

## Validation commands

```powershell
# Daemon wire + ALC mapper + new MRTR ALC tests
dotnet test tests/DevTools.Mcp.Tests --filter "FullyQualifiedName~InvokeDynamicSdkHarness|ToolsetResultSerializer|ToolsetInvoker|Mrtr"

# After naming settles, prefer a dedicated trait or namespace:
# FullyQualifiedName~DevTools.Mcp.Tests.Mrtr
```

Live: `docs/agents/mcp-integration-test.md` Scenario 10 (warning) and optional MRTR
demo scenario after G1/G2.

## Files likely touched

| Area | Files |
|------|-------|
| G1-a | `DotnetToolsetAiFunctionFactory.cs` |
| G1-b tests | `DotnetToolsetAiFunctionFactoryTests.cs`, `DotnetToolsetToolInvokerTests.cs` (new); optional dispatcher/catalog harness |
| Demo stub | `samples/McpToolsetDemo/` `test_mrtr_confirm` (optional live) |
| G1-c / boundaries | `docs/architecture/MCP/platform-boundaries.md`, gap matrix MRTR row |
| G2=A | `TransformTools.cs`, `crud_tools.py` / `element_service.py`, Scenario 10 |
| G1-Py (if promoted) | `McpPrimitiveDispatcher.NormalizePayload`, `PythonResultParser`, invoke script |
| G3 | `docs/agents/mcp-integration-test.md` or Gateway note; no Worker change unless relay breaks |

## Risks

| Risk | Mitigation |
|------|------------|
| Cursor/`invoke_dynamic` breaks on incomplete results | Keep warning path; gate throws on `IsMrtrSupported` |
| Re-entering SDK `CallToolResult` switch on ALC | Keep invoker + mapper + boundary |
| ALC sync + `ElicitAsync` hang | Document unsupported; use low-level throw only |
| Host legacy resolve against daemon (no elicitation handler) | G4 spike before G2=A |
| Gateway drops / mishandles `input_required` | G3 before cloud product claim |
| Over-fixing G1 as “merge InputResponses into args” | Follow csharp low-level: read Params only |

## Progress

- [x] Daemon MRTR wire + harness (T-D-01..04)
- [x] G1-a factory `IsAugmentedWith` (T-ALC-01..05)
- [x] G1-b ALC round-trip tests (T-ALC-10..15)
- [x] G1-c document high-level unsupported (+ optional `test_mrtr_confirm` demo stub)
- [ ] G4 host spike (T-HOST-01..04)
- [x] G2 decision recorded (**B** — warning-first)
- [ ] G3 Gateway checklist (if claiming cloud MRTR)- [x] G1-Py wire bridge (payload + `InputRequiredResult` → exception; unit tests) — `Resolve(Elicit)` still deferred

## Completion criteria

- [x] T-ALC-01..03 and T-ALC-10..13 green
- [x] T-D-* still green (no daemon regression)
- [x] G1-c documented in architecture
- [x] G2 decision recorded (**B** — warning-first; MRTR elicitation deferred pending G3/G4)- [ ] At least one E2E path documented: stdio daemon mock **or** live host ALC stub (T-HOST-02)
- [x] No regression on ALC structured output (`ToolsetResultSerializer` tests green; live optional)

## Decisions

- 2026-08-02 (adoption): Product delete confirm stayed **warning-first (B)** pending ALC + Gateway.
- 2026-08-02 (gap review): G1 root cause refined to **create-time `IsAugmentedWith` bind**, not Argument merging; high-level `ElicitAsync` MRTR out of ALC scope; Python MRTR deferred as G1-Py.
- 2026-08-02 (implementation): G1-a/b/c + G1-Py wire closed.
- 2026-08-02 (cleanup): **G2=B recorded** — warning-first + `dryRun`; MRTR elicitation for bulk delete deferred pending G3/G4.

## Related

- Parent (completed): [`../completed/2026-08-02-mcp-advanced-features-adoption.md`](../completed/2026-08-02-mcp-advanced-features-adoption.md)
- Architecture: [`platform-boundaries.md`](../../architecture/MCP/platform-boundaries.md), [`sdk-gap-matrix.md`](../../architecture/MCP/sdk-gap-matrix.md)
- csharp-sdk refs: `AIFunctionMcpServerTool`, `McpServerImpl` MRTR, `InputRequiredException`
- python-sdk refs: `resolve.py`, `_input_required.py`, `call_tool(..., allow_input_required=True)`

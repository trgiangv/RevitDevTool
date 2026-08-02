# Spec & Plan: MCP advanced features (token + spec UX)

Date: 2026-08-02

## Status

Completed (2026-08-02). MRTR product: [`../active/2026-08-02-mrtr-implementation.md`](../active/2026-08-02-mrtr-implementation.md).
Gap matrix: [`sdk-2-0-gap-matrix.md`](../../architecture/MCP/sdk-2-0-gap-matrix.md).

## Outcome

Extend the **search_dynamic → invoke_dynamic** product path with MCP 2.0 / 2026-07-28 features that
have clear operational value for CAD/BIM agents — without new external daemon tool names and without
subscriptions/completions.

Success criteria:

| Goal | Measure |
|------|---------|
| Token efficiency | Structured hits on high-volume tools; batch `reads[]` for static+template resources; text `Content` not duplicates full JSON |
| Spec-native UX | Resource templates discoverable via `search_dynamic`; MRTR confirm on destructive bulk ops; Tasks optional on long jobs |
| Samples | C# `RevitMcpToolSet` + Python `mcp_toolset` parity per `samples/TOOLSET-SPEC.md` |
| Proof | Unit/contract tests per phase; integration checklist in `docs/agents/mcp-integration-test.md` |

Prior plan `2026-08-02-mcp-sdk-2-0-feature-adoption.md` (phases 0–5) is **complete** — this plan
covers the next layer.

## Product fit (why these features, not others)

DevTools external surface stays **two dynamic tools**. Host capabilities are opaque `capabilityId`
locators. Agents should:

1. **Discover** with `search_dynamic` (`detail=summary` default).
2. **Read** stable model/docs via `resource` / `resource_template` + optional batch `reads[]`.
3. **Act** via `tool` `invoke_dynamic` (vision, mutate, export).
4. **Parse** machine payloads from `StructuredContent` when present.
5. **Confirm** destructive work through MCP-native input (MRTR) when the connector supports it.

**Deferred (low ROI for current agents):**

- `resources/subscribe` — noisy on live Revit models; stateful subscription registry.
- `completions` — CLI agents rarely use argument autocomplete; opaque `capabilityId` flow.
- `ToolUse` / `ToolResult` blocks — product already has `search_dynamic` + `invoke_dynamic`.
- Audio product tools — adapter only.

## Feature matrix

| Feature | Token / UX value | Platform today | This plan |
|---------|------------------|----------------|-----------|
| Resource templates | Read parameterized snapshots without new tools; batch `reads[]` | Catalog + `invoke_dynamic` ✅; samples mostly fixed URI | Add 2 templates + batch demo |
| `UseStructuredContent` on toolsets | Drop duplicate JSON in `Content`; stable parsing | 3 Revit tools only | Top 6 query/write tools C# + Python |
| `ResourceLinkBlock` chaining | Point to template/fixed URI without re-search | Pass-through ✅ | Sample links to new templates |
| MRTR / `InputRequiredException` | One logical confirm for bulk delete; no custom warning JSON dance | Wire pass-through ✅; product warning-first; ALC retry gap | See [`../active/2026-08-02-mrtr-implementation.md`](../active/2026-08-02-mrtr-implementation.md) |
| MCP Tasks Optional | Long export/code without blocking sync pipe | `McpTaskExecutionMeta` ✅ | Live E2E + export tools sample |
| Compact batch reads | One round-trip for cheatsheet + context + selection | `reads[]` ✅ | Integration scenario + prompt hints |

## Architecture constraints

### Double-hop MRTR (critical)

```text
AI client ──tools/call invoke_dynamic──► Daemon (MCP server)
              └── HostSession.CallToolAsync ──► Host MCP server (tool)
```

SDK `McpClient.CallToolAsync` **auto-retries** when the host returns `resultType: input_required`,
via `ResolveInputRequestsAsync` (needs `ElicitationHandler`). `HostSession` registers **no**
handler today → MRTR from host tools will **fail** inside the daemon unless we change behavior.

**Required platform behavior (Phase 3):**

| Layer | Change |
|-------|--------|
| `HostSession` | Add `CallToolPassthroughAsync` using session handler **without** MRTR auto-retry, **or** register a handler that surfaces `InputRequiredResult` to caller |
| `InvokeDynamicTool` | When host returns incomplete, re-emit `InputRequiredResult` on the **daemon** `tools/call` response (not wrap in text JSON) |
| External client | Capable MCP 2026-07-28 clients fulfill elicitation and retry `invoke_dynamic` with `inputResponses` + echoed `capabilityId`/`arguments` |

Fallback for non-MRTR clients: host tools keep **structured warning** path (`dryRun`, threshold message) — no regression.

### Structured content policy (locked)

| Surface | `StructuredContent` | Text `Content` |
|---------|---------------------|----------------|
| Daemon fixed tools | Full typed payload | Compact JSON mirror (existing) |
| Host toolsets (this plan) | Full typed payload | **One-line summary** when `UseStructuredContent=true`; no indented JSON duplicate |
| `invoke_dynamic` errors | None | Compact JSON envelope (existing) |
| Batch `reads[]` | N/A | JSON envelope (existing) |

### Resource vs template vs tool (agent guidance)

| Need | Prefer |
|------|--------|
| Static snapshot (cheatsheet, capabilities, levels list) | Fixed `resource` + batch `reads[]` |
| One element / schedule / view by ID | `resource_template` + `invoke_dynamic(reads[])` if batching |
| Filter / mutate / export | `tool` |
| Vision check | `tool` returning `ImageContentBlock` (single invoke) |

---

## Phase 1 — Resource templates (samples + spec)

**Goal:** Teach parameterized read surface; enable batch reads without per-ID tools.

### 1.1 New templates (C# `RevitMcpToolSet`) — [x]

| URI template | Name | MIME | Returns |
|--------------|------|------|---------|
| `revit://element/{elementId}` | `revit_element` | `application/json` | id, category, family/type, level, pinned, workset, bbox summary |
| `revit://schedule/{scheduleId}/preview` | `revit_schedule_preview` | `text/csv` | CSV header + first N rows (N=30 default; optional `maxRows` query arg if SDK supports template args) |

Optional stretch (Phase 1b if cheap):

| `revit://view/{viewId}` | `revit_view` | `application/json` | name, type, sheet number, scale |

Implementation:

- New `samples/RevitMcpToolSet/Resources/ElementResources.cs` (or extend `ModelResources.cs`).
- Register schedule template as **resource** (not only embedded block in `revit_preview_schedule`).
- Update `revit_model_digest` / `revit_preview_schedule` ResourceLink URIs to match templates.

### 1.2 Python parity — [x]

- `samples/PythonDemo/mcp_toolset/resources/model_resources.py` — `revit://element/{element_id}`, `revit://schedule/{schedule_id}/preview`.
- **Note:** FastMCP uses snake_case template params (`element_id`); C# sample uses camelCase (`elementId`). Agents must use names from `search_dynamic` `argsHint`.

### 1.3 `samples/TOOLSET-SPEC.md` — [x]

Add **Resource templates** section:

- URI table aligned with C#/Python.
- Chaining example: `search_dynamic(kinds=["resource_template"], query="element")` → batch read.

### 1.4 Tests — [~]

| Test | File | Assert | Status |
|------|------|--------|--------|
| Parser discovers templates | `RevitMcpToolSetParserTests.cs` | `UriTemplate` contains `{elementId}` | [x] |
| Catalog search kind filter | `HostCatalogTests.cs` | `resource_template` hits include new URIs | [x] |
| Template read with args | `DynamicToolsAndObservabilityTests.cs` | Mock host template → `invoke_dynamic` with `arguments` | [x] |
| Batch reads mixed fixed + template | `InvokeDynamicSdkHarnessTests.cs` | `reads[]` with fixed + template capabilityIds | [x] |

### 1.5 Integration — [x]

Add to `docs/agents/mcp-integration-test.md` **Scenario 9 — Resource templates**:

1. `search_dynamic(query="element", kinds=["resource_template"])`
2. `invoke_dynamic(capabilityId=…, arguments={elementId: <known id>})`
3. `invoke_dynamic(reads=[cheatsheet capabilityId, element template capabilityId])`

---

## Phase 2 — Structured output on high-volume toolsets

**Goal:** Agents read `StructuredContent` from `invoke_dynamic` pass-through; shrink text tokens.

### 2.1 C# tools (`UseStructuredContent = true`)

| Tool | Structured shape | Text summary (example) |
|------|------------------|------------------------|
| `revit_find_elements` | `{ elementIds[], totalCount, hasMore, offset }` | `"Found 12 elements (total 240, hasMore=true)"` |
| `revit_read_parameters` | `{ elements: [{ id, parameters: [...] }] }` | `"Parameters for 3 elements"` |
| `revit_get_status` | existing health object | `"Model healthy, 0 warnings"` |
| `revit_delete_elements` | `{ deleted_count, failures[], dryRunResults? }` | `"Delete preview: 5 elements"` |
| `revit_place_*` (one representative mutator) | partial-success envelope | `"Placed 8, 1 failure"` |
| `revit_export_pdf` / `revit_export_image` | `{ path, bytes?, warnings? }` | `"Exported to …"` |

Pattern (match existing `revit_get_model_summary`):

```csharp
[McpServerTool(..., UseStructuredContent = true)]
public static CallToolResult FindElements(...) {
    var structured = …;
    return new CallToolResult {
        StructuredContent = JsonSerializer.SerializeToElement(structured, JsonOptions),
        Content = [new TextContentBlock { Text = summaryLine }]
    };
}
```

### 2.2 Python parity

Same tools in `crud_tools.py`, `query_service` responses, `export_tools.py` — return
`CallToolResult` with `structured_content` + short text (FastMCP / python-sdk types).

### 2.3 Platform (no wire change)

- `CatalogMcpServerTool` / `invoke_dynamic` already pass-through `StructuredContent` ✅
- Verify `McpLogPayload` logs structured summary for host tool calls (not full blob).

### 2.4 Tests — [x] contract tests added; parser/DLL gate pending implementation

| Test | File | Assert | Status |
|------|------|--------|--------|
| `InvokeDynamic_PreservesHostStructuredContentWithShortText` | `StructuredOutputTests.cs` | Mock host `StructuredContent` + short text preserved; text &lt; 120 chars | [x] |
| `DotnetParser_StructuredOutputTools_HaveOutputSchema` | `RevitMcpToolSetParserTests.cs` | `revit_find_elements`, `revit_read_parameters`, `revit_get_status` have `OutputSchema` after rebuild | [x] (fails until 2.1 + parser) |
| `ContractTests` | `ContractTests.cs` | `tools/list` via catalog includes `outputSchema` for updated tools | [ ] |
| Parser extraction | `DotnetMcpAssemblyParser` | `UseStructuredContent` tools emit `OutputSchema` in parsed catalog | [x] |
| ALC `CallToolResult` bridge | `DotnetToolsetReturnMapperTests.cs` | Isolated toolset JSON → host `CallToolResult` with text + structured | [x] |

---

## Phase 3 — MRTR pass-through + destructive confirm

**Goal:** MCP-native confirm for bulk delete; no new tool names.

### 3.1 Platform

1. **`IHostSession`** — add `CallToolPassthroughAsync` returning `ResultOrAlternate<CallToolResult, InputRequiredResult>` (SDK type) or internal discriminated union.
2. **`HostSession`** — implement via `McpClient` session send without MRTR retry loop (check SDK for `SendRequestAsync` exposure or `CallToolPassthroughAsync` if added upstream; otherwise thin wrapper).
3. **`InvokeDynamicTool`** — on `InputRequiredResult`:
   - Return incomplete result on daemon `tools/call` (SDK server must support — verify `McpServerTool` handler path).
   - Preserve `capabilityId` + base `arguments` in `requestState` or elicitation message so client retry targets same locator.
4. **Docs** — `docs/product/mcp.md`: MRTR subsection; retry shape for `invoke_dynamic`.

### 3.2 Host helper (copyable to toolsets)

New `source/DevTools.Mcp.Core/McpMrtrMeta.cs` (or extend docs-only pattern):

- Constants for elicitation schema ids.
- `static InputRequiredException ConfirmDelete(int count, string requestState)` helper.

Samples copy slim version into `samples/RevitMcpToolSet/Mcp/`.

### 3.3 `revit_delete_elements` behavior

| Condition | MRTR client | Legacy client |
|-----------|-------------|---------------|
| `dryRun=true` | Preview structured result | Same |
| `count ≤ threshold` | Delete + structured result | Same |
| `count > threshold`, `dryRun=false` | `InputRequiredException` elicitation: "Delete N elements?" | Structured warning (current) — detect `!context.Server.IsMrtrSupported` |

Threshold stays `DeleteConfirmationThreshold` (existing constant).

### 3.4 Python

`revit_delete_elements` in `crud_tools.py` — same branching; Python SDK MRTR API as available in pinned `mcp` package.

### 3.5 Tests

| Test | Layer |
|------|-------|
| Mock host MRTR tool → `invoke_dynamic` returns `input_required` | `DynamicToolsAndObservabilityTests` |
| Elicitation accept → retry → success `CallToolResult` | Same (test client with `ElicitationHandler`) |
| `IsMrtrSupported=false` path → warning JSON, no throw | Mock host toolset unit test |
| Conformance-style diagnostic tool in `McpToolsetDemo` | `test_mrtr_confirm` optional stub |

**Out of scope:** MRTR on built-in `execute_csharp_code` (Tasks path preferred for long work).

---

## Phase 4 — Tasks polish + live proof

**Goal:** Close unresolved item from prior plan; document client opt-in.

### 4.1 Samples

Ensure Optional meta on:

- `revit_export_pdf`, `revit_export_image`, `revit_export_schedule` (C# + Python) — already partially done; verify parity.

### 4.2 Tests

| Test | Notes |
|------|-------|
| `HostTasksLiveIntegrationTests` | Keep; add export tool case if fast enough |
| Unit: `DevToolsMcpTaskExecutionModes` | Optional meta on sample export tools via parser fixture |
| Daemon task + image result | Mock or synthetic slow tool returning `ImageContentBlock` |

### 4.3 Docs

- `docs/product/mcp.md` — Tasks client `_meta` example for Gateway connectors.
- `docs/agents/mcp-integration-test.md` — Scenario: tasks opt-in on `execute_csharp_code`.

---

## Phase 5 — Agent efficiency hooks (token)

Cross-cutting doc + small product tweaks (no new tools).

### 5.1 `search_dynamic` hints

- Ensure `argsHint` on template hits lists template parameters (`elementId`, `scheduleId`).
- Prompt resources: recommend `kinds=["resource","resource_template"]` for read-only prefetch.

### 5.2 Batch read recipes (prompts)

Update `ToolsetPrompts` / Python prompts:

- Session start: `reads[]` = `toolset/capabilities` + `model/context` + `model/selection` (3 locators, 1 invoke).
- After find: `reads[]` = element template for top IDs (cap 5) instead of `revit_read_parameters` when only summary needed.

### 5.3 Token budget table (docs)

Extend `mcp-integration-test.md` benchmarks:

| Operation | Target chars (structured path) |
|-----------|-------------------------------|
| `find_elements` text | &lt; 120 |
| Batch 3 resources | &lt; 4 KiB structured JSON |
| Delete confirm MRTR | 0 extra text round-trip |

### 5.4 Optional code (if benchmark fails)

- `revit://model/context` warm path — carry over from `2026-07-26-mcp-agent-efficiency.md` Phase 2 if still &gt; 500 ms.

---

## Implementation sequence

```text
Phase 1 templates ──► Phase 2 structured ──► Phase 5 doc/prompt hooks
        │                    │
        └──────► Phase 4 Tasks (parallel after Phase 2)
        │
        ▼
Phase 3 MRTR (depends on Phase 2 structured delete response shape)
```

Recommended session order: **1 → 2 → 4 → 3 → 5** (MRTR last — highest integration risk).

## File touch map

| Area | Files |
|------|-------|
| Templates C# | `samples/RevitMcpToolSet/Resources/*.cs`, `MultimodalContentTools.cs` |
| Templates Py | `samples/PythonDemo/mcp_toolset/resources/*.py` |
| Structured C# | `ModelQueryTools.cs`, `TransformTools.cs`, export tools, `ParameterTools.cs` |
| Structured Py | `crud_tools.py`, `query_service.py`, `export_tools.py` |
| MRTR platform | `HostSession.cs`, `IHostSession.cs`, `InvokeDynamicTool.cs`, `HostBroker` if needed |
| MRTR helper | `source/DevTools.Mcp.Core/McpMrtrMeta.cs` (new) |
| Tests | `DynamicToolsAndObservabilityTests.cs`, `StructuredOutputTests.cs`, `ParserIntegrationTests.cs`, `HostCatalogTests.cs` |
| Docs | `docs/product/mcp.md`, `docs/architecture/MCP/tools.md`, `samples/TOOLSET-SPEC.md`, `docs/agents/mcp-integration-test.md` |
| Skills | `.agents/skills/revit-developer/SKILL.md` (batch reads + structured preference) |

## Risks and recovery

| Risk | Mitigation |
|------|------------|
| MRTR breaks Cursor `invoke_dynamic` | Fallback warning path when `!IsMrtrSupported`; feature detect in integration test |
| Daemon cannot emit incomplete `tools/call` | Spike Phase 3 first with conformance mock; escalate SDK issue if blocked |
| Structured output breaks text-only clients | Keep one-line text summary always |
| Template URI collision with tool-returned links | Single source of URI constants in sample |
| RevitMcpToolSet build needs `RevitVersion` | Validate via `scripts/build-host.ps1 -Year 2025` + parser tests without full matrix |

## Validation commands

```powershell
# Unit / contract (repo root) — mock via McpSdkTestHarness (SDK-aligned); live host only for final e2e
dotnet test tests/DevTools.Mcp.Tests --filter "FullyQualifiedName~InvokeDynamicSdkHarnessTests|StructuredOutputTests|DynamicToolsAndObservabilityTests|DotnetToolsetResultBoundaryTests|DotnetToolsetReturnMapperTests|RevitMcpToolSetParserTests|ContractTests"

# After Phase 3
dotnet test tests/DevTools.Mcp.Tests --filter "FullyQualifiedName~Mrtr"

# Live (optional)
dotnet test tests/DevTools.Mcp.Tests --filter "FullyQualifiedName~HostTasksLiveIntegrationTests"
```

Integration: `docs/agents/mcp-integration-test.md` scenarios 1, 5, 9 (resource templates), MRTR delete (new).

## Progress

- [x] Phase 1: Resource templates — C# + Python samples, TOOLSET-SPEC, integration Scenario 9, harness/contract tests
- [x] Phase 2: Structured output — C#/Python toolsets, `DotnetMcpAssemblyParser` OutputSchema, mapper tests
- [~] Phase 3: MRTR wire only — see [`../active/2026-08-02-mrtr-implementation.md`](../active/2026-08-02-mrtr-implementation.md) for product + ALC retry
- [x] Phase 4: Tasks Optional parity verified (C# + Python export tools)
- [x] Phase 5: Prompts, token benchmarks, `search_dynamic` template argsHint, architecture docs
- [x] **P1 ALC boundary** (platform, 2026-08-02): `DotnetToolsetToolInvoker` + `DotnetToolsetReturnMapper` + `CallToolRequestServiceProvider` — bypass SDK `McpServerTool.InvokeAsync` type switch for isolated toolsets; live verified `revit_find_elements`, `revit_read_parameters`, `revit_get_status`

### Open items (non-blocking)

| Item | Notes |
|------|-------|
| `ContractTests` outputSchema | Catalog `tools/list` assertion for structured tools |
| Phase 3 MRTR | [`../active/2026-08-02-mrtr-implementation.md`](../active/2026-08-02-mrtr-implementation.md) — out of scope for this plan |
| `RevitMcpToolSet` in `McpRegistryConfig` per Revit year | Operator config (`%AppData%\RevitDevTool\{Year}\Settings\`) |

### Architecture placement (2026-08-02)

| Layer | Owns |
|-------|------|
| `DevTools.Mcp.Core` | Transport contracts: `HostToolCallOutcome`, broker interfaces, `InvokeDynamicMrtrState` — no tool-specific MRTR meta, no `__mcp*` |
| `DevTools.Mcp.Client` | `CallToolPassthroughAsync` / single-round-trip (SDK-aligned) |
| `DevTools.Mcp.Server` | `search_dynamic`, `invoke_dynamic` — opaque proxy; protocol `CallToolRequestParams` fields only |
| `DevTools.Mcp.Catalog` | Parser, ALC load, **`DotnetToolsetReturnMapper`** (JSON bridge for ALC `CallToolResult`), **`DotnetToolsetToolInvoker`** / **`DotnetToolsetAiFunctionFactory`**, `DotnetMcpCatalogCreateOptions`, `DotnetToolsetResultBoundary` (final safety round-trip) |
| `samples/*` | Pure SDK tools: business logic, structured output, product policies (warnings) |

**Pass-through rules:** discovery from catalog schema only; invoke forwards same arguments + MRTR retry fields on wire; results pass `Content`/`StructuredContent`/`Meta` without re-wrapping; isolated toolset invoke skips SDK result switch — **Catalog maps raw return via JSON before wire**.

## Completion criteria

Plan can move to `docs/plans/completed/` when:

1. [x] Resource templates live + integration Scenario 9
2. [x] Structured output on high-volume tools + ALC boundary fix
3. [x] MRTR wire — deferred product to MRTR plan
4. [ ] Optional: `ContractTests` outputSchema — tracked in gap matrix

Move to `docs/plans/completed/` after optional ContractTests or accept deferral.

## Related plans

- SDK 2.0 base adoption: completed in this refactor session (see gap matrix)
- MRTR product: [`../active/2026-08-02-mrtr-implementation.md`](../active/2026-08-02-mrtr-implementation.md) (active)
- Completed prior: `docs/plans/completed/` — platform split, python MCP migration

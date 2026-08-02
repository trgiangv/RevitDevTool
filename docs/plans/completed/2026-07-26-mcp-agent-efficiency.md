# Execution Plan: MCP agent efficiency (token, latency, observability polish)

Date: 2026-07-26

## Status

Active

## Outcome

Reduce agent token cost and round-trip latency for the standard MCP coding loop
(`search_dynamic` → resources → `invoke_dynamic` tools → `navigate_history`)
without changing the two-tool external surface or host wire contracts.

Target improvements (measured via `docs/agents/mcp-integration-test.md`):

| Metric | Baseline (2026-07-26) | Phase 1 target | Phase 2 target |
|--------|----------------------|----------------|----------------|
| `search_dynamic(query="")` response | ~2,500 tokens | ~1,000 tokens | — |
| `read_file_info` (Snowdon .rvt) | ~1,500 tokens | ~300 tokens (`detail=summary`) | — |
| `invoke_dynamic` resource (text) | Indented JSON double-wrap | Compact JSON | Native content blocks (if SDK allows) |
| `revit://model/context` cold read | ~10s (first after idle) | — | <2s typical |
| `revit://model/context` warm read | ~0.5–10s | — | <500ms after collector optimize |
| Daemon log line for full catalog search | ~9KB result dump | Summary only | — |
| Host tool execution | ✅ Fixed (`CallToolAsync`) | Maintain | — |

## Context

- Product contract: `docs/product/mcp.md`
- Architecture: `docs/architecture/MCP/tools.md`, `docs/architecture/MCP/README.md`
- Agent workflow + baselines: `docs/agents/mcp-integration-test.md`
- Integration pass (2026-07-26): core loop works after `HostSession` sync fix;
  resources OK; token/latency gaps documented in chat review.

### Prerequisite (done)

| Item | Status |
|------|--------|
| `HostSession.CallToolAsync` uses sync `McpClient.CallToolAsync` (not `CallToolWithPollingAsync`) | ✅ Shipped |
| `DaemonSettings` DI registration in `ServerHostBuilder` | ✅ User fixed |
| `mcp-integration-test.md` API names (`search_dynamic` / `invoke_dynamic`) | ✅ Updated |

## Scope

### In scope

**Phase 1 — Quick wins (token + logs, ~1 session)**

1. `search_dynamic`: add `includeSchema` parameter, default `false`.
2. `read_file_info`: add `detail` parameter (`summary` | `full`), default `summary`.
3. `invoke_dynamic` resource path: serialize with `McpJsonUtilities.DefaultOptions` (compact), not `IndentedJsonOptions`.
4. `McpLogPayload`: when logging `search_dynamic` results, emit hit count + targets only (no full catalog JSON in monitor lines).
5. Tool descriptions: note “do not parallelize mutating `invoke_dynamic` on same `hostInstanceId`”.
6. Tests in `tests/DevTools.Mcp.Tests` for new parameters and compact payloads.
7. Doc updates (see [Documentation layers](#documentation-layers)).

**Phase 2 — Host resource latency (~1 session)**

8. `RevitModelContext`: per-category `GetElementCount()` (native count-only; no element iterate).
9. Optional: `revit://model/summary` resource (counts + active view only) if full context still >500ms warm.
10. Re-measure cold/warm latency on Snowdon Towers via integration workflow.

**Phase 3 — Cheatsheet split (optional, ~0.5 session)**

12. Split `revit-csharp-cheatsheet.md` into quick-ref (transactions, units, query) vs advanced (WPF).
13. Expose as `revit://csharp-cheatsheet` (quick) + `revit://csharp-cheatsheet/advanced` or section anchors.
14. Mirror for Python cheatsheet if size warrants.

### Out of scope

- MCP Tasks end-to-end (daemon `WithTasks` + host `tasks/get`) — separate plan when long-running tools need async polling.
- `search_dynamic` embedding `list_host_instances` (nice-to-have; defer unless Phase 1 insufficient).
- Cross-process correlation IDs (daemon ↔ host) — observability v2.
- Dynamic toolset Scenario 6 (42 tools) — requires `McpRegistryConfig.json` setup.
- Multi-host Scenario 7 — no current demand.
- Client-side caching (Cursor MCP panel) — not repository authority.

## Decisions (locked)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| `search_dynamic` schema default | `includeSchema=false` | Built-in tools repeat same schema; agent reads description + cheatsheet. Opt-in for dynamic toolsets. |
| `read_file_info` default | `detail=summary` | Agents peeking before `launch_host`; full link table rarely needed. Breaking: callers needing links must pass `detail=full`. |
| Resource JSON in `invoke_dynamic` | Compact `McpJsonUtilities.DefaultOptions` | Logging already compact; agent responses should match. |
| Tool result JSON (`ToolHelpers`) | Compact one-line via `McpJsonUtilities.DefaultOptions` | No indented pretty-print — saves tokens for agents and logs. |
| Log catalog search results | `StructuredContent` at tool; logger prefers it over full `Content` | No tool-name special case in `McpLogPayload`. |
| `model/context` cache | **Dropped** — no TTL cache | Stale counts after doc/view change; optimize collector only. |
| MCP Tasks | Defer | Sync path sufficient for compile/execute <30s; Tasks mismatch caused P0 regression. |
| Parallel mutating calls | Document only (Phase 1) | No server-side serialization — would add latency and host coupling. |

## Approach

### Phase 1 — Daemon + shared MCP (`DevTools.Mcp`, `DevTools.Daemon`)

```
SearchDynamicTool.Search(...)
  + bool includeSchema = false
  → omit inputSchema from SearchHitDto when false

ReadFileInfoTool.Read(...)
  + string detail = "summary"
  → summary DTO: basicInfo subset, project title, external link count/names only

InvokeDynamicTool.ToCallToolResult(...)
  → McpJsonUtilities.DefaultOptions (compact)

McpLogPayload.SerializeCallToolResult(...)
  → when StructuredContent is set: log that (compact summary for search_dynamic)
  → otherwise: serialize full CallToolResult

ToolHelpers
  → Result<T> / Serialize<T> use McpJsonUtilities.DefaultOptions (compact one-line)

McpEngine tool descriptions (invoke_dynamic destructive hint already set)
  → append one line on parallel mutation hazard
```

### Phase 2 — Host resources (`DevTools.Agents.Revit`)

```
RevitModelContext.Read(uri)
  → build context; category counts via GetElementCount per BuiltInCategory
  → optional RevitModelSummary resource if benchmarks still high
```

### Phase 3 — Content (`DevTools.Agents.Revit/Content/`)

```
revit-csharp-cheatsheet.md → split files
RevitCSharpCheatsheet.cs → register second URI or serve truncated quick ref by default
```

## Documentation layers

Update **one layer per concern** when that phase ships — do not duplicate.

| Phase | Layer | File | What to add |
|-------|-------|------|-------------|
| 1 | Product | `docs/product/mcp.md` | `includeSchema`, `detail` defaults; compact agent payloads |
| 1 | Architecture | `docs/architecture/MCP/tools.md` | `search_dynamic` / `read_file_info` parameters; log summary policy |
| 1 | Agents | `docs/agents/mcp-integration-test.md` | Revised token baselines; parallel-call warning; measurement rows for Phase 1 |
| 2 | Architecture | `docs/architecture/MCP/tools.md` | `revit://model/context` collector strategy (no cache) |
| 2 | Agents | `docs/agents/mcp-integration-test.md` | Latency targets for resource reads |
| 3 | Architecture | `docs/architecture/MCP/tools.md` | Cheatsheet URI split |
| — | Plans | `docs/plans/README.md` | Move this file to `completed/` when validated |

No new `docs/decisions/` unless we adopt a repo-wide agent payload size policy.

## Implementation tasks

### Phase 1

- [ ] `SearchDynamicTool`: `includeSchema` param + DTO conditional serialization
- [ ] `ReadFileInfoTool`: `detail` param + `FileInfoSummaryResult` type
- [ ] `InvokeDynamicTool`: compact resource serialization
- [ ] `McpLogPayload`: summarize `search_dynamic` log results
- [ ] Update `DynamicToolsAndObservabilityTests` + new cases for params
- [ ] `dotnet test tests/DevTools.Mcp.Tests`
- [ ] `dotnet publish source/DevTools.Daemon -c Release` + reload MCP
- [ ] Integration: Scenario 1 + token size check (char/3.5 estimate)
- [ ] Doc layers: product + architecture + agents (table above)

### Phase 2

- [x] `RevitModelContext`: per-category `GetElementCount()`
- [x] Benchmark: 3× `invoke_dynamic` → `revit://model/context` (cold, warm, warm)
- [x] Doc: architecture `tools.md` + agents latency table
- [x] Deploy host: `scripts/kill-host.ps1` + `scripts/build-host.ps1 -Year 2025`

### Phase 3 (optional)

- [ ] Split cheatsheet markdown + register URIs
- [ ] Integration: measure cheatsheet token size (<800 quick ref target)
- [ ] Doc: architecture `tools.md` built-in resources table

## Risks and recovery

| Risk | Mitigation |
|------|------------|
| `detail=summary` breaks agent expecting full `transmissionData` | Document `detail=full`; integration test asserts both modes |
| `includeSchema=false` blocks dynamic toolset discovery | Agents pass `includeSchema=true` when exploring unknown DLL tools |
| Compact JSON in tool results | Agents read one-line JSON; use IDE format if needed |

**Recovery:** Each phase is independently revertable. Phase 1 is daemon-only publish;
Phase 2 requires host redeploy. Git revert per phase if integration baselines regress.

## Validation

### Phase 1 proof

```text
search_dynamic(query="", limit=50)           → char count ↓ ≥40% vs baseline
search_dynamic(..., includeSchema=true)      → schemas present
read_file_info(path, detail=summary)         → char count ↓ ≥70% vs full
read_file_info(path, detail=full)            → unchanged shape
invoke_dynamic → revit://version              → no pretty-print \r\n indent
DevTools.Mcp.Tests                           → all pass
mcp-integration-test Scenario 1                → execute + compile error still pass
```

### Phase 2 proof

```text
invoke_dynamic → model/context (3 sequential)  → warm call <500ms (log durationMs)
Snowdon Towers                               → counts match pre-optimization
```

### Repository checks

- Compile: Cursor stop hook on `.cs` edits
- Unit: `dotnet test tests/DevTools.Mcp.Tests`
- Manual MCP: `docs/agents/mcp-integration-test.md` scenarios 1, 3, 4
- Daemon deploy: `dotnet publish source/DevTools.Daemon -c Release`
- Host deploy (Phase 2+): `scripts/build-host.ps1 -Year 2025`

## Progress

- [x] Prerequisite: `HostSession` sync `CallToolAsync` fix
- [x] Prerequisite: integration baseline captured (2026-07-26)
- [x] Plan authored
- [x] Phase 1 implementation
- [x] Phase 1 unit tests (`DevTools.Mcp.Tests` 51/51)
- [x] Phase 1 docs (product/architecture/agents)
- [x] `dotnet publish source/DevTools.Daemon -c Release`
- [ ] Phase 1 MCP integration re-measure (reload MCP required after publish)
- [x] Phase 2 implementation (`RevitModelContext` per-category counts)
- [x] Phase 2 validation (Snowdon 2025-07-26: warm 19–21ms; counts unchanged)
- [ ] Phase 2 plan move to `completed/` (after Phase 1 MCP re-measure)
- [ ] Phase 3 (if approved after Phase 2 metrics)

## Result

### Phase 2 (2026-07-26)

| Metric | Before | After |
|--------|--------|-------|
| Category counting | 12× `FilteredElementCollector.GetElementCount()` | Per-category native count (no element hydration) |
| Warm `durationMs` (Snowdon, host log) | ~43–64ms (prior session) | **19–21ms** |
| Element counts (Snowdon) | unchanged | Walls 1128, Floors 191, … verified |
| First `invoke_dynamic` after `launch_host` | — | ~30s (daemon MCP client connect to host pipe; not collector) |

`revit://model/summary` deferred — warm reads already &lt;500ms.

### Phase 1+ (pending)

_Complete after Phase 1 MCP re-measure. Record token deltas before moving to `docs/plans/completed/`._

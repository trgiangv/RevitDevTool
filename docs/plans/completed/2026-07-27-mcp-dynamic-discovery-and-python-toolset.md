# Execution Plan: MCP dynamic discovery + Python toolset runtime fix

Date: 2026-07-27

## Status

Implemented 2026-07-27 — pending live MCP integration validation on Revit 2025.

## Progress

- [x] Track A — Python toolset snake_case params; patterns-query/export/documentation updated; registry note in agents digest
- [x] Track B — Substring search + rank (exact → prefix → name → description); no FuzzySharp
- [x] Track C — `argsHint`, `invoke_dynamic.reads[]` batch (no hardcoded starter lists)
- [ ] Live validation — `dotnet publish` daemon, restart Revit, scenarios 1/2/4/6 from integration test doc

## Outcome

Agents keep the fixed two-tool external surface (`search_dynamic` /
`invoke_dynamic`) without context bloat, while:

1. **Python dynamic tools execute correctly** when agents send schema-advertised
   argument names (fix `selectedOnly` / `Field(alias=)` dump bug).
2. **Discovery finds the right capability on the first try** for natural queries
   (hybrid exact + fuzzy), not only exact substrings.
3. **Fewer discovery round-trips** via better `search_dynamic` (`score`, `argsHint`,
   fuzzy ranking) and optional batch read-only `invoke_dynamic.reads[]` — **not**
   hardcoded tool/resource lists on `launch_host`.

Measured against `docs/agents/mcp-integration-test.md` on Revit 2025 + Snowdon
Towers (same baseline session 2026-07-27).

| Metric | Baseline (2026-07-27) | Target |
|--------|----------------------|--------|
| `revit_find_elements` with camelCase / schema args | Fail (`selectedOnly`) | Success |
| `search_dynamic("wall find query")` | 0 hits | Top hit includes `revit_find_elements` (score ≥ cutoff) |
| `search_dynamic("execute")` / `"revit_"` | Exact substring OK | Unchanged or better |
| Round-trips for cold coding loop after launch | search + context + cheatsheet + execute ≈ 4–5 | ≤ 3 (fuzzy search + argsHint + batch read + execute) |
| `search_dynamic` latency @ ≤500 catalog rows | ~8ms | &lt; 50ms wall (daemon) |
| Empty `query` | Full catalog (limit) | Unchanged |

## Context

- Product: `docs/product/mcp.md`
- Architecture: `docs/architecture/MCP/tools.md`, `workflows.md`, `README.md`
- Prior efficiency plan: [`docs/plans/completed/2026-07-26-mcp-agent-efficiency.md`](2026-07-26-mcp-agent-efficiency.md)
  (Phase 1–2 largely done; this plan continues discovery/round-trip work)
- Integration evidence: 2026-07-27 live pass — core execute/undo/resources OK;
  Python `revit_find_elements` broken; substring search miss on natural language;
  `launch_host` ~46s; `model/context` cold ~2s / warm &lt;500ms
- Package already declared: `Raffinert.FuzzySharp` 5.0.3 in `Directory.Packages.props`
  (not yet referenced by any project)
- Python MCP pin today: root `pixi.toml` `mcp==1.26.0`; runtime
  `source/DevTools.Execution/Resources/scripts/pixi.toml` `mcp>=1.27,<2`
- Root cause of alias bug (SDK, still present in `mcp` 2.0.0rc1 source):
  `ArgModelBase.model_dump_one_level` dumps **alias** keys then calls
  `fn(**kwargs)`, so `Field(alias="selectedOnly")` on param `selected_only`
  advertises camelCase but invokes with the wrong kwarg name

## Scope

### In scope

**Track A — Python toolset runtime correctness**

1. Remove `Field(alias=...)` from all FastMCP tool **function parameters** under
   `samples/PythonDemo/mcp_toolset/tools/` (schema = snake_case Python names).
2. Update toolset markdown patterns that show camelCase args
   (`resources/content/patterns-*.md`, prompts) to snake_case.
3. Document registry guidance: do not register Python + C# toolsets that expose
   the **same tool names** simultaneously (ambiguous / divergent schemas).
4. Focused proof: invoke `revit_find_elements` with snake_case args after host
   reload; regression list of previously-aliased params.

**Track B — Dynamic discovery quality (`HostCatalog` / `search_dynamic`)**

5. Add `Raffinert.FuzzySharp` PackageReference to `DevTools.Mcp`.
6. Replace pure substring `Matches` with **hybrid ranking**:
   - empty query → unfiltered catalog (current behavior, honor `limit`)
   - exact / ordinal-ignore-case substring on `target` → score 100 (stable)
   - fuzzy on `target` (and optional short resource name) via
     `TokenSetRatio` or `PartialRatio` (not description-first `WeightedRatio`)
   - description may add a small boost only when target already scores above a
     low floor, or use a high cutoff (≥70) if scored at all
7. Return optional `score` on each `SearchDynamicItem` (omit when empty query).
8. Unit tests for ranking cases (exact prefix, natural phrase, empty query,
   limit, kind filter).

**Track C — Round-trip reduction (search + invoke, no hardcoded lists)**

9. ~~`launch_host.starter`~~ **Rejected** — do not hardcode built-in tool/resource
   names/URIs; discovery stays catalog-driven via `search_dynamic`.
10. `search_dynamic` gains compact `argsHint` per tool hit: up to N top-level
    property names from `inputSchema` (default on; cap N=8). Full schema still
    behind `includeSchema=true`.
11. `invoke_dynamic` gains optional batch **read-only** mode:
    - new optional `reads: [{kind, target, arguments?}, ...]` **or** keep single
      `kind`/`target` (mutually exclusive)
    - only `resource` / `resource_template` (and optionally tools annotated
      read-only if catalog carries the hint — v1: resources only)
    - sequential fan-out on one session; fail-soft per item; no parallel mutate
12. Docs: product + architecture + agents integration baselines.

### Out of scope

- Migrating pixi / runtime to `mcp==2.0.0rc1` (`FastMCP` → `MCPServer`) — separate
  plan after 2026-07-28 stable or explicit pin; **does not fix alias dump bug**.
- Changing the “exactly two dynamic operations” product rule (no projecting host
  tools into daemon `tools/list`).
- Batch **mutating** tools / server-side mutation queue.
- Embedding / vector search.
- Cheatsheet split (remains Phase 3 of `2026-07-26-mcp-agent-efficiency.md`).
- Multi-host Scenario 7 / NuGet PEP 723 Scenario 8 unless needed for proof.
- Upstream fix to python-sdk `model_dump_one_level` (nice-to-have; do not block).

## Decisions (locked for this plan)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Python tool arg wire names | **snake_case only** (drop `Field(alias=)` on params) | Matches Python MCP convention; avoids SDK dump bug without waiting on v2 |
| C# RevitMcpToolSet args | Keep camelCase (CLR / existing schema) | Separate assembly; do not dual-register with Python under same names |
| Registry when testing | Prefer **one** of PythonDemo or RevitMcpToolSet | Prevents ambiguous resolve and schema divergence |
| Search algorithm | Hybrid: exact/substring target first, then FuzzySharp on target | Preserves `"revit_"` / `"execute"`; fixes natural queries |
| Fuzzy scorer | `TokenSetRatio` primary; `PartialRatio` acceptable fallback | Tool names are token-like; avoid description-dominated WeightedRatio |
| Fuzzy cutoff | Default 60 for inclusion when not exact; exact always included | Tunable constant in `HostCatalog` |
| Description in score | Boost ≤ +10 only if target score ≥ 40; else ignore | Stops `"wall"` matching every long description |
| Empty query | No scores; current filter/order | Agents use empty query as catalog dump |
| `argsHint` | Default **on**, max 8 property names, no types | Removes most `includeSchema=true` round-trips |
| `launch_host.starter` | **Out of scope** | User intent: optimize search, not hardcode URIs |
| Batch invoke v1 | Read-only resources/templates only | Lowest risk; matches “context + version” loop |
| Package | `Raffinert.FuzzySharp` already in CPM | Add ProjectReference only to `DevTools.Mcp` |

### Open only if blocked during implement

None expected. If batch API shape conflicts with SDK `McpServerTool.Create`
parameter binding, prefer adding a **second** tool `invoke_dynamic_batch` rather
than overloading — record decision in Progress if that happens.

## Approach

### Track A — Python toolset

```
samples/PythonDemo/mcp_toolset/tools/*.py
  - Remove Field(alias="camelCase") from every tool parameter
  - Keep Field(ge=..., description=...) without alias
  - Param names remain snake_case (selected_only, max_results, element_ids, …)

dto/*.py populate_by_name models
  - Unchanged (response / nested models may keep aliases for JSON shape)

resources/content/patterns-*.md + prompts
  - Show snake_case argument examples

McpRegistryConfig (operator note in agents digest)
  - Document: register either pythonToolsetPaths OR overlapping
    dotnetToolsetPaths, not both for the same revit_* names
```

Restart host after toolset/registry change (not hot-reloadable).

### Track B — Hybrid search

```
DevTools.Mcp.csproj
  + PackageReference Raffinert.FuzzySharp

HostCatalog.Search
  → EnumerateHits (unchanged host/kind filters)
  → if needle is null: OrderBy kind/target, Take(limit)  // current
  → else: score each hit, drop below cutoff unless exact,
           OrderBy score desc, then kind, target; Take(limit)

Scoring (pseudocode)
  exactOrContains(target)     → 100
  else TokenSetRatio(query, target) → 0..100
  + descriptionBoost (0..10) if targetScore >= 40
  keep if score >= Cutoff (60) OR exact

SearchDynamicItem
  + int? Score  // JsonIgnore when null

SearchDynamicTool description
  → update from "Substring match" to "Exact/substring on name, fuzzy fallback"
```

Do **not** open host pipes. Search stays daemon-local.

### Track C — Round-trips (search-first)

```
SearchDynamicItem
  + string[]? ArgsHint  // top-level inputSchema.properties keys, max 8
  + int? Score          // when query non-empty

InvokeDynamicTool
  reads: JsonElement?  // array of {kind,target,arguments?}
  when reads set: kind/target optional/ignored; reject if any kind=tool

  Response: { results: [ { target, ok, content|error }, ... ] }
  Sequential; fail-soft per item
```

### Agent loop target (after this plan)

```
launch_host → hostInstanceId
search_dynamic("execute" | "model context" | natural query) → score + argsHint
invoke_dynamic batch reads [targets from search hits]
invoke_dynamic tool execute_* | revit_*
```

## File map

| Area | Files |
|------|--------|
| Python tools | `samples/PythonDemo/mcp_toolset/tools/*.py` |
| Python docs in toolset | `samples/PythonDemo/mcp_toolset/resources/content/*.md`, `prompts/*.py` |
| Catalog search | `source/DevTools.Mcp/Broker/HostCatalog.cs`, `HostCatalogModels.cs` |
| Search tool DTO | `source/DevTools.Mcp/Tools/SearchDynamicTool.cs` |
| Invoke / batch | `source/DevTools.Mcp/Tools/InvokeDynamicTool.cs` |
| Package ref | `source/DevTools.Mcp/DevTools.Mcp.csproj` |
| Tests | `tests/DevTools.Mcp.Tests/HostCatalogTests.cs`, `DynamicToolsAndObservabilityTests.cs`, new ranking/batch tests |
| Product | `docs/product/mcp.md` |
| Architecture | `docs/architecture/MCP/tools.md`, `workflows.md` |
| Agents | `docs/agents/mcp-integration-test.md` |
| Plans index | `docs/plans/README.md` |

## Documentation layers

Update **one layer per concern** when that track ships.

| Track | Layer | File | What |
|-------|-------|------|------|
| A | Agents | `mcp-integration-test.md` | Snake_case Python args; one-toolset registry note |
| A | Architecture | `PythonDemo` or MCP tools.md dynamic tools note | Alias policy |
| B | Product | `mcp.md` | Search = hybrid; optional `score` |
| B | Architecture | `tools.md` | Ranking rules + FuzzySharp |
| C | Product | `mcp.md` | `argsHint`, batch reads contract |
| C | Architecture | `tools.md`, `workflows.md` | Search hints + batch read loop |
| C | Agents | `mcp-integration-test.md` | New baselines / scenario order |

No new `docs/decisions/` unless batch becomes a second external tool name
(`invoke_dynamic_batch`) — then add a short decision.

## Implementation tasks

### Track A — Python toolset (P0)

- [ ] Inventory all `Field(alias=` on tool params (`rg` under `tools/`)
- [ ] Strip aliases; leave validation constraints
- [ ] Update pattern markdown + prompt examples to snake_case
- [ ] Operator note: single overlapping toolset in `McpRegistryConfig`
- [ ] Host restart + MCP: `invoke_dynamic` → `revit_find_elements` with
      `filters` + `max_results` (snake_case) on Snowdon → success
- [ ] Spot-check 2–3 other previously-aliased tools (`revit_read_parameters`,
      `revit_write_parameters` or list_rooms if args)

### Track B — Hybrid search (P0/P1)

- [ ] PackageReference FuzzySharp on `DevTools.Mcp`
- [ ] Failing tests: natural query ranks `revit_find_elements`; exact `execute`
      still first-class; empty query unchanged; cutoff drops garbage
- [ ] Implement scoring in `HostCatalog`
- [ ] Expose `score` on search DTO
- [ ] Update tool description text
- [ ] `dotnet test tests/DevTools.Mcp.Tests`
- [ ] Live: `search_dynamic("wall find query")` returns find_elements in top 5

### Track C — Round-trips (P1)

- [ ] Extend `LaunchHostResult` + fill `starter` from catalog after connect
- [ ] `argsHint` extraction helper (safe if schema missing/malformed)
- [ ] Batch read path on `invoke_dynamic` (or `invoke_dynamic_batch` if needed)
- [ ] Unit tests: starter shape; argsHint length; batch rejects tools; batch
      partial failure
- [ ] `dotnet publish` Daemon + reload MCP
- [ ] Live measure: cold loop hop count ≤ 3 before first execute
- [ ] Doc layers (product / architecture / agents)

### Close-out

- [ ] Re-run `mcp-integration-test.md` Scenarios 1, 2, 4, 6 (toolset)
- [ ] Record metrics in Result
- [ ] Move this plan to `docs/plans/completed/` after validation
- [ ] Cross-link / complete leftover items on `2026-07-26-mcp-agent-efficiency.md`
      if superseded

## Risks and recovery

| Risk | Mitigation |
|------|------------|
| Snake_case breaks agents/docs that cached camelCase schemas | Host restart refreshes catalog; update toolset patterns; C# toolset unchanged for teams using only DLL |
| Dual toolset still registered | Agents digest + integration checklist; ambiguous resolve already returns error |
| Fuzzy false positives | Exact-first + cutoff + description demoted; unit fixtures for noisy queries |
| Fuzzy slows large catalogs | Score only filtered host/kind set; target strings short; assert &lt;50ms @500 rows in unit microbench optional |
| Batch API binding awkward | Fall back to `invoke_dynamic_batch`; decision note |
| `starter` empty if catalog late | Connection wait already requires MCP session; if empty, omit field (agents fall back to search) |
| Daemon publish breaks stdio | Reload MCP after publish (existing runbook) |

**Recovery:** Track A is sample-only (revert files + host restart). Track B/C are
daemon/Mcp library — `git revert` + `dotnet publish` Daemon. Host rebuild not
required unless registry/toolset DLL changes.

## Validation

### Focused proof

```text
dotnet test tests/DevTools.Mcp.Tests
  - HostCatalog ranking tests green
  - search_dynamic DTO score/argsHint tests green
  - launch_host starter contract test (if mockable) or LaunchHostResult shape test
  - batch read rejects kind=tool; sequential partial failure
```

### Integration proof (`mcp-integration-test.md`)

```text
1. Registry: only one of PythonDemo | RevitMcpToolSet for overlapping names
2. Restart Revit 2025 + reload MCP
3. launch_host(Snowdon) → starter.tools contains execute_csharp_code;
   starter.resources contains revit://model/context
4. search_dynamic("wall find query") → revit_find_elements in top results + score
5. search_dynamic("execute") → execute_csharp_code present (no regression)
6. invoke_dynamic revit_find_elements snake_case args → element list (Python path)
   OR C# toolset equivalent if Python disabled
7. Batch reads [model/context, version] → both ok, one round-trip
8. Scenario 1 execute + compile error still pass
9. Record: hop count, search latency, token estimate vs 2026-07-27 baseline
```

### Repository checks

- Compile: Cursor stop hook on `.cs` edits
- Unit: `dotnet test tests/DevTools.Mcp.Tests`
- Daemon: `dotnet publish source/DevTools.Daemon -c Release`
- Host: restart after toolset/registry change; rebuild host only if add-in code changes
- Manual: `docs/agents/mcp-integration-test.md`

## Progress

- [x] Integration baseline + diagnosis (2026-07-27)
- [x] Spec/plan authored
- [ ] Track A implementation
- [ ] Track B implementation
- [ ] Track C implementation
- [ ] Docs layers
- [ ] Validation + Result
- [ ] Move to `docs/plans/completed/`

## Decisions log

- 2026-07-27: Prefer toolset-side snake_case over waiting for mcp 2.0.0rc1 /
  upstream `model_dump_one_level` fix — rc1 still dumps aliases into `fn(**kwargs)`.
- 2026-07-27: Hybrid search over full substring replacement — preserve exact
  agent workflows already working.
- 2026-07-27: Batch v1 = read-only resources only — avoid mutation ordering hazards.

## Result

_Complete after validation. Record hop counts, search ranking samples, toolset
invoke success, and any leftover follow-ups._

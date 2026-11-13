# Execution Plan: Models Context Panel

Date: 2026-11-13

## Status

Active

## Outcome

A dockable **Model Context** panel in Revit 2023+ that browses, filters and
selects elements of the Current model and its links, and shows, hides,
isolates, halftones or colors them in the active view, as specified in
[0062](../../decisions/0062-model-context-panel.md). The core and the views
run and are tested out of process on a fake model before the Revit adapter
lands.

## Context

- Decision: [0062](../../decisions/0062-model-context-panel.md) (behavior §1–§10,
  architecture §11, UI and targets §12, storage §13, ownership §15, spike in
  Follow-Up).
- Design canvas: *Model Context Panel — UX directions*, page "Panel + user
  flows". 0062 wins where they differ.
- Boundaries: [host-boundaries](../../agents/host-boundaries.md). The core is
  shared (`DevTools.*`), the adapter is Revit (`RevitDevTool.Tools`).
- Existing pieces to reuse: `RevitDevTool.Core/RevitContextExecutor`
  (`RaiseAsync` with cancellation), `RevitDevTool.Core/DockablePaneProvider`,
  `RevitDevTool.Tools/Selection/ElementSelector`, `TokenParser`,
  `ElementSearcher`, `DevTools.UI` HandyControl theme and frozen icons,
  `DispatcherTimer` debounce pattern, `tests/DevTools.HandyControl.Tests`
  (`WpfSta` + FlaUI).
- Proof: `.agents/skills/build/SKILL.md`, `docs/agents/test-matrix.md`,
  `.agents/skills/revit-test/SKILL.md` for host tests,
  `.agents/skills/ci-change/SKILL.md` for new projects.

## Scope

In scope:

- `source/DevTools.ModelsContext/` (core: model, engine, ports, view models).
- `source/DevTools.ModelsContext.Views/` (XAML).
- `DevTools.UI` controls: flat tree list, summary `CheckComboBox` template,
  icon geometries; the "All" sync fix in `libs/HandyControl` `CheckComboBox`.
- `RevitDevTool.Tools/ModelsContext/` (Revit adapter T0–T2, pane, filter
  compiler and catalog).
- Tests: `DevTools.ModelsContext.Tests`, `DevTools.ModelsContext.Views.Tests`,
  `DevTools.ModelsContext.Tests.Shared`, and the
  `DevTools.ModelsContext.Sandbox` app under `tests/`.
- The 0062 spike on a live host and, if it passes, the T3 queue.
- CI wiring for the new projects; product and architecture docs at the end.

Out of scope:

- Context strip and context popup (0062 §14).
- Replace / writing parameter values.
- An AutoCAD adapter (the core must allow it; it is not built here).
- Nested links in T3.

## Approach

Phases run in order; within a phase, tasks with disjoint files run in
parallel. Each task names its owner files, its proof, and the agent class:
**Opus** for engine, adapter and threading work, **Sonnet** for bounded
scaffolding, XAML and tests. The main session keeps this plan, reviews each
diff, runs format-lint once per phase and records proof here.

### Phase 0 — Scaffolding (Sonnet)

- Create the four projects with the repo's conventions:
  - `DevTools.ModelsContext`: `net48;net8.0-windows;net10.0-windows`, no
    `UseWpf`, no Revit, `CommunityToolkit.Mvvm`.
  - `DevTools.ModelsContext.Views`: same TFMs, `UseWpf`, references the core
    and `DevTools.UI`.
  - `tests/DevTools.ModelsContext.Tests` and `.Views.Tests`: `MSTest.Sdk`,
    same TFMs (copy `DevTools.HandyControl.Tests` settings; Views.Tests adds
    `FlaUI.UIA3`).
  - `tests/DevTools.ModelsContext.Tests.Shared`: library.
  - `tests/DevTools.ModelsContext.Sandbox`: WPF exe, `net10.0-windows`.
- Add all to `RevitDevTool.slnx`; reference the core and views from
  `RevitDevTool.Tools`.
- CI: follow the ci-change skill (`product-paths`, `ci-jobs.json`,
  `ProjectGraph`) so the two test projects run in CI.
- Proof: build of every new project and of `RevitDevTool.Tools` for one Revit
  config; CI job list shows the new tests.

### Phase 1 — Core model and engine (Opus, tests by Sonnet in parallel)

Owner: `source/DevTools.ModelsContext/Model`, `Engine`, `Ports`;
`tests/DevTools.ModelsContext.Tests.Shared`; `tests/DevTools.ModelsContext.Tests`.

- Model: `DocumentKey`, `ElementRef`, `ElementRow`, `TypeRow`,
  `DocumentSnapshot`, `TreeSchema`, `TreeRow`, `FilterDefinition`,
  `VisibilityRequest`, `VisibilityPlan`, document capabilities.
- Ports: `IModelSource`, `IModelEvents`, `ISelectionPort`, `IViewActions`,
  `IFilterEvaluator`, `IHostFilterCatalog`, `ICustomFilterStore`, `IClock`,
  scheduler.
- Engine: `SnapshotAggregator` (counts per level, All models merge),
  `DeltaApplier` (added, modified, deleted; dirty above about 2,000),
  `ScopeCounter` (view and selection id sets), `ValueIndexCache` (cap 500,
  LRU 8), `VisibilityPlanner` (§7 group rules, §9 splits, disabled reasons),
  `SelectionGuard` (sent-set drop), `TemporaryJournal`, `Debouncer`.
- Fake model in Tests.Shared: generator (seeded, about 100k elements, three
  links, two instances of one link file) and snapshot JSON load/save.
- Expected tests (short names): counts match generator totals; delete
  decrements chain; large delta marks dirty; All models merges categories;
  Current model row plans hide by id; All models row plans group hide; link
  row plans reference step; halftone on link child is disabled; selection
  spanning documents splits; self-caused selection is dropped; debounce fires
  once; value index caps at 500; two link instances share one snapshot.

### Phase 2 — View models (Opus)

Owner: `source/DevTools.ModelsContext/ViewModels`; VM tests in
`DevTools.ModelsContext.Tests`.

- `ModelContextViewModel`, `ContextBar` (scope, documents, at least one
  ticked), `TreeTab` (flat rows, expand inserts a range with one Reset, paging
  "200 of N · more", search on row text, Sync with 50 / 5,000 thresholds,
  status line), `FilterTab` (source list, custom filters with naming rules,
  sets, Live with baseline for Add/Remove, pause above 5,000), `TemporaryBar`
  (N changes, Keep confirm when T3 is pending, Reset), `ContextMenu` (items,
  hidden Select while syncing, disabled reasons, shortcuts).
- Expected tests: tree expand inserts children; Sync opens path under 50;
  Sync shows badges above 50; Live pauses above 5,000; name must be unique;
  empty name blocks save; Keep asks when link changes pending; Select hidden
  while Sync on.

### Phase 3 — Controls, views and sandbox (Sonnet; flat list by Opus)

Owner: `source/DevTools.UI/Controls/FlatTreeList*`, the summary
`CheckComboBox` template, `libs/HandyControl/.../CheckComboBox.cs` (move the
"All" sync above the early return), `source/DevTools.ModelsContext.Views`,
`tests/DevTools.ModelsContext.Views.Tests`, `tests/DevTools.ModelsContext.Sandbox`.

- Views follow 0062 §2, §4, §5, §7, §12 and the canvas; every interactive
  element has an `AutomationId`.
- Sandbox: opens the pane on the fake model; switches generated / replayed
  model, light / dark theme, and a "slow host" delay.
- Expected UI tests (FlaUI by AutomationId, UIA patterns, no mouse events):
  tabs are Tree and Filter; group header has no controls; eye toggles row;
  context menu lists actions; documents box shows one line; All tracks manual
  ticks.
- Performance check in the sandbox against §12 (first rows < 300 ms, expand
  200 < 50 ms, about 40 realized containers); record numbers here.

### Phase 4 — Revit adapter T0–T2 (Opus)

Owner: `source/RevitDevTool.Tools/ModelsContext/`, pane registration in
`RevitDevTool.Core` / composition.

- `DocumentRegistry`, `RevitModelSource` (chunked collection through
  `RaiseAsync`, link view collector), `RevitModelEvents` (subscribe once,
  defer all work out of `SelectionChanged`), `RevitSelectionPort`
  (`SetReferences` for links), `RevitViewActions` (T0–T2, Temporary View
  Properties, Keep, Reset, Zoom via `ZoomAndCenterRectangle`, Section box),
  `RevitFilterEvaluator`, `RevitFilterCatalog` (rule reader with an
  unsupported-rule row), `REVIT2024_OR_GREATER` hints.
- Snapshot export script for MCP `execute_csharp_code`, so real models replay
  in the sandbox.
- Proof: build for 2023, 2025 and 2027 configs; host tests (MTP via
  RevitDevTool.TestAdapter) for snapshot counts, filter compile and T0/T2
  plans on a sample model with two link instances.

### Phase 5 — Spike and T3 (Opus, live host)

- Run the 0062 Follow-Up spike on Revit 2023 and 2026; step 1 decides T3.
- Go: `PostCommandQueue` (single flight, `Idling` state machine, selection
  snapshot and restore) and journal wiring. No-go: disable link-row
  visibility with the reason, per 0062 §9.
- Record each spike answer in this plan and amend 0062.

### Phase 6 — Docs and acceptance

- `docs/product/model-context.md`, `docs/architecture/ModelsContext/README.md`,
  test-matrix rows for the new suites; set 0062 to Accepted.

## Risks And Recovery

- T3 commands cannot be posted: the no-go path is already specified; nothing
  in Phases 1–4 depends on T3.
- Flat list performance misses §12: measure in the sandbox in Phase 3 before
  the adapter; fall back to paging more aggressively, not to `TreeView`.
- Link view collector ignores temporary hide: rows the panel hid stay in the
  tree from the journal (0062 §7); no design change.
- A port turns out to need a host object: stop and amend this plan; do not let
  a host type into the core.
- Recovery: each phase lands as its own commits; a failed phase is reverted
  without touching earlier phases. The pane is not registered until Phase 4,
  so shipped add-ins are unaffected before then.

## Progress

- [x] 0062 amended: Current model vocabulary, Tree + Filter tabs, context menu,
  three-layer architecture.
- [x] `ElementSearcher` groups by link instance (pre-existing bug).
- [ ] Phase 0 — scaffolding and CI.
- [ ] Phase 1 — core model, engine, fake model, tests.
- [ ] Phase 2 — view models and tests.
- [ ] Phase 3 — controls, views, UI tests, sandbox, performance numbers.
- [ ] Phase 4 — Revit adapter T0–T2, host tests, snapshot export.
- [ ] Phase 5 — spike; T3 queue or no-go path.
- [ ] Phase 6 — product and architecture docs; 0062 Accepted.

## Decisions

- 2026-11-13: Core is `DevTools.ModelsContext` (shared, no host API) so other
  hosts with attached models can reuse it; views are a separate project so
  core tests do not load WPF.
- 2026-11-13: Revit 2023+, assuming the latest update; two link hints need
  2024.

## Validation

- Focused proof: `dotnet test` on `DevTools.ModelsContext.Tests` and
  `DevTools.ModelsContext.Views.Tests` (all TFMs).
- Integration proof: sandbox performance numbers against 0062 §12; host tests
  on Revit 2023 and 2026; spike log.
- Repository-required checks: build skill per touched project,
  `pwsh -File scripts/format-lint.ps1` once per phase, CI `result` green.

## Result

Complete after implementation.

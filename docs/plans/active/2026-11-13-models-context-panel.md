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
- Views in `source/DevTools.Presentation/Views/ModelsContext/` (XAML; moved
  there in Phase 3d from a separate `DevTools.ModelsContext.Views` project,
  which the add-in ILRepack would have merged and broken its self pack URIs).
- `DevTools.UI` controls: flat tree list, summary `CheckComboBox` template,
  icon geometries; a private `CheckComboBox` fork in
  `DevTools.UI/Controls/CheckComboBox` (`libs/HandyControl` is never edited).
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
  - `tests/DevTools.ModelsContext.Tests`: `MSTest.Sdk`, same TFMs (copy
    `DevTools.HandyControl.Tests` settings), with one guard test
    `CoreReferencesNoHostOrWpf` so the project is never empty.
  - `tests/DevTools.ModelsContext.Tests.Shared`: library.
  - `.Views.Tests` (adds `FlaUI.UIA3`) and the `Sandbox` WPF exe
    (`net10.0-windows`) are created in Phase 3 with their first content; an
    empty MTP test project exits with code 8.
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

Owner: `source/DevTools.UI/Controls/FlatTreeList*`,
`source/DevTools.UI/Controls/CheckComboBox/` (private fork; `libs/HandyControl`
untouched), `source/DevTools.Presentation/Views/ModelsContext`,
`tests/DevTools.ModelsContext.Views.Tests`, `tests/DevTools.ModelsContext.Sandbox`.

- Phase 3a (shared controls) landed first: `SummaryCheckComboBox` fork with
  "All" sync and disabled-skip, `FlatTreeList` styles (28 px, recycling),
  panel icons; 6 FlaUI tests in `DevTools.HandyControl.Tests`.

- Views follow 0062 §2, §4, §5, §7, §12 and the canvas; every interactive
  element has an `AutomationId`.
- Sandbox: opens the pane on the fake model; switches generated / replayed
  model, light / dark theme, and a "slow host" delay.
- Expected UI tests (FlaUI by AutomationId, UIA patterns, no mouse events):
  tabs are Tree and Filter; group header has no controls; eye toggles row;
  context menu lists actions; documents box shows one line; All tracks manual
  ticks.
- Performance check in the sandbox against §12, at 100k with three links and
  at 1M across five documents (first rows, expand, longest UI stall, memory
  per million); record numbers here.
- Lifecycle in the core with fakes (0062 §11 Lifecycle): sessions per Current
  model, generation numbers that drop stale results, view switch, model
  switch, closing, link unload/reload, busy host. Expected tests: stale result
  is dropped; closing drops session; model switch parks session; busy host
  disables actions; same link version keeps snapshot.

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
- Version rule (0062 §11 Invalidation): stamp snapshots with
  `GetDocumentVersion`; on link reload keep on same `VersionGUID`, apply
  `GetChangedElements(oldGuid)` on a different one, rebuild when unknown or
  large. Quick per-category `GetElementCount()` for first paint.
- Lifecycle wiring: `ViewActivated`, active model switch, `DocumentClosing`,
  link status, busy detection; every call wrapped, failures become a status
  line.
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
- [x] Phase 0 — scaffolding and CI. Core, Views, Tests.Shared, Tests in the
  slnx; `addin` product paths regenerated (`dotnet run --project build -- paths`).
  Proof: new projects build on all TFMs; Tools builds for 2023 and 2025;
  `DevTools.ModelsContext.Tests` 1/1 on net10.0-windows and net48;
  `Build.Tests` 119/119.
- [x] Phase 1 — core model, engine, fake model, tests. Proof: core builds on
  all TFMs with 0 warnings; `DevTools.ModelsContext.Tests` 34/34 on net48,
  net8.0-windows and net10.0-windows; Tools builds for 2025.
- [x] Phase 2 — view models and tests. Proof: `DevTools.ModelsContext.Tests`
  74/74 on net48, net8.0-windows and net10.0-windows; Tools builds for 2025.
- [x] Phase 3a — shared controls (`SummaryCheckComboBox` fork, `FlatTreeList`,
  icons). Proof: `DevTools.HandyControl.Tests` 19/19 on all three TFMs.
- [x] Phase 3b — lifecycle, version rule, quick counts, port gaps. Proof:
  `DevTools.ModelsContext.Tests` 94/94 on all three TFMs; 1M elements across 5
  documents: generate 78 ms, build 181 ms, aggregate 91 ms, snapshot 57.7 MB
  per million (Debug, net10.0-windows).
- [x] Phase 3c — views, Views.Tests, sandbox. Proof: Views builds on all TFMs
  with 0 warnings; `DevTools.ModelsContext.Views.Tests` 7/7 (net10, net48);
  `Build.Tests` 119/119. Sandbox `--measure` (Debug, no host delay):

  | Check | 100k, 3 links | 1M, 5 docs | Target |
  |---|---|---|---|
  | First rows | 78 ms (185 rendered) | 19 ms (142 rendered) | pass |
  | Expand 200-instance page | 68 ms | 73–83 ms | miss (< 50) |
  | Longest UI stall | 47 ms | 36 ms | pass, borderline |
  | Realized containers | 29 | 29 | pass |
  | Snapshot memory per million | 75.7 MB | 70.8 MB | miss (< 64) |

- [x] Phase 3d — done. Views moved to `DevTools.Presentation/Views/ModelsContext`
  (Views project deleted; guard `PresentationXamlUsesNoSelfUris`); stock
  HandyControl button styles, `hc:IconElement` and brushes only; one shared
  `ColorPicker` per panel, no dialog window. Sandbox `--measure` (Release):
  expand 200 instances 13–43 ms at 100k and 9–34 ms at 1M (target < 50);
  snapshot 37.0 MB per million by array sizes and by GC heap (`RowIdIndex`
  replaces the id dictionary); first-load stall still 33–59 ms. Proof:
  `DevTools.ModelsContext.Tests` 105/105 (net48, net8, net10),
  `Views.Tests` 10/10 (net48, net10), `DevTools.HandyControl.Tests` 19/19,
  `Build.Tests` 119/119, merged `RevitDevTool` host builds for 2025 with
  ILRepack. Port changes for Phase 4: `IModelSource.HostName`,
  `IModelEvents.ParameterDefinitionsChanged`,
  `IViewActions.GetIdsHiddenPermanentlyOrTemporarilyAsync`.
- Original Phase 3d list: close the two misses and the view gaps: expand without a
  full-list reset or thread hop; snapshot memory under 64 MB per million (the
  core test reports 57.7 MB from array sizes, the sandbox 70–76 MB from the
  heap, so measure the same way first); a row Color command; precomputed
  `Indent` and `CountText` on rows; hover highlight of affected rows; bind the
  parameter catalog, busy text and pane visibility from Phase 3b; a tree menu
  UI test through UIA; stronger stale-result test. Also move the views into
  `DevTools.Presentation/Views/ModelsContext`, delete the separate Views
  project, use no self `;component/` URI (x:Class resource dictionary), keep
  icons in `DevTools.UI` `Icons.xaml`, and add a guard test for other
  `;component/` URIs; prove with a build of the merged `RevitDevTool` host.
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
- 2026-11-13 (Phase 1): groups are `long GroupId` everywhere; All models rows
  carry the Current model `DocumentKey`; a type that changes group or family
  marks the snapshot dirty instead of moving counts; the planner returns no
  partial plans (any disabled target disables the plan with its reason);
  `SelectionGuard` records expire after 2 s on `IClock`; rule values are text
  and the adapter parses units; `SnapshotStore` shares one snapshot per
  `SourceId`.
- 2026-11-13 (Phase 2): default scope Active view, Sync on; an empty custom
  filter matches nothing and Live sends nothing; the panel's eye record clears
  on Keep and Reset; turning an eye back on clears Isolate; Duplicate drops
  unsupported rules and names the copy "X (2)"; Tree → Revit selects instances
  inside the active scope; a link model row selects the link instance.
- 2026-11-13 (Phase 2 gaps, fixed in Phase 3b): `UniqueId` for the journal
  comes from the adapter at action time; per-element visibility read-back
  through `IViewActions` for Current model eyes; a parameter catalog port for
  rule parameters; a pane-visibility hook that pauses and resyncs; stable row
  keys (document, level, group/family/type ids) so open branches survive a
  rebuild.
- 2026-11-14 (Phase 3b): an unloaded link stays ticked with rows from the kept
  snapshot and "not loaded" as the reason; quick counts are skipped in
  Selection scope; the version check compares `VersionGUID` only; "too large"
  means changed ids over 20% of the snapshot; changes while the pane is hidden
  mark the snapshot dirty; a model switch keeps the selected filter but drops
  its unsaved edits.
- Open after Phase 3b, owned by Phase 4: the busy text comes from the adapter
  (host name), not the core; an `IModelEvents` signal for parameter
  definitions added or deleted refreshes the catalog; `GetHiddenIdsAsync`
  returns ids hidden in the active view by permanent hide, temporary hide or
  isolate, and the adapter documents which; the "stale result is dropped" test
  is strengthened to fail without generation numbers.

## Validation

- Focused proof: `dotnet test` on `DevTools.ModelsContext.Tests` and
  `DevTools.ModelsContext.Views.Tests` (all TFMs).
- Integration proof: sandbox performance numbers against 0062 §12; host tests
  on Revit 2023 and 2026; spike log.
- Repository-required checks: build skill per touched project,
  `pwsh -File scripts/format-lint.ps1` once per phase, CI `result` green.

## Result

Complete after implementation.

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
- `DevTools.UI` controls: flat tree list, `TagCheckComboBox` (the documents
  box) and icon geometries (`libs/HandyControl` is never edited).
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
  "200 of N · more", search on row text, Selection sync with 50 / 5,000 thresholds,
  status line), `FilterTab` (source list, custom filters with naming rules,
  sets, Live with baseline for Add/Remove, pause above 5,000), `TemporaryBar`
  (N changes, Keep confirm when T3 is pending, Reset), `ContextMenu` (items,
  hidden Select while syncing, disabled reasons, shortcuts).
- Expected tests: tree expand inserts children; Selection sync opens path under 50;
  Selection sync shows badges above 50; Live pauses above 5,000; name must be unique;
  empty name blocks save; Keep asks when link changes pending; Select hidden
  while Selection sync on.

### Phase 3 — Controls, views and sandbox (Sonnet; flat list by Opus)

Owner: `source/DevTools.UI/Controls/FlatTreeList*`,
`source/DevTools.Presentation/Views/ModelsContext`,
`tests/DevTools.ModelsContext.Views.Tests`, `tests/DevTools.ModelsContext.Sandbox`.
`libs/HandyControl` stays untouched.

- Phase 3a (shared controls) landed first: `FlatTreeList` styles (28 px,
  recycling) and panel icons, with FlaUI tests in `DevTools.HandyControl.Tests`.
  The documents box is the `DevTools.UI` `TagCheckComboBox` (Phase 5b); the
  view keeps "All" off unloaded links and at least one ticked entry.

- Views follow 0062 §2, §4, §5, §7, §12 and the canvas; every interactive
  element has an `AutomationId`.
- Sandbox: opens the pane on the fake model; switches generated / replayed
  model, light / dark theme, and a "slow host" delay.
- Expected UI tests (FlaUI by AutomationId, UIA patterns, no mouse events):
  the view switch shows Explorer and Filter; group header has no controls; eye toggles row;
  context menu lists actions; ticks show as tags; All skips unloaded links;
  the last tag cannot close.
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
- Go: `LinkedHideShow` (one synchronous call per request, selection snapshot
  and restore, 0062 §10) and journal wiring. No-go: disable link-row
  visibility with the reason, per 0062 §9.
- Record each spike answer in this plan and amend 0062.

Spike log, Revit 2026, 2026-11-15 (Snowdon Towers HVAC, six links, run on a
duplicate of `{3D}` that was deleted afterwards):

| Step | Result |
|------|--------|
| `LookupCommandId` for `ID_TEMPHIDE_HIDE/ISOLATE/HIDECAT/ISOLATECAT/RESET`, `ID_VIEW_UNHIDE_ELEMENTS`, `ID_VIEW_OVERRIDE_ELEMENTS_HALFTONE` | Ids resolve; `CanPostCommand` false with and without link references selected; `PostCommand` throws `ArgumentException` (only `PostableCommand` or external commands) |
| `SetReferences` (5 link refs) + `PostableCommand.HideElements` | Hidden in that view only (link view collector 1466 → 1461; source view still 1466); selection cleared; undo entry "Hide/Isolate" |
| Same with 1,969 refs from two links + 3 Current model ducts | One command, one undo entry; Structural 1466 → 0, Architectural 500 refs hid 548; ducts hidden; `SetReferences` 101 ms |
| Temporary View Properties on, Hide, off | Hide survives the exit (no-go as a restore path) |
| Reveal Hidden on + `PostCommand(ID_VIEW_UNHIDE_ELEMENTS)` | Same `ArgumentException` |
| Reveal Hidden on + `invokeCommandHandler("ID_VIEW_UNHIDE_ELEMENTS")` | Unhides the 5; back to 1466 after Reveal off; undo entry "Hide/Isolate" |
| Same with Reveal Hidden off | No effect |
| `invokeCommandHandler("ID_TEMPHIDE_HIDE"/"ID_TEMPHIDE_ISOLATE")` with link refs | No effect; selection kept |
| `invokeCommandHandler("ID_TEMPHIDE_HIDE")` with 3 Current model ducts | Temporarily hidden (control) |
| Link view collector | 0–8 ms per link; drops Hide in View elements |

Decision (user, 2026-11-15): T3 = Hide in View for link rows, Show through
Reveal Hidden + `invokeCommandHandler`, journal-scoped Reset; Isolate,
Halftone and Color on link rows disabled. 0062 §5, §7–§10, §13 amended.
Remaining spike steps are in 0062 Follow-Up.

Implementation, 2026-11-15: core (planner reasons, journal of hidden link
elements, Show/Reset/Keep per 0062 §8) and adapter.

- First adapter, `PostCommandQueue` (post + `Idling`): worked from MCP, failed
  from the panel. The journal shows `API queue command
  ID_VIEW_FRAME_REVEALHIDDEN cannot be invoked in this context` when the pane
  has focus; Show timed out, and a later run crashed Revit (unhandled CLR
  exception `0xe0434352` in an external event). Dropped.
- Second spike: `View.EnableRevealHiddenMode` in a transaction, then
  `invokeCommandHandler("ID_VIEW_UNHIDE_ELEMENTS")`, then
  `DisableTemporaryViewMode` unhides synchronously in one call;
  `invokeCommandHandler("ID_VIEW_HIDE_ELEMENTS")` hides synchronously; a
  `TransactionGroup` makes one undo entry ("Model Context Show").
- Adapter now `LinkedHideShow`. Live check on Revit 2026 through the panel
  (FlaUI on `Explorer.Row.Eye` and `Temporary.Reset`, Scope = Selection with
  5 linked beams): Hide hides 5/5, Show shows 5/5, Reset shows 5/5; undo has
  one "Model Context Hide" and one "Model Context Show"; the selection is
  kept; Revit stays responsive.
- UIA gaps found while driving the panel: the scope radio buttons in
  `ButtonGroupSolid` expose no Toggle/SelectionItem pattern, and the
  documents `CheckComboBox` items cannot be ticked while the drop-down is
  closed. Scope was set through the view model for this check. Both gaps are
  closed in Phase 5b (`PanelButtonGroup` peer, `TagCheckComboBox` item peers).

Not yet run live: Revit 2023–2025 and 2027.

Third spike, 2026-11-15 (Isolate, Halftone and Color on linked elements,
0062 §5.11): no internal isolate or override path; Revit refuses the temporary
and override commands on link references (`canExecute` false), Halftone runs
with no effect, and a `SelectionFilterElement` misses linked elements. A host
view filter on `IFC_GUID` reaches linked elements but is not a usable key
(case-insensitive rules collide, no link-instance distinction, 7,823 rules
took 305 s to regenerate). Isolate by hiding the rest of the link works
(7,818 hidden in about 730 ms, restore about 5.5 s) and stays a candidate
until the edge cases in 0062 Follow-Up 8 are measured.

### Phase 5b — Panel UI/UX revision (2026-11-15)

- Explorer and Filter switch through an icon `ButtonGroup` next to the scope
  group instead of a `TabControl`.
- Sync becomes an icon toggle named Selection sync (two-way selection between
  tree and Revit), renamed in code too.
- The documents box is a new control in `DevTools.UI`: tags on one line with
  `+N` overflow and a search box in the drop-down.
- Current model is checked by default and can be unchecked like a link.
- Expand all and Collapse all beside the Explorer search, timed at 26k and 50k
  elements.
- The design canvas (Panel and Selection sync artboards) is updated to match.
- Done: the switch row (scope | view) fits 300 px; the documents box has its
  own row below. `PanelButtonGroup` exposes the RadioButtons to UIA (the stock
  `ButtonGroup` item peer hid Toggle/SelectionItem). The Current model could be
  unticked only after another entry was ticked; the box and `SetDocuments`
  favoured it (unticking "All" kept it, and a refresh with no loaded tick, as
  during a link reload, re-ticked it). Now one rule for every entry: the last
  tick stays. Expand all stops at Type rows: in the fake model it takes 2 ms for
  542 rows at 26k and 50k elements; opening instances too took 152 ms (23,470
  rows) at 26k and 281 ms (38,187 rows) at 50k, plus one Revit call per Type.
  Proof: `DevTools.ModelsContext.Tests` and `DevTools.ModelsContext.Views.Tests`.

### Phase 5c — Filter parameter identity and types (planned 2026-11-15)

Goal: the rule's data type comes from the parameter, never from the user, and
one rule works on host and link elements alike. Inputs: Revit 2026
UIFramework decompile (the Edit Filter dialog is native; the managed side
only asks native for operators, free-text vs dropdown, and (id, name) value
lists per parameter id), and two in-house tools, ParameterMapper and
PenetrationMapper (tags, sampling, name+type keys; neither caches).

1. Spike (live Revit 2026, host + link), before code:
   - Parameter.Id sign and `ParameterBindings` presence for a non-shared
     family parameter on a project instance; whether `ElementParameterFilter`
     with that id filters it.
   - `Definition.GetDataType()` for built-ins read from a sampled element.
   - Cost of sampling one element per family type and per category on the
     HVAC sample and its six links.
   - Result (Revit 2026, HVAC sample and six links): a non-shared family
     parameter has a positive id, a `ParameterElement` with an
     `InternalDefinition` in that document, and no entry in
     `ParameterBindings` (6 to 234 per document). It is not in
     `GetFilterableParametersInCommon`, yet `ElementParameterFilter` filters
     on it natively (has value 37/37 host, 105/142 Architectural; `>=` matched
     the in-memory count 105/105). So family parameters stay native; no
     in-memory path is needed for them. Sampled built-ins give a data type for
     270 of 348 (host). One collector pass per document took 11 to 272 ms
     plus 4 to 57 ms to read the samples' parameters.
2. Identity. `ParameterKey` becomes one match key per kind:
   - Built-in: `BuiltInParameter` name. Shared: GUID.
   - Project and Family (non-shared): name plus data type (`ForgeTypeId`
     string). Same name with another data type is another key.
   - Origin (Built-in, Shared, Project, Family) and binding (Instance, Type)
     are tags on the merged item, not part of the key. A Family parameter in
     one document and a Project parameter in another, same name and data
     type, are one item with both tags (decided 2026-11-15).
3. Catalog per document, cached:
   - Enumerate with `GetFilterableParametersInCommon` per category, then add
     family parameters and data types of built-ins from sampled elements (one
     per family type, one per category without families).
   - `ParameterInfo`: key, display name, origin, binding, storage type, spec,
     kind, per document.
   - Cache per `SourceId` and category; drop a document's entries on its
     `DocumentChanged` (families, parameter elements, bindings) and on link
     reload. The panel merges cached per-document lists by key and counts how
     many ticked documents have each key (coverage).
4. Kinds: Text, Integer, Number (with spec), YesNo, Reference (ElementId),
   Enumerated (integer with named values). Derived, read-only; the Kind combo and
   `FilterRule.Kind` go (stored filters drop the field).
5. Operators and editors per kind: Text all; Number and Integer compare;
   YesNo, Reference, Enumerated equal and not equal; every kind has has/no value.
   YesNo is a Yes/No dropdown; Reference and Enumerated pick from the value index
   by name; Number shows a unit hint and parses with host units once.
6. Parameter picker: HC `AutoCompleteTextBox`, each item the name plus HC
   stock labels for origin, binding, data type and coverage (as in the two
   in-house tools).
7. Compile per document: resolve the key in the target document to the real
   parameter (id, storage, spec) and build the rule from that, not from a
   stored kind. A key the document lacks, or a type that does not fit, gives
   no match there. Family parameters compile to native rules like project
   ones (spike 1).
8. Value index cached per (document, key, categories) with the document
   version; Reference names resolve in the owning document.
8b. Case-sensitive text rules (Revit string rules are always
   case-insensitive). An `Aa` toggle per text rule, off by default:
   - Native pass with a widened tree: a positive leaf (equals, contains,
     begins, ends) keeps its case-insensitive rule, which matches a superset;
     a negative leaf (not equals, not contains...) becomes "has a value" in a
     positive position. A leaf under an inverted set flips: it takes the
     case-insensitive rule, which matches a subset there. The result is a
     superset of the exact answer.
   - Exact pass in memory on that superset only: read the value (instance,
     then type) and test the whole tree with ordinal comparison.
   - When the value index for that key is cached, equals and not equals come
     from it directly (value to element ids), with no Revit call.
9. Tests: fakes and sandbox follow the new model; tests per kind, operator
   set, same name with two data types, family plus project with one name,
   host versus link units. Live FlaUI check on Revit 2026 host and link.
10. ADR 0062 Filter section rewritten.

### Phase 5d — Filter logic like Revizto (planned 2026-11-16)

Goal: the rule builder reads like Revizto Find Objects, drops the NOT toggle,
and can say "(in Arch.rvt, Walls: …) OR (in MEP.rvt, Ducts: …)". Reference:
design canvas Flow 8 (Today screenshot, After mockups in HC Light sizes,
examples 1–4, scope picker). ADR 0062 §5 holds the decisions.

1. Model and store (clean cut):
   - `FilterSet` loses `Inverted` and gains an optional scope: document
     sources (`SourceId`, by file) and category ids; none = same as parent.
   - Operators renamed: Missing → Undefined, Exists → Defined, HasNoValue →
     Empty, HasValue → NotEmpty (labels: undefined, defined, empty, not empty).
   - `CustomFilterStore` drops `not`, writes `scope`, and the new operator names.
2. Engine:
   - Per document, a group whose document scope leaves that document out is
     false and is pruned before compiling; an unresolved group is false.
   - A group's category scope compiles to `ElementMulticategoryFilter` ANDed
     with its children. The fake matcher does the same over snapshots.
   - `CaseSensitiveRules` and `FilterCompiler` lose the inverted-set paths.
   - The Revit filter reader maps an inverted parameter filter to negated
     operators with the match swapped, else an unsupported rule.
3. View models:
   - Group: `Match`, a `ToggleMatch` command, scope lists limited to the parent,
     warning for values outside the parent, unresolved state, `IsExpanded`.
   - Each child knows whether it shows the connector (all but the first) and its
     text (the parent's match).
   - The parameter list, coverage and value counts of a condition follow its
     group's effective scope.
   - `MoveTo(item, group, index)` with a guard against moving a group into
     itself or its children; Move up / Move down use it.
4. Views (HC stock styles only; shapes as on Flow 8):
   - AND/OR `ButtonDefault.Small` between children; group header with grip,
     "Group", collapse toggle, remove; scope row with two `TagCheckComboBox`es;
     condition card on `RegionBrush` with the grip on its left edge, same two
     rows as today; + Condition (`AddGeometry`) and + Group (`Stack`).
   - Grip geometry (Fluent reorder dots) in `Icons.xaml`; drag behavior in
     `DevTools.UI` (WPF DragDrop plus an insertion adorner in `PrimaryBrush`),
     no new package; Alt+Up / Alt+Down key bindings.
   - AutomationIds: `Filter.Set.Connector`, `Filter.Set.Scope.Documents`,
     `Filter.Set.Scope.Categories`, `Filter.Set.Collapse`, `Filter.Item.Grip`,
     `Filter.Set.Warning`; `Filter.Set.Not` goes.
5. Tests:
   - Core: connector toggle, scope narrowing and pruning per document, picker
     per group scope, move guard, store round trip without `not`, operator
     names, examples 1–4 from Flow 8 as filter trees on the fakes.
   - Views (UIA, AutomationIds): connector click switches the group, scope
     combos list only the parent's items, Alt+Up/Down reorders, no NOT control.
   - Sandbox shots for examples 2 and 4 and the open scope picker, Light and
     Dark; compared side by side with the Flow 8 mockups (FlaUI capture), and
     put on the canvas next to them.
6. Docs: product page and `docs/architecture/ModelsContext/filters.md` follow.
7. Architecture and performance (review of HEAD f61733be, 2026-11-16):
   - Before the views: split the filter view models so Phase 5d does not grow
     them: `ParameterScopes` (core, memoised per scope), `CategoryPickerViewModel`,
     `FilterLibraryViewModel` with a pure `CustomFilterLibrary`, `FilterRunner`
     (evaluate, Live, baseline), one file per rule view model type, and a pure
     `FilterTreeMoves`.
   - Adapter, in parallel: index slices instead of `Skip/Take` (O(n²) on
     net48) with the slice time cap; `EnumValuesOf` through the sliced count;
     resolved parameters and reference targets cached per source and version;
     `ParameterResolver` split with its pure parts in core; a
     `DocumentChangeReader` and a core `ChangeClassifier`.
   - Logging gaps: unresolved rule, compile cost, exact-pass superset vs exact
     count, per-document result counts, Live paused, merge timing, delta batches,
     document-change handler timing, event names in `Post`, per-document catch
     in `Resolve`, file path in store errors.
   - Budgets, measured in the sandbox (`--measure`, filter section, 100k and 1M,
     3k parameters over 5 documents) and live on Revit 2026 with Trace on:

     | Check | Budget |
     |---|---|
     | Group parameter list cold / warm | ≤ 5 ms / cache hit |
     | Scope pruning per document | ≤ 1 ms |
     | Picker keystroke, 3k items | ≤ 16 ms |
     | Compile per document cold / warm | ≤ 50 ms / ≤ 5 ms |
     | One Revit run | < 250 ms, target 30 ms per slice |
     | Live edit to selection sent | ≤ 500 ms (300 ms debounce) |
     | UI stall | < 50 ms |

   - Later, off the Phase 5d path: `ModelContextViewModel` (event inbox,
     context loader) and `ExplorerTabViewModel` (five parts) splits.

### Phase 6 — Docs and acceptance

- Product and architecture pages are done (Step 10). Test matrix with golden
  counts and the live suites (adapter inside Revit, FlaUI panel flows):
  [test-matrix.md](../../architecture/ModelsContext/test-matrix.md). Left: record
  the pending golden values from the first live run; set 0062 to Accepted.

## Risks And Recovery

- T3 Show depends on an internal Revit call: when it is missing or has no
  effect, link rows keep Hide and disable Show (0062 §9); nothing in Phases
  1–4 depends on T3.
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
- [x] Phase 3a — shared controls (`FlatTreeList`, icons; the documents box
  later moved to the stock HandyControl `CheckComboBox`). Proof: `DevTools.HandyControl.Tests` 19/19 on all three TFMs.
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
- [x] Phase 4 — Revit adapter T0–T2, pane, ribbon command, snapshot export
  script (not yet run). Not run in a live Revit. Proof: Tools builds 0
  warnings for 2022 (skips ModelsContext), 2023, 2025, 2027; merged host builds
  for 2025 with ILRepack; core 105/105; `Build.Tests` 119/119. No host test
  project (no repo pattern for Tools host tests).
- [x] Phase 4b — done: `IHostFilterCatalog.EvaluateAsync` runs Revit filters
  in their own document (`GetElementFilter()` + categories, selection filters
  by stored ids); store errors reach the status line; read-back asks only
  panel-touched ids and visible rows, cached per (model, view); ribbon icon
  PNGs rendered from the panel geometry. Proof: core 109/109 (net10, net48),
  Views.Tests 10/10, Tools 0 warnings for 2023/2025/2027, merged host builds
  for 2025, sandbox builds, `Build.Tests` 119/119. Limit: expanding a header
  asks once for all in-scope ids of that model.
- Original Phase 4b list: fixes before live run: Revit rule filters must run through
  `GetElementFilter()` in their own document (a port method to evaluate a host
  filter by id) instead of the read-back definition; custom filter save
  errors reach the status line; hidden-id read-back must not walk every
  element (panel-touched ids plus visible branches, cached per view, dropped by
  `DocumentChanged`); a dedicated ribbon icon.
- [ ] Phase 5c — parameter identity and types.
  - [x] Step 1 spike (recorded above).
  - [x] Steps 2–5, 7, 8, 8b: match keys, merged catalog with origin, binding
    and coverage tags, derived kinds and operator sets, adapter catalog and
    value caches, per-document compile (host units, Yes/No, Enum map,
    family parameters native), inverted sets, case-sensitive two-pass
    evaluation. Proof: `DevTools.ModelsContext.Tests` 199/199 and
    `Views.Tests` 40/40 (net10.0-windows); Tools builds for 2023 and 2026;
    `build-host.ps1 -HostApp Revit -Year 2026` builds and deploys. Not run in
    a live Revit yet.
  - [x] Step 6: the picker is `DevTools.UI` `AutoCompleteComboBox` (own view
    per control, max suggestions, error template hidden while open), items
    with HC label tags; per-kind editors, `Aa` and NOT toggles.
  - [x] Step 9 live FlaUI check (Revit 2026, HVAC sample, Architectural link,
    Doors 142): Swing Angle (family parameter, two ids) has value 131,
    missing 11, > 45° 130, = 90° 130, != 90° 1, <= 45° 1; Mark = "s10" 1
    with `Aa` off and 0 with it on, = "S10" 1, != "S10" 141, != "s10" 142;
    all match the API counts. Two fixes came from it: family parameter
    elements are not returned by `OfClass(ParameterElement)`, so a name plus
    data type key maps to the ids found on sampled elements; and a native
    not-equals rule passes elements without the parameter, so each per-id
    rule is ANDed with "has the parameter".
  - [x] Picker by category: catalog items carry their category ids; the
    picker lists the union for the checked categories (all when none), and a
    rule keeps a parameter that drops out. Proof: `DevTools.ModelsContext.Tests`
    and `Views.Tests` pass (net10.0-windows); not run in a live Revit yet.
  - [x] Step 10 docs: `docs/product/models-context.md` and
    `docs/architecture/ModelsContext/` (README with Gotchas, snapshots, filters,
    parameter-catalog, ui); linked from `ARCHITECTURE.md`, `agents/index.md`,
    `product/README.md` and 0062.
  - [x] Phase 5d live fixes (2026-11-17, Revit 2026): type parameters decide
    *defined*, *empty* and *not empty* on the element types
    ([filters.md](../../architecture/ModelsContext/filters.md#type-parameters));
    sampled built-ins listed; candidate ids cached; slices paced to 30 ms;
    Walls count 1,120 (curtain panels moved to their own category); item UIA
    names; one document label in log lines. Panel and API agree: E1 207 (API
    215 counts 8 walls in a secondary design option), E4 301, Reference Level =
    L3 319, Fire Rating on walls and doors and Family Name on ducts for all
    eight operators.
  - [x] Flow 9 picker rows (2026-11-17): Instance / Type sections with sticky
    headers, source icons, category summary, structured tooltip, no pills and
    no coverage warning; typed numbers in the Current model's units, picked
    numbers keep their stored range (L2 LU-01, PS-01, PS-02). Shutdown no longer
    touches the pane's visual tree and temporary bar commands run through
    `PanelRuntime.RunAsync` (README Gotchas 17, 18).
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

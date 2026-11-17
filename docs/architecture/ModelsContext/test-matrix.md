# Model Context Test Matrix

Every Model Context use case with one fixed expected outcome and the test that
checks it. The live model is the Revit sample **Snowdon Towers Sample HVAC.rvt**
(the Current model) with its **Snowdon Towers Sample Architectural** link loaded,
so every count is deterministic.

Behavior: [product/models-context.md](../../product/models-context.md).
Repo-wide test rules: [agents/test-matrix.md](../../agents/test-matrix.md).

## Layers

```text
L1 core + views, fakes      tests/DevTools.ModelsContext.Tests, .Views.Tests     CI, seconds
L2 adapter inside Revit     tests/RevitDevTool.ModelsContext.LiveTests           live Revit, no UI
L3 panel inside Revit       tests/RevitDevTool.ModelsContext.UiTests             live Revit, FlaUI
```

| Layer | Carries | Why here |
|-------|---------|----------|
| L1 | View model logic, debounce, Live and pause, library (save, rename, switch), scope pruning, tree moves, planner tiers, picker filtering, AutomationIds | Pure logic on a seeded fake model; runs in CI on every change |
| L2 | Every count in this matrix, catalog facts, host filter read-back, edits and cache refresh | The Revit API is the thing under test, not the UI. Calls go straight to `RevitFilterEvaluator`, `RevitModelSource`, `RevitFilterCatalog` and `ParameterCatalogReader` inside Revit, with no waits |
| L3 | A few end-to-end flows: build a rule, Aa, AND to OR, save and reload, Live pause, link hide and Reset, linked element hide (T3) and Reset | Proves the wiring from UIA input to count. Each flow repeats a golden L2 count, so a UI failure is never a counting question. T3 runs only here, through the panel's own click path |

L2 and L3 projects do not end in `.Tests`, so `ProjectGraph.TestProjects` leaves
them out of CI and `tests/Directory.Build.props` does not compile
`TestDataDirectory.cs` into them (that module initializer would change the data
folder of the running Revit). Both are `Build="false"` in `RevitDevTool.slnx`.

### Frameworks

L2 runs on NUnit; L3 and the data rule tests run on MSTest. L2 stays on NUnit
because the RevitDevTool.TestAdapter MSTest runtime runs test bodies on a
thread-pool thread while the Revit thread waits in
`MSTestRuntimeSession.Execute`: `UIDocument.ActiveView` comes back null and any
`ExternalEvent` dispatch deadlocks (probe: `IsRevitInApiMode` false, dispatch
timed out after 20 s). NUnit runs the body on the Revit API thread. Both list
each data row by its case id (`CountMatchesOracle(TX-01)`).

### Case data, oracle and golden values

- **All case data is JSON** in `tests/RevitDevTool.ModelsContext.LiveTests/Data/`,
  copied to the output; code holds only the flow. L3 links the same folder and
  reads the same cases. Every case has `id` (the matrix row), `description`,
  `expected`, `manualSteps` (exact Revit steps anyone can follow) and, for UI and
  visibility cases, `images` (the image slots the run must capture).

  | File | Holds |
  |------|-------|
  | `sample-facts.json` | Model and link titles, link label, view names (`L3`, `{3D}`), category display names, parameter aliases (built-in name, family name + spec, or "first shared in") |
  | `scopes.json`, `filters.json` | Count cases: `docs` (Host, Arch, Both), `scope`, optional `hostLengthUnit` (set in a rolled-back transaction), `filter` (`categories`, `match`, `rules` with nested groups and group `scope`; a rule with `picked` takes the stored range of its text from the panel's value list, and the oracle matches the elements whose own display text is that text) |
  | `explorer.json` | Explorer checks by `check`: CategoryCount, SnapshotPerCategory, QuickCounts, ViewCounts, ViewIds, SecondaryOptions, DocumentList |
  | `catalog.json` | Picker facts by `check`: Item, Models, ValueCount, SampledBuiltIns, BuildTime |
  | `host-filters.json`, `edits.json` | A shared `setup` (parameters, values) and the cases of the Revit filter and model edit tests |
  | `visibility.json` | Visibility cases: `tier`, `source` (ExplorerRow, FilterResult), `action`, `color`, `target`, `other`, expected API state or `plannedTiers` |
  | `ui-flows.json` | L3 flows: `test`, the filter cases they reuse (`cases`), images |
  | `probes.json` | Value spreads `WriteSampleProbe` writes |
- **Oracle**: `Oracle.cs` counts each case again with plain
  `FilteredElementCollector` and `Parameter` reads, following the product contract
  (instance value first, then the type; *defined* means the element or its type has
  the parameter; text empty is null or `""`). It never calls the panel's compiler.
- `CountMatchesOracle(<id>)` asserts oracle = golden (the model still gives the
  value), then panel = oracle. When only the second fails the panel has a bug;
  the golden value stays.
- `WriteMismatchedElements` writes `mismatched-elements.md` (elements only one
  side matches, with instance and type values); `WriteSampleProbe` writes
  `sample-probe.json` (levels, phases, design options, shared parameters, value
  spreads). Both go to the run folder.
- Design options: Whole model counts main + primary options; Active view is
  exactly the view collector (any option), as the panel does.
- The active-view rows switch to `L3` when needed. A view not yet open in the
  session stops them as Inconclusive (Revit draws it only after the run
  returns); an already-open view is switched to and counted.

### Evidence per run

Each run writes `artifacts/models-context-tests/<yyyyMMdd-HHmmss>/` (ignored by
git). L2 opens a new folder; L3 reuses the folder of the latest run until it has
its own results, so running L2 then L3 gives one folder:

| File | Holds |
|------|-------|
| `report.md` | Summary per layer with wall time, then per case: what it checks, steps done, expected, oracle, panel, timings, images, PASS/FAIL, *How to verify by hand* |
| `results.json`, `results-L2.json`, `results-L3.json` | The same data, diffable |
| `images/` | L2: the active view rendered with `Document.ExportImage` before, after and after reset of each visibility case. L3: the panel (UIA capture) and the Revit window, per image slot |
| `mismatched-elements.md`, `sample-probe.json` | Diagnostics |

The assertion (oracle or Revit API state) is the pass/fail source; images are
evidence.

### Rules for adding a case

1. Put all data in `Data/*.json`; the test code holds the flow only.
2. Take the expected value from the oracle (`results.json` of a run), never
   from the panel. A row where oracle = golden and the panel differs is a panel
   bug: list it under Mismatches, do not change the golden value.
3. Write `manualSteps` a person can follow in Revit without the panel's code.
4. Give every UI and visibility case `images` with at least `before` and
   `after`; the run fails a case whose slots were not captured.
5. L3 waits only on the readiness signal (`ItemStatus` Idle/Busy); no sleeps,
   no keys, no mouse, UIA patterns by AutomationId only. Never poll Revit's
   UIA tree while it is busy.
6. The model ends unmodified: edits run in rolled-back transactions and
   temporary modes are reset through the panel's own Reset; a test never
   repairs what the panel left behind.
7. Never run T3 (Hide in View for link elements) from an L2 test; declare
   `expected.plannedTiers` instead (Gotcha 13 in [README.md](README.md#gotchas)).
   Never build a link view collector for a view other than the active one.
   L3 attaches to the Revit that ran L2 of the same run folder (Gotcha 14).
8. `CaseRulesTests` (in the L3 project, runs without Revit) fails a case
   without description, expected or manual steps, a duplicate id, a UI or
   visibility case without `before`/`after` slots, a T3 case without
   `plannedTiers`, and a flow naming an unknown filter case.

### Readiness signal

The panel root `ModelContext.Panel` exposes UIA `ItemStatus` = `Idle <n>` or
`Busy <n>`. Busy means a panel job runs (`PanelRuntime.RunAsync`) or a debouncer
waits (the runtime wraps the scheduler and counts pending timers). `<n>` grows by
one each time a job ends. An L3 action is:

```text
settle: wait for Idle, read n
act:    one UIA pattern call (Invoke, Toggle, Value, SelectionItem)
wait:   Idle and generation > n
read:   Filter.ResultCount
```

No sleeps and no re-measuring. An action that starts no job (expanding the
categories box, typing the filter name, Save) waits on its own UIA state instead.

## Run

```powershell
# L1
dotnet run --project tests/DevTools.ModelsContext.Tests/DevTools.ModelsContext.Tests.csproj -f net10.0-windows
dotnet run --project tests/DevTools.ModelsContext.Views.Tests/DevTools.ModelsContext.Views.Tests.csproj -f net10.0-windows
# L2: Revit 2026 with the sample open; --pid picks that Revit when several are open
dotnet test --project tests/RevitDevTool.ModelsContext.LiveTests/RevitDevTool.ModelsContext.LiveTests.csproj -c Debug.Autodesk.2026 -- --pid <revit pid>
# L3 after L2: attaches to the Revit pid L2 recorded in the run folder;
# the deployed add-in must be current (scripts/build-host.ps1 -HostApp Revit -Year 2026)
dotnet run --project tests/RevitDevTool.ModelsContext.UiTests/RevitDevTool.ModelsContext.UiTests.csproj
# Data rules only, no Revit needed
dotnet run --project tests/RevitDevTool.ModelsContext.UiTests/RevitDevTool.ModelsContext.UiTests.csproj -- --filter CaseRulesTests
```

Without `--pid` the adapter reuses the oldest Revit 2026 (or starts one), so pass
it whenever another Revit 2026 is open. L2 writes its Revit pid to
`results-L2.json`; L3 attaches only to that process and stops as Inconclusive
when it does not show the sample.

L2 tests that edit the model (host filters, edits) run in a transaction that is
rolled back. Visibility cases commit temporary modes and reset them. L3 saves
one custom filter named `uia-live …` and deletes it in the same test. Never
save the model.

## Matrix

Expected values come from `Data/*.json` (recorded from the oracle on Revit 2026,
2026-11-17). Change them there, not here alone.

Measured on Revit 2026 (Snowdon HVAC + six links), 2026-11-17, run
`20261117-222012`, L2 then L3 in one Revit session: L2 130 tests in about
2 min 30 s, all PASS once `L3` had been drawn (SC-05 counts 1,669 for oracle
and panel alike while `L3` is open but not yet redrawn after the visibility
cases switch to `{3D}`; a rerun with `L3` active gives 917); L3 7 flows, all
PASS, in 1 min 14 s, plus 6 data rule tests. L1 core 340 tests (net10, net48),
views 102 tests. `L2 matrix` below means
`FilterMatrixTests.CountMatchesOracle(<id>)`.

### Context scope (L2 `FilterMatrixTests`, `ExplorerCountTests`)

| ID | Use case | Docs | Expected | Test |
|----|----------|------|----------|------|
| SC-01 | Whole model, every model category | Current | 2,985 | L2 matrix |
| SC-02 | Whole model, every model category | Arch | 7,746 | L2 matrix, L3 UI-05 |
| SC-03 | Link unticked: Doors in the Current model only | Current | 0 | L2 matrix |
| SC-04 | Link ticked: Doors in both | Both | 142 | L2 matrix |
| SC-05 | Active view, every model category | Current | 917 | L2 matrix |
| SC-06 | Active view through the link | Arch | 780 | L2 matrix |
| SC-07 | Active view, Doors | Both | 24 | L2 matrix |
| SC-08 | Active view, Swing Angle > 45° | Both | 22 | L2 matrix |
| SC-09 | Secondary design options: Whole model only counts main + primary; view and selection follow the collector | Arch | snapshot counts = oracle (main + primary) | L2 `ExplorerMatchesOracle` |
| SC-10 | Documents list: Current model first, then each loaded link | all | keys equal | L2 `ExplorerMatchesOracle` |
| SC-11 | Ticks, last tick, unloaded link | fake | state | L1 `PanelBarsTests`, `PanelViewTests` (`AtLeastOneDocumentStaysTicked`, `AllSkipsUnloadedLink` …) |

### Explorer counts and visibility tiers

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| EX-01 | Doors in the Architectural link | 142 | L2 `ExplorerMatchesOracle` |
| EX-02 | Walls in the Architectural link (main + primary) | 1,120 | L2 `ExplorerMatchesOracle` |
| EX-03 | Ducts in the Current model | 1,053 | L2 `ExplorerMatchesOracle` |
| EX-04 | Snapshot count per category = oracle | equal per category | L2 `ExplorerMatchesOracle` |
| EX-05 | Quick counts = oracle | equal per category | L2 `ExplorerMatchesOracle` |
| EX-06 | Active view counts per category = view collector | equal per category | L2 `ExplorerMatchesOracle` |
| EX-07 | Active view ids = view collector (link through the host view) | same set | L2 `ExplorerMatchesOracle` |
| EX-08 | Tree, paging 200, search, expand / collapse all | rows | L1 `ExplorerTabTests` |
| EX-09 | Tiers T0–T3 planned per row | plan | L1 `PlannerTests` |
| EX-10 | T1: link model row eye hides the link, Reset shows it | temporary bar "1", then gone | L3 UI-06 |
| EX-12 | T3: eye on a category under a link hides those linked elements, Reset shows them | eye Hidden, bar "1", then eye Shown, no bar | L3 UI-07 |
| EX-11 | Selection sync under 50 / above 50 / above 5,000 | state | L1 `SyncTests` |

### Filter operators per value kind

Doors, Walls and Stairs are in the Architectural link; Ducts and Mechanical
Equipment in the Current model.

| ID | Kind, storage | Rule | Expected | Test |
|----|---------------|------|----------|------|
| TX-01 | Text, instance built-in | Doors Mark = "s10" | 1 | L2 matrix, L3 UI-02 |
| TX-02 | Text, Aa on | Mark = "s10" | 0 | L2 matrix, L3 UI-02 |
| TX-03 | Text, Aa on | Mark = "S10" | 1 | L2 matrix |
| TX-04 | Text, Aa on | Mark ≠ "S10" | 141 | L2 matrix |
| TX-05 | Text, Aa on | Mark ≠ "s10" | 142 | L2 matrix |
| TX-06 | Text | Mark contains "1" | 61 | L2 matrix |
| TX-07 | Text | Mark does not contain "1" | 81 | L2 matrix |
| TX-08 | Text | Mark begins with "S" | 22 | L2 matrix |
| TX-09 | Text | Mark does not begin with "S" | 120 | L2 matrix |
| TX-10 | Text | Mark ends with "0" | 3 | L2 matrix |
| TX-11 | Text | Mark does not end with "0" | 139 | L2 matrix |
| TX-12 | Text | Mark > "S5" | 0 | L2 matrix |
| TX-13 | Text | Mark ≤ "S5" | 142 | L2 matrix |
| TX-14 | Text | Doors Comments empty | 142 | L2 matrix |
| TX-15 | Text | Comments not empty | 0 | L2 matrix |
| TX-16 | Text | Mark defined | 142 | L2 matrix |
| TX-17 | Text | Mark undefined | 0 | L2 matrix |
| TX-18 | Text, Aa on | Mark contains "s1" | 0 | L2 matrix |
| TX-19 | Text, Aa on | Mark begins with "S" | 22 | L2 matrix |
| NU-01 | Number, family parameter (two ids) | Swing Angle not empty | 131 | L2 matrix |
| NU-02 | Number | Swing Angle undefined | 11 | L2 matrix |
| NU-03 | Number with unit | Swing Angle > 45° | 130 | L2 matrix, L3 UI-01 |
| NU-04 | Number with unit | Swing Angle = 90° | 130 | L2 matrix, L3 UI-04 |
| NU-05 | Number | Swing Angle ≠ 90° | 1 | L2 matrix |
| NU-06 | Number | Swing Angle ≤ 45° | 1 | L2 matrix |
| NU-07 | Number | Swing Angle ≥ 90° | 130 | L2 matrix |
| NU-08 | Number | Swing Angle < 90° | 1 | L2 matrix |
| NU-09 | Number | Swing Angle empty | 0 | L2 matrix |
| NU-10 | Number | Swing Angle defined | 131 | L2 matrix |
| NU-11 | Number, unit other than display | Ducts Length > "3000 mm" | 131 | L2 matrix |
| NU-12 | Number on the type | Walls Width ≥ "200 mm" | 349 | L2 matrix |
| NU-13 | Number on the instance | Walls Unconnected Height < "3000 mm" | 665 | L2 matrix |
| IN-01 | Integer | Stairs Actual Number of Risers > 10 | 23 | L2 matrix |
| IN-02 | Integer | Risers ≤ 10 | 3 | L2 matrix |
| IN-03 | Integer | Risers not empty | 26 | L2 matrix |
| YN-01 | Yes/No | Walls Room Bounding = Yes | 872 | L2 matrix |
| YN-02 | Yes/No | Room Bounding = No | 248 | L2 matrix |
| YN-03 | Yes/No | Walls Structural ≠ Yes | 1,103 | L2 matrix |
| EN-01 | Enum on the type | Walls Function = Exterior | 303 | L2 matrix |
| EN-02 | Enum on the type | Function ≠ Exterior | 816 | L2 matrix |
| EN-03 | Enum on the instance | Walls Location Line = Wall Centerline | 333 | L2 matrix |
| EN-04 | Enum, unknown name | Function = "No Such Function" | 0 | L2 matrix |
| EN-05 | Enum, unknown name | Function ≠ "No Such Function" | 1,119 | L2 matrix |
| RF-01 | Element id, level | Ducts Reference Level = L3 | 319 | L2 matrix |
| RF-02 | Element id, level | Reference Level ≠ L3 | 734 | L2 matrix |
| RF-03 | Element id, level in a link | Walls Base Constraint = L4 | 210 | L2 matrix |
| RF-04 | Element id, phase | Doors Phase Created = New Construction | 142 | L2 matrix |
| RF-05 | Element id | Reference Level not empty | 1,053 | L2 matrix |
| L1-OPS | Operators offered per kind, value editor per kind | lists | L1 `ParameterModelTests`, `FilterViewTests.CaseToggleOnlyForText`, `YesNoEditorIsNotEditable` |
| LU-01 | Typed length in a link with other units: Current model set to millimeters (rolled back), Arch Walls Width ≥ "200" | 349 (= NU-12) | L2 matrix (`hostLengthUnit`) |
| PS-01 | Picked slope from a link: Arch Roofs Slope = "2" picked (link shows degrees without a symbol) | 8 | L2 matrix (`picked`) |
| PS-02 | Same text typed: read as 2"/12" in the HVAC model | 0 | L2 matrix |
| UN-01 | Typed number parsed once in the Current model's units; a pick keeps the stored range; typing over a pick drops it; stored filters keep the pick | fake | L1 `TypedNumberUsesCurrentUnits`, `PickedValueKeepsStoredNumber`, `TypingDropsPickedNumber`, `StoreKeepsPickedNumber` |

### Storage

| ID | Storage | Rule | Expected | Test |
|----|---------|------|----------|------|
| ST-01 | Type | Walls Fire Rating = "2 HR" | 293 | L2 matrix |
| ST-02 | Type | Walls Fire Rating not empty | 683 | L2 matrix |
| ST-03 | Type | Walls Fire Rating empty | 437 | L2 matrix |
| ST-04 | Instance without value, type with value | Doors Fire Rating = "60 MIN" | 25 | L2 matrix |
| ST-05 | Instance without value, type with value | Doors Fire Rating empty | 29 | L2 matrix |
| ST-06 | Instance without value, type with value | Doors Fire Rating not empty | 113 | L2 matrix |
| ST-07 | Built-in read from the type | Ducts Family Name = "Round Duct" | 1,045 | L2 matrix |
| ST-08 | Type text | Doors Type Mark empty | 0 | L2 matrix |
| ST-09 | Shared (first by name on an Arch model element), every category | defined | 10 | L2 matrix |
| ST-10 | Shared, every category | not empty | 10 | L2 matrix |
| ST-11 | Key missing in the category | Ducts Swing Angle not empty | 0 | L2 matrix |
| ST-12 | Key missing in the category | Ducts Swing Angle undefined | 1,053 | L2 matrix |
| ST-13 | Type presence, guards, OR over ids | fake | L1 `TypeBoundTests`, `FamilyParameterTests` |
| SB-01 | Every sampled built-in the filter dialog omits finds its own value (7 documents) | 0 failures | L2 `CatalogMatchesFacts(SB-01)` |

### Groups and group scope

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| GR-01 | AND: Swing Angle = 90° and Mark begins with S | 22 | L2 matrix |
| GR-02 | OR: Mark = S10 or Swing Angle undefined | 12 | L2 matrix, L3 UI-03 |
| GR-03 | Nested OR inside AND (Walls) | 96 | L2 matrix |
| GR-04 | No rules: every checked element | 142 | L2 matrix |
| GR-05 | Empty nested group is true | 142 | L2 matrix |
| GR-06 | Nested AND inside OR | 68 | L2 matrix |
| GS-01 | Group categories narrow the parent (Doors / Walls) | 433 | L2 matrix |
| GS-02 | Group documents pick per file (Arch Doors / HVAC Ducts) | 1,195 | L2 matrix |
| GS-03 | Unresolved source under OR is skipped | 1 | L2 matrix |
| GS-04 | Unresolved source under AND matches nothing | 0 | L2 matrix |
| GS-05 | Group naming only the Current model, AND parent, link ticked | 0 | L2 matrix |
| GS-06 | Nested category scopes intersect | 683 | L2 matrix |
| GS-07 | Connector, scope lists, warning, moves, collapse | state | L1 `FilterLogicTests`, `FilterTreeMovesTests`, Views `FilterLogicViewTests` |
| GS-08 | Drop decisions: up, down, onto itself, into, out of and across nested groups, group into its own descendant, zones with hysteresis, group header vs rules area, empty group, preview lifetime | effects, order, line | Views `FilterDropTests` |
| GS-09 | Drag preview, line and drop end to end | order changes, line shown | Sandbox `--drag-shots` in process; `--drag` real mouse is opt-in only ([ui.md](ui.md#how-drag-is-tested)) |
| GS-10 | Scope tags stay inside their box at 304 and 600 px, nested groups | bounds | Views `ScopeTagsStayInsideBox` |

### Case-sensitive rules

TX-02 to TX-05, TX-18, TX-19 above; widening and exact pass also in L1
`CaseSensitiveTests`.

### Live, recount, edits

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| LV-01 | Edit runs once after 300 ms | one selection | L1 `LiveDebounceFiresOnce` |
| LV-02 | Model edit recounts, never selects | no selection sent | L1 `ModelEditRecountsWithoutSelecting` |
| LV-03 | Value edit (3 ducts get a comment), cache dropped as `DocumentChanged` does | 0 → 3 | L2 `ValueEditRecounts` |
| LV-04 | Copied duct: candidates stay until the cache drops (version does not change per transaction) | n → n, then n + 1 | L2 `AddedElementCountsAfterRefresh` |
| LV-05 | Live pauses above 5,000 | paused, nothing sent | L1 `LivePausesAbove5000` |
| LV-06 | Live pause shows in the panel: switch and label *Live* unchanged, line *Paused: over 5,000 matches*; the line goes away under the limit, with no categories, with no filter, with Live off | SC-02 > 5,000 | L1 `PauseEnds*`, Views `PausedLine*`, L3 UI-05 |
| LV-07 | Add / Remove keep the baseline | selection | L1 `AddModeKeepsBaseline` |
| LV-08 | Readiness: edit Busy, run Idle, generation grows | status | L1 `ActivityTests`, Views `ActivityViewTests` |

### Custom filters

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| CF-01 | New filter gets next free name, rename, unique, trimmed, Esc | state | L1 `FilterTabTests` (`NewFilterGetsNextName`, `NameMustBeUnique`, `EscRestoresLastName` …) |
| CF-02 | Switch with unsaved changes asks | pending | L1 `SwitchAsksWhenUnsaved` |
| CF-03 | Store round trip, scope kept, no `not` | equal | L1 `StoreKeepsScopeWithoutNot`, `CustomFilterLibraryTests` |
| CF-04 | Save, switch away, reload: same count | NU-04 | L3 UI-04 |

### Revit filters as sources

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| HF-01 | Inverted AND reads as OR of negated operators (De Morgan) | Any(not contains, not equal) | L2 `InvertedAndReadsAsOr` |
| HF-02 | Read-back of the inverted filter counts like Revit | = native | L2 `InvertedReadCountsLikeRevit` |
| HF-03 | Inverted rule with no negated operator | one Unsupported rule | L2 `InvertedWithoutNegationIsUnsupported` |
| HF-04 | Element id comparison rule (Phase Created greater) | Unsupported rule | L2 `ReferenceComparisonIsUnsupported` |
| HF-05 | Rule-based filter runs natively in its document | = native | L2 `RuleFilterRunsNatively` |
| HF-06 | Selection filter uses stored ids | same ids | L2 `SelectionFilterUsesStoredIds` |
| HF-07 | Catalog lists rule-based and selection filters | kinds | L2 `CatalogListsBothKinds` |
| HF-08 | Revit filter is read only, Duplicate | state | L1 `HostFilterIsReadOnly`, `ReadOnlyHostIgnoresEdits`, `StripDropsUnsupportedRules` |

### Parameter picker

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| PK-01 | Family parameter is one item, Family tag, Number, Instance | one item | L2 `CatalogMatchesFacts(PK-01)` |
| PK-02 | Built-in text on the instance | Text, Built-in | L2 `CatalogMatchesFacts(PK-02)` |
| PK-03 | Type enum is Enum, Type tag | Enumerated | L2 `CatalogMatchesFacts(PK-03)` |
| PK-04 | Yes/No kind from the spec | YesNo | L2 `CatalogMatchesFacts(PK-04)` |
| PK-05 | Reference Level listed for ducts as Element | Reference | L2 `CatalogMatchesFacts(PK-05)` |
| PK-06 | Integer stays Integer | Integer | L2 `CatalogMatchesFacts(PK-06)` |
| PK-07 | Shared parameter tagged | Shared | L2 `CatalogMatchesFacts(PK-07)` |
| PK-08 | Models: Mark in HVAC and Arch | 2 models | L2 `CatalogMatchesFacts(PK-08)` |
| PK-09 | Models: Swing Angle in Arch only, no warning | 1 model | L2 `CatalogMatchesFacts(PK-09)` |
| CT-01 | Catalog build time from the panel's Trace lines, cold cache | lines for both documents | L2 `CatalogMatchesFacts(CT-01)` ([parameter-catalog.md](parameter-catalog.md#build-time-with-the-tooltip-facts)) |
| PK-13 | Instance / Type sections with row counts, header not a list item and not selectable, sticky on scroll, exact name first | UI state | Views `PickerGroupsBySection`, `HeadersAreNotListItems`, `HeaderIsNotAnItem`, `HeaderStaysOnScroll`; HandyControl `ExactMatchComesFirst` |
| PK-14 | Row: source icon per origin (none for built-in), category summary, no tags or warning colors | UI state | Views `RowIconFollowsSource`, `RowShowsCategorySummary`, `RowHasNoTagsOrWarnings` |
| PK-15 | Tooltip rows present only with a value: built-in measured, built-in text, shared, project, family, yes/no; five then "+ N more" | rows | L1 `ParameterTipTests`, Views `TipShowsRowsAndLists` |
| PK-16 | Rule card: no unit line, placeholder in the Current model's format, "Type" for a type parameter | UI state | L1 `PlaceholderUsesCurrentModel`, `TextHasNoPlaceholder`; Views `RuleCardHasNoUnitLine`, `ValueBoxShowsExample`, `ChosenTypeParameterShowsType` |
| PK-10 | Value counts of Swing Angle on Doors | total 142, values 131 | L2 `CatalogMatchesFacts(PK-10)` |
| PK-11 | Typing filters, ≤ 100 opens, tags, SetValue picks | UI state | Views `FilterViewTests` (`PickerTypingFiltersIgnoringCase`, `PickerValueSetsParameterName` …) |
| PK-12 | Value pattern picks a parameter in Revit | "Swing Angle" | L3 UI-01 |

### Visibility in the active 3D view (L2 `VisibilityMatchesApi`, `visibility.json`)

Each case activates `{3D}` through `UIDocument.ActiveView`, builds the plan the
panel sends, runs it through `RevitViewActions`, reads the view state through
the API, resets, and checks the reset. Images: the view before, after and after
reset.

| ID | Tier, source | Action | Expected (API) | Test |
|----|--------------|--------|----------------|------|
| VI-01 | T0 Explorer row, Current model Ducts | Hide | temporary mode on, ducts hidden, terminals and link visible | L2 |
| VI-02 | T0 Explorer row, Current model Ducts | Isolate | ducts visible, terminals and link hidden | L2 |
| VI-03 | T0 Explorer row, Current model Ducts | Color | view properties mode on, ducts colored, terminals not; Reset restores each duct's overrides | L2 |
| VI-04 | T0 filter result RF-01 | Color | L3 ducts colored, RF-02 ducts not; Reset restores the overrides | L2 |
| VI-05 | T0 filter result RF-01 | Hide | RF-01 ducts hidden, RF-02 ducts visible | L2 |
| VI-06 | T1 link model row | Hide | link instance hidden, ducts visible | L2 |
| VI-07 | T1 link model row | Isolate | link visible, ducts hidden | L2 |
| VI-08 | T1 link model row | Color | link instance colored; Reset restores its overrides | L2 |
| VI-09 | T2 All models Ducts | Hide | ducts hidden, terminals visible | L2 |
| VI-10 | T2 All models Ducts | Isolate | ducts visible, terminals hidden | L2 |
| VI-11 | T2 All models Ducts | Color | category override colored, cleared by Reset | L2 |
| VI-12 | T2 All models Doors (only in the link) | Hide | temporary mode on, ducts visible; linked doors in the image only | L2 |
| VI-13 | T3 filter result GR-02 (link doors) | Hide | planned as T3, not run (Gotcha 13) | L2 |
| VI-14 | T3 filter result GR-02 | Color | refused: "Inside a link, single elements can only be hidden and shown…" | L2 |
| VI-15 | T0 + T3 filter result GS-02 | Hide | planned as T0 + T3, not run | L2 |

### L3 flows (`PanelFlowTests`, `ui-flows.json`)

| ID | Flow | Expected | Test |
|----|------|----------|------|
| UI-01 | Build NU-03 in the panel | parameter "Swing Angle", count = NU-03 | `NumberRuleCountsGolden` |
| UI-02 | TX-01, then Aa on | TX-01, then TX-02 | `CaseToggleChangesCount` |
| UI-03 | Connector to OR (GR-02) | GR-02 | `ConnectorMakesOrGroup` |
| UI-04 | Save NU-04, new filter, reload | NU-04 | `SavedFilterReloads` |
| UI-05 | All categories in the link (SC-02), then None | Live switch on, line *Paused: over 5,000 matches*, then no line | `LivePausesAboveLimit` |
| UI-06 | Eye on the link model row, Reset | bar "1 change", then no bar | `LinkHideShowsTemporaryBar` |
| UI-07 | Eye on Doors under the link (T3, Hide in View), Reset | eye Hidden and bar "1 change", then eye Shown and no bar | `LinkDoorsHideThenReset` |
## Mismatches found

None open. List a row here (ID, expected from the oracle or the Revit API,
panel value, cause seen with `PrintCaseDifference`) when the panel differs;
the expected value stays.

Fixed on 2026-11-17, with the cause:

| ID | Cause | Fix |
|----|-------|-----|
| TX-12, TX-13 | Native text *greater* / *less* sort `S10` after `S5` | Text comparisons run in the exact pass in ordinal order ([filters.md](filters.md#exact-text-rules)) |
| EN-02 | Native integer *not equals* passes a type without a value | Value comparisons on non-text storage are guarded by *not empty* |
| EN-03 | Curtain walls are blind to both has-value rules on Location Line | `Holds` (`≥ int.MinValue`) counts as present |
| ST-02, ST-03, ST-05, ST-06, GS-06 | A type Fire Rating of `""` counted as a value | For text, `""` is no value, on instances and in `TypePresence` |
| VI-03, VI-04, VI-08 | Element Color stays after Temporary View Properties ends | Reset restores the overrides recorded before the change (Gotcha 15) |

EN-01 was 311 in the first live check, made before the design-option rule; with
main + primary only it is 303 for oracle and panel alike, so it is not a bug.
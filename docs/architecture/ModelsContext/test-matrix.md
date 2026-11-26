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
L3 panel inside Revit       tests/RevitDevTool.ModelsContext.UiTests             live Revit, FlaUI on panel controls
```

| Layer | Carries | Why here |
|-------|---------|----------|
| L1 | View model logic, debounce, selection sync and pause, library (save, rename, switch), scope pruning, tree moves, planner tiers, picker filtering, AutomationIds | Pure logic on a seeded fake model; runs in CI on every change |
| L2 | Every count in this matrix, catalog facts, host filter read-back, edits and cache refresh | The Revit API is the thing under test, not the UI. Calls go straight to `RevitFilterEvaluator`, `RevitModelSource`, `RevitFilterCatalog` and `ParameterCatalogReader` inside Revit, with no waits |
| L3 | A few end-to-end flows: build a rule, Aa, AND to OR, save and reload, selection sync pause, link hide and Reset, linked element hide (T3) and Reset | Proves the wiring from UIA input to count. Each flow repeats a golden L2 count, so a UI failure is never a counting question. T3 runs only here, through the panel's own click path |

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
- The active-view rows call `SampleViews.Activate`: it switches to an already open
  `L3` and refreshes it. A view opened inside a run is not drawn until the run returns
  and the view collector then counts far too many Current model elements (SC-05
  1,669 instead of 917), so `Activate` stops as Inconclusive when `L3` is not open.
  `scripts/test-models-context.ps1` runs `ViewSetupTests.PreparesSample` in a run of
  its own first (it activates `L3` and shows the Model Context pane through the Revit
  API), then the suite. The sample is opened before the script through MCP
  `launch_host`; a document is never opened inside a run (Gotcha 20).

### Evidence per run

Each run writes `artifacts/models-context-tests/<yyyyMMdd-HHmmss>/` (ignored by
git). L2 opens a new folder; L3 reuses the folder of the latest run until it has
its own results, so running L2 then L3 gives one folder:

| File | Holds |
|------|-------|
| `report.md` | Summary per layer with wall time, then per case: what it checks, steps done, expected, oracle, panel, timings, images, PASS/FAIL, *How to verify by hand* |
| `results.json`, `results-L2.json`, `results-L3.json` | The same data, diffable |
| `images/` | L2 visibility: the active view rendered with `Document.ExportImage` (before, after, after reset) only with `-Images`, plus one `failed` image for a failing case. L3: the panel (UIA capture) and the Revit window, per image slot |
| `mismatched-elements.md`, `sample-probe.json` | Diagnostics |

The assertion (oracle or Revit API state) is the pass/fail source; images are
evidence. `-Images` creates `artifacts/models-context-tests/images.on`, which L2 reads; without it
visibility cases export nothing unless they fail, and the manual-verify steps of those cases
need a run with `-Images` for picture evidence.

### Setup through MCP, UI Automation only for the panel

Everything that is not a click on the panel goes through MCP or the Revit API:

| Step | How |
|------|-----|
| Start Revit | MCP `launch_host(hostApp="Revit", versionNumber="2024")`, without `filePath`. The startup dialogs (add-in security, Always Load) are answered by the host launcher. If it times out, Revit keeps starting: wait until `list_processes` shows the pid |
| Open the sample | `code_mode`: `InvokeAsync("open_document", {filePath}, pid)`. Not `launch_host(filePath=...)`: on Revit 2024 a model opened at startup opens before the add-in registers its panes, and the panes are never created in that session (plan, Phase 7) |
| Check the model, links, active view, counts by hand | `code_mode`: `execute_csharp_code` or `ReadAsync("revit://model/context", null, pid)` |
| Activate `L3` and show the pane | L2 `ViewSetupTests.PreparesSample` (Revit API, its own run) |
| Act on the panel (eye, Aa, connector, Save, Reset) and read its result | L3, FlaUI by AutomationId and UIA patterns |

Use FlaUI only for the panel's own controls, which no API reaches. Do not use it
to answer Revit dialogs, click the ribbon or open files.

Panel preconditions a flow (and anyone checking the panel by hand) must meet:

- The Filter editor (categories, conditions, groups) is read only until a custom
  filter is selected. A flow starts with **+** (`Filter.Add`) and checks that the
  source shows the new filter before it touches `Filter.Categories`.
- L3 attaches to the Revit that L2 recorded, else to `MODELS_CONTEXT_REVIT_PID`
  (the script sets it). With neither, it stops as Inconclusive when more than one
  Revit shows the sample, so it never drives another session's panel.

### Rules for adding a case

1. Put all data in `Data/*.json`; the test code holds the flow only.
2. Take the expected value from the oracle (`results.json` of a run), never
   from the panel. A row where oracle = golden and the panel differs is a panel
   bug: list it under Mismatches, do not change the golden value.
3. Write `manualSteps` a person can follow in Revit without the panel's code.
4. Give every UI case `images` with at least `before` and `after`; the run fails a
   case whose slots were not captured. Visibility cases list their slots too, but L2
   requires them only in a run with `-Images`.
5. L3 waits only on the readiness signal (`ItemStatus` Idle/Busy); no sleeps,
   no keys (the one exception: Shift+F10 opens a context menu, UI-18 to UI-20), no mouse, UIA patterns by
   AutomationId only. Never poll Revit's
   UIA tree while it is busy. A collapsed or hidden control is still in the UIA
   tree (offscreen): test presence with `Shown` / `Gone`, never with a bare
   `FindFirstDescendant`.
   L3 uses the screen and runs only when asked (`scripts/test-models-context.ps1 -Ui`):
   every UIA action (Invoke, Toggle, Select) brings the target window to the front,
   even a plain WPF window with none of our code, and UI-18 to UI-20 take the
   keyboard focus for Shift+F10. L2 takes nothing (Revit API only). Within L3:
   - Pick a combo item with `RevitPanel.Choose`. It finds the item through
     `ItemContainer` and selects it while the drop-down stays closed. A WPF combo
     box makes its item containers only when the drop-down first opens. For a combo
     box that never opened, `Choose` opens it for one `Select` and closes it in
     `finally`, and the step log records it. An open drop-down holds the mouse
     capture of Revit's UI thread. Never call `ExpandCollapse` or `Realize`
     directly: `Realize` also opens the drop-down.
   - Images come from `PrintWindow` of the window that holds the panel (`Shot`),
     never from the screen. Another window on top does not show in the image.
   - `RevitPanel.Act` records a step when Revit comes to the front during an
     action; with the UIA behavior above that is expected. See Current gaps
     in [docs/agents/test-matrix.md](../../agents/test-matrix.md).
6. The model ends unmodified: edits run in rolled-back transactions and
   temporary modes are reset through the panel's own Reset; a test never
   repairs what the panel left behind.
7. Never run T3 (Hide in View for link elements) from an L2 test; declare
   `expected.plannedTiers` instead (Gotcha 13 in [README.md](README.md#gotchas)).
   Never build a link view collector for a view other than the active one.
   L3 attaches to the Revit that ran L2 of the same run folder (Gotcha 14).
8. `CaseRulesTests` (in the L3 project, runs without Revit) fails a case
   without description, expected or manual steps, a duplicate id, a UI case without `before`/`after` slots, a T3 case without
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
# 1. Deploy the current add-in for the test year.
pwsh -File scripts/build-host.ps1 -HostApp Revit -Year 2024
# 2. Through MCP (agent tool calls, not shell commands): launch_host(hostApp="Revit", versionNumber="2024"),
#    then code_mode open_document(filePath="C:\Program Files\Autodesk\Revit 2024\Samples\Snowdon Towers Sample HVAC.rvt"); keep the pid.
# 3. L2 then L3 in one command. Step 1 of the script prepares the sample in a run of its own
#    (Gotcha 19), step 2 is the full L2 run, step 3 is L3. -Year 2026 for the 2026 host; -Images exports the full visibility image set.
pwsh -File scripts/test-models-context.ps1 -RevitPid <revit pid>
# L2 alone (run PreparesSample once per Revit session first)
dotnet test --project tests/RevitDevTool.ModelsContext.LiveTests/RevitDevTool.ModelsContext.LiveTests.csproj -c Debug.Autodesk.2024 -- --pid <revit pid> --filter PreparesSample
dotnet test --project tests/RevitDevTool.ModelsContext.LiveTests/RevitDevTool.ModelsContext.LiveTests.csproj -c Debug.Autodesk.2024 -- --pid <revit pid>
# L3 alone: attaches to the Revit pid L2 recorded in the run folder
dotnet run --project tests/RevitDevTool.ModelsContext.UiTests/RevitDevTool.ModelsContext.UiTests.csproj
# Data rules only, no Revit needed
dotnet run --project tests/RevitDevTool.ModelsContext.UiTests/RevitDevTool.ModelsContext.UiTests.csproj -- --filter CaseRulesTests
```

Always pass `--pid`: without it the adapter reuses the oldest Revit of the
configuration's year, or starts one without the sample. L2 writes its Revit pid to
`results-L2.json`; L3 attaches only to that process and stops as Inconclusive
when it does not show the sample.

L2 tests that edit the model (host filters, edits) run in a transaction that is
rolled back. Visibility cases commit temporary modes and reset them. L3 saves
one custom filter named `uia-live …` and deletes it in the same test. Never
save the model.

### Window run loop (no Revit restart)

`ModelsContextWindowCommand` opens the real panel in a plain window
(AutomationId `ModelContext.Window`) from a fresh load of `RevitDevTool.Tools.dll`,
so a panel or adapter change shows up without restarting Revit.

```powershell
dotnet build source/RevitDevTool.Tools/RevitDevTool.Tools.csproj -c Debug.Autodesk.2026 -p:CommandBuild=true
```

`CommandBuild=true` turns on the repo's ILRepack (`props/ILRepack.targets`, ADR 0019) for
`RevitDevTool.Tools` only and writes to `bin\<Configuration>.Command\`: one merged
`RevitDevTool.Tools.dll` plus the loose `DevTools.UI.dll` (the add-in's shared copy, gotcha 14)
and the sidecars the add-in also keeps loose (FSharp.Core, IronPython, DLR). Command Execution
needs exact identities in a command folder, so the plain build's NuGet siblings (System.Text.Json
8.0.0.0 asked for, another version beside) were rejected. The add-in build does not change.

Then run it through Command Execution or MCP:
`run_command(path="<repo>\source\RevitDevTool.Tools\bin\Debug.Autodesk.2026.Command\RevitDevTool.Tools.dll", entry="RevitDevTool.Tools.ModelsContext.Pane.ModelsContextWindowCommand")`.

- Each run hides the docked pane and closes the window of an earlier run, so one
  panel answers Revit events and owns the `Model Context <n>` filters (gotcha 25).
- Closing the window removes its view filters, unsubscribes its events and
  disposes the panel. Temporary modes and recorded overrides live in that load's
  memory only: press Reset before closing.
- The window builds its own services (`AddSettingServices`, a ZLogger rolling file,
  `AddModelsContext`) and subscribes through `RevitContext.UiControlledApplication`;
  it shares the pane's wiring (`ModelsContextPanel`).
- Its log is not the add-in log: `%APPDATA%\RevitDevTool\{Year}\Logs\models-context-window_{pid}_{yyyyMMdd}_{seq}.log`
  (Debug and up, one file per Revit process and day, shared by every reload). Each run writes
  "Model Context window opened from …" and each close "Model Context window closed.", so a
  reload shows as a close then an open.
- `DevTools.UI` binds to the add-in's loaded copy (gotcha 14): edits there still
  need a Revit restart. On net48 years (2024 and below) each run loads a copy that
  is never unloaded; restart Revit after many runs.

## Matrix

Expected values come from `Data/*.json` (recorded from the oracle on the Revit 2026
sample, 2026-11-17). Change them there, not here alone.

Revit 2026 is the primary test host, Revit 2024 the secondary (`-Year 2024` is the script default).
Current results (Snowdon HVAC + six links):

- Revit 2026, final deploy (warm snapshot back to one id pass; Categories box focus keeps the checks), fresh Revit,
  0064 rule: `-Ui` run `20261126-202825` gives L2 447 PASS, 0 FAIL, 8 Inconclusive (CA by design) and L3 28 of 28
  (UI-11 checks Doors and Walls), 4 live steps skipped; safety ok after L2, after L3 and at host end. CA-01 to CA-10
  pass alone in `{3D}` (run `20261126-203659`). Docked pane through UIA: `L3` 1,041 with Duct Tags 73; Whole model
  2,928.

- Revit 2026, deployed add-in (`scripts/build-host.ps1 -HostApp Revit -Year 2026`, fresh Revit, 0064 rule):
  L2 run `20261126-194036` 447 PASS, 0 FAIL, 8 Inconclusive (CA by design); CA-01 to CA-10 pass alone in `{3D}` (run
  `20261126-194505`); the `-Ui` run `20261126-194559` gives L2 0 FAIL again and L3 27 of 28 (UI-11 fails, also alone
  in run `20261126-195428`: after Select Walls and AddToSelection Doors only Doors is checked; Views
  `AddKeepsBothChecked` passes offline), 4 live steps skipped; safety ok after L2, after L3 and at host end. HVAC SW-01
  run `20261126-195723`: every document's Whole model count equals `ExportSnapshots` (Current 2,928, Architectural
  9,743, Structural 1,475, Facades 528, Plumbing 6,563, Electrical 3,003, Site 119). Docked pane through UIA: Whole
  model 2,928; `L3` 1,041 with Duct Tags 73.

- Revit 2026, runs `20261126-185127` and `20261126-192631`, the latter on a fresh Revit with the oracle link guard
  ([0064](../../decisions/0064-model-context-annotation-in-explorer.md) rule, window restored on an unlocked desktop):
  L2 447 PASS, 0 FAIL, 8 Inconclusive (CA by design) in each, safety ok after L2; SC-01 2,928, SC-02 9,743, SC-05 1,041, SC-06 1,956, AN-01 to AN-03 and SB-01 pass. L3 did not run. In the
  earlier run `20261126-180708` the `L3` collector gave 1,257 (50 Duct Tags for 73, oracle = panel) with ducts of
  other levels in it; Revit was minimized when checked afterwards (Gotcha 19).
- Revit 2026, run `20261125-013631` (fresh Revit after a restart): L2 441 PASS, 0 FAIL, 8 Inconclusive (CA by design)
  in 268 s, SC-05 917; L3 18 of 20 in 298 s, the new menu flows UI-18 and UI-20 then passed after test fixes
  (runs `20261125-014836`, `-014723`); UI-19 passed in the full run; safety ok after L2, after L3 and at host end.
  CA-01 to CA-10 passed alone in `{3D}` (run `20261125-014919`).
- Revit 2026, run `20261125-005153` (fresh Revit, Snowdon HVAC + six links, Center line left out, eye counts what the
  view draws): L2 440 PASS, 1 FAIL (SC-01, golden moved, below), 8 Inconclusive (CA-01 to CA-09 need `{3D}` from a
  run of their own) in 275 s; L3 17 of 17 flows in 473 s; safety ok after L2; the count cases and the host end state
  passed again in run `20261125-010506` (84 of 84). CA-01 to CA-10 passed alone with `{3D}` active (run
  `20261125-010601`). WH-01 to WH-03, WH-05, WH-06 and PI-03 ran and passed.
- Goldens from the oracle on Revit 2026 (panel = oracle each time): SC-02 7,526 → 11,195 (UI-04 too; Arch subcategory
  elements: Railings › Supports 2,608, Wall Sweeps 258, Curtain Wall Grids 235 …), SC-06 780 → 1,028, EN-02 816 → 817,
  EN-05 1,119 → 1,120 (2026-11-24). SC-01 went 2,929 → 4,961 with Center line collected and back to 2,929 once Center
  line was left out (run `20261125-005153`). TX-04, TX-05, NU-05, YN-03 and RF-02 kept their values.
- Before 0064, SC-05 (Active view `L3`) stayed 917 (oracle = panel). On 2026-11-25 the L3 view collector gave 917 in 21 probes:
  fresh session never in front, Revit minimized with L3 reopened, in front after `ZoomToFit`, after a full L2 run.
  Earlier sessions gave 1,198 and 1,669 (only Ducts and Flex Ducts moved) and could not be repeated (Gotcha 19).
- Earlier: Revit 2026, run `20261118-103516`: L2 130 cases PASS in 110 s; L3 6 flows PASS plus 7 data rule tests. A later L3 run, `20261118-133615`, passed 13/13 in 73 s.
- Revit 2024, run `20261118-093432`: L2 127/131, L3 5/6; the differences are listed under Mismatches. A case where the oracle differs from the golden value there means the 2024 sample file differs, not that the panel is wrong: record it with both values and decide per-version golden values before changing `Data/*.json`.
- L1 last run (net10.0-windows, 2026-11-25): core and views all pass.
- Revit 2026, run `20261125-091712` (Why hidden matrix build, unlocked desktop): L2 443 PASS, 0 FAIL, 8
  Inconclusive (CA by design; WH-01 to WH-03, WH-05, WH-06, WH-13 pass, WH-12 10 of 10 *Drawn with its railing* in
  `{3D}`), safety ok after L2 and L3; L3 27 of 32 (UI-19, UI-20 passed; UI-16, UI-18, UI-21, UI-07, UI-08 failed). The flows
  alone on the same build, `{3D}` active: runs `20261125-093511` (6 of 7: UI-18 to UI-21, UI-07, UI-08 pass) and
  `20261125-095040` (UI-18 to UI-21 and UI-07 pass; UI-08 failed while another session was changing the UI test
  harness). UI-21 dot pixels 0 before, 32 after Keep (eye Hidden), 0 after Show (eye Shown). The first UI-21
  failure was a test bug: the Doors row is dimmed once all its doors are hidden, so the dot is #8d6d1f (the warning
  color at about 55% opacity), outside a ±40 color match; the flow now matches the warning color's hue and needs a
  dot-sized cluster. UI-16 fails often on this build and on the build before it (2 of 3 in an A/B on the old code):
  after the duct hide, the Explorer rebuild sometimes leaves Ducts (or Taps) without an expander for over 10 s.
  Cause (found offline): the hide's document change is a delta with no element change, and it still reloaded the
  whole context after the panel had gone idle, replacing every row object; a row the flow had just found lost its
  expander. Fixed in `HostEventRelay` (outside Active view such a delta only re-reads hidden states); L1
  `LifecycleTests.EmptyDeltaKeepsRows` and `EmptyDeltaReloadsActiveView`. Live rerun of UI-16 pending.
- Explicit L2 cases, Revit 2026, 2026-11-25 (Why hidden matrix build): WH-04 PASS (*Above view range*), WH-09 PASS
  (240 sampled linked elements, none falls back; first causes BelowRange 80, AboveRange 72, FilterPossible 38,
  Category 33, Phase 9, OutsideCrop 4, Railing 2, NotDrawn 2), WH-11 PASS (run `20261125-091514`, `L3`); WH-12 PASS
  (`20261125-091643`, `L3`); WH-14 PASS on the Architectural work copy in `{3D}` (4 hidden links, Revit stays up);
  WH-15 PASS on its Longitudinal Section (`20261125-090007`). NE-01 PASS earlier (run `20261125-030129`).
- Why hidden sample (MCP, every hidden row by the panel rule, 4 explained per category, weighted by the category's
  count): HVAC `L3` 4 of 21,015 fall back (Facades slab edges), HVAC `{3D}` 0 of 2,054, Architectural `L3` 149 of
  22,239, Architectural `{3D}` 0 of 15,056, Architectural Longitudinal Section 0 of 17,643. Before the matrix
  work, HVAC `{3D}` had 1,869 of 2,054 (railing parts) and the section 5,634 of 17,643 (behind the section line).
- L3 flows check only the Architectural link at start (`PanelSteps.CheckOnly`), since a view's links now check at open.

`L2 matrix` below means `FilterMatrixTests.CountMatchesOracle(<id>)`.

### Context scope (L2 `FilterMatrixTests`, `ExplorerCountTests`)

| ID | Use case | Docs | Expected | Test |
|----|----------|------|----------|------|
| SC-01 | Whole model, every listed category, elements with no owner view | Current | 2,928 | L2 matrix |
| SC-02 | Whole model, every listed category, elements with no owner view | Arch | 9,743 | L2 matrix, L3 UI-04 |
| SC-03 | Link unchecked: Doors in the Current model only | Current | 0 | L2 matrix |
| SC-04 | Link checked: Doors in both | Both | 142 | L2 matrix |
| SC-05 | Active view, every listed category, annotation included | Current | 1,041 | L2 matrix |
| SC-06 | Active view through the link (railing parts drawn with their railing count, HR-04) | Arch | 1,956 | L2 matrix |
| SC-07 | Active view, Doors | Both | 24 | L2 matrix |
| SC-08 | Active view, Swing Angle > 45° | Both | 22 | L2 matrix |
| SC-09 | Secondary design options: Whole model only counts main + primary; view and selection follow the collector | Arch | snapshot counts = oracle (main + primary) | L2 `ExplorerMatchesOracle` |
| SC-10 | Documents list: Current model first, then each loaded link | all | keys equal | L2 `ExplorerMatchesOracle` |
| SC-11 | Checks, last check, unloaded link | fake | state | L1 `PanelBarsTests.AtLeastOneDocumentStaysChecked`, Views `PanelViewTests.AllSkipsUnloadedLink` … |
| AN-01 | Active view annotation on floor plan `L3`: quick and tree counts per category | Current | equal to the oracle | L2 `ExplorerMatchesOracle` |
| AN-02 | Active view annotation on sheet `Cover Sheet`: quick and tree counts per category | Current | equal to the oracle | L2 `ExplorerMatchesOracle` |
| AN-03 | Whole model annotation: only elements with no owner view, quick and tree counts | Current | equal to the oracle | L2 `ExplorerMatchesOracle` |

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
| EX-09 | Tiers T0–T4 planned per row | plan | L1 `PlannerTests` |
| LA-01 | Link isolate: rows and results plan `LinkIsolateStep` (mixed results split T0 + T3), the plan carries the view id, the rest is journaled (not the targets) and reads hidden, Reset and an eye back on show it, a Show that reads back 0 drops the entries and reports once, Keep takes them off the bar and Reset leaves them (the eye and the dot stay; the eye shows them again) | plan, journal, eyes, message | L1 `PlannerTests` (`LinkIsolateKeepsTargets`, `MixedIsolateSplits`), `PanelBarsTests` (`LinkIsolateJournalsRest`, `ResetShowsIsolateRest`, `EyeOnEndsLinkIsolate`, `EyeOnUnshownRestReports`, `ResetDropsUnshownOnce`, `KeepTakesIsolateOffBar`) |
| LA-02 | Link overrides: type row = Family Name + Type Name, category row = no rule, instance row and ids without a rule refused, a result filter keeps its rules, a non-native rule and over 100 rules refused, Revit 2023 refused, tooltip note, marks on the link rows, one re-apply after eye back on, a closed link drops the plan | plan, reason, marks | L1 `PlannerTests` (`LinkTypeRowColorsByName`, `LinkGroupRowColorsCategory`, `LinkInstanceRowRefusesColor`, `LinkResultWithoutFilterRefused`, `LinkResultUsesFilterRules`, `UndefinedRuleIsNotNative`, `LinkRulesOverCapRefused`, `TypeRowsOverCapRefused`, `LinkCheckFlagsFilter`), `PanelBarsTests` (`LinkGroupMenuOffersLinkActions`, `LinkOverridesNeedNewerRevit`, `LinkColorMarksRows`, `EyeOnReappliesLinkColorOnce`, `ClosedLinkDropsFilterPlan`) |
| EX-10 | T1: link model row eye hides the link, Reset shows it | temporary bar "1", then gone | L3 UI-05 |
| EX-11 | Selection sync under 50 / above 50 / above 5,000; with sync on the status counts the Revit selection in checked documents, above 50 the hint reads *Too many to reveal · Reveal all*, Expand all keeps the count and sends nothing; sync off counts rows | state, status, hint, host selection | L1 `SyncTests` (`SyncShowsBadgesAbove50`, `CountShowsHostOutsideTree`, `LargeHostSelectionKeepsFullCount`, `SyncOffCountsTreeRows`, `CountShowsBeforeSend`); live window (Revit 2026, Snowdon HVAC, 380 picked references = 224 elements): *224 selected*, then 224 after Reveal all, Expand all and Collapse all, Revit references unchanged |
| EX-12 | T3: eye on a category under a link hides those linked elements, Reset shows them | eye Hidden, bar "1", then eye Shown, no bar | L3 UI-06 |
| EX-13 | Current model row: the eye and the menu act by id on every element of the listed categories, never by category; links stay drawn on Hide, Isolate hides them; selection sync selects its elements | one `ElementsStep`, no `CategoryStep`, eyes | L1 `PlannerTests.ModelRowHidesListedIds`, `ExplorerTabTests` (`ModelEyeHidesOnlyHost`, `ModelIsolateKeepsHostIds`); Views `ModelRowEyeHidesHost`; live window (Revit 2026, Snowdon HVAC `{3D}`, 2026-11-26): eye hides 2,806 listed, collector 4,812 → 5, both links drawn, eye again 4,812; Isolate keeps 2,808, no link drawn; sync *2,806 selected* = Revit selection; Reset back to 4,812 |

### Explorer boxes, hidden rows, Pick, shared engine

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| CB-01 | Categories box: all checked at first, uncheck drops the group row, model count follows checks, a new category shows checked | state | L1 `CategoryBoxTests` (`CategoriesStartChecked`, `UncheckDropsGroupRow`, `ModelCountFollowsChecks`, `NewCategoryShowsChecked`) |
| CB-02 | Menu acts on the checked categories, no check disables it with *Select categories*, search keeps hidden checks, `SetChecks` is one change, the link notice shows after Isolate, the eye hides the category everywhere, tooltip names the links | plan, message, CanExecute | L1 `CategoryBoxTests` (`IsolateActsOnChecked`, `HalftoneActsOnChecked`, `NoCheckDisablesMenu`, `CheckEnablesMenu`, `SearchKeepsHiddenChecks`, `SetChecksBatchesOneChange`, `NoCheckReportsReason`, `IsolateShowsLinkNotice`, `EyeHidesCategoryEverywhere`, `EyeTipNamesLinks`) |
| CB-03 | Box lists the checked categories, uncheck removes the group row, eye hides, menu lists Isolate, Halftone, Color; Isolate runs on two checked rows, menu off without checks; selecting a row checks only it, Add keeps both, Remove unchecks, the box keeps the others, rows are never painted selected, All selects every row; live: select and add check exactly two | UI state | Views `ExplorerViewTests` (`CategoriesBoxListsChecked`, `UncheckRemovesGroupRow`, `CategoryEyeHidesCategory`, `CategoryMenuListsActions`, `IsolateRunsOnChecked`, `MenuOffWithoutChecks`), `CategorySectionTests` (`SelectChecksOnlyRow`, `AddKeepsBothChecked`, `RemoveUnchecksRow`, `BoxCheckKeepsOthers`, `SelectedRowIsPlain`, `AllSelectsEveryRow`); L3 UI-11 |
| CB-04 | Box tree: parent check and select check the children, a child uncheck shows the partial mark, a partial click checks all, hand-checked children check the parent, search for a child opens its parent, collapse keeps checks; live on the Site link (Toposolid › Sub-divisions); the box eye on a link category | UI state | Views `CategoryTreeViewTests` (12, Explorer and Filter); L3 UI-09, UI-10, UI-13 |
| CB-05 | Explorer nests a subcategory row under its parent; the parent counts own + children | Site: Toposolid 10, Sub-divisions 8 | L1 `SubcategoryRowTests`; L3 UI-12 |
| CB-06 | Hide unchecked lists only checked rows (a root stays for a checked child), combines with search, an uncheck drops the row, off lists all again; the toggle is in both boxes and shrinks the list | VisibleItems, UI rows | L1 `CategoryBoxTests` (`HideUncheckedListsCheckedOnly`, `HideUncheckedCombinesWithSearch`, `UncheckDropsHiddenRow`, `HideOffListsAllAgain`), `CategoryNestingTests.HideKeepsParentOfCheckedChild`; Views `CategorySectionTests.HideUncheckedShrinksList` (Explorer and Filter) |
| RB-01 | Filter result bar: one line, the mode and the Selection sync toggle never overflow and stay fully drawn left of the overflow arrow (*matches* hides first), tools overflow from the end and run from the overflow | layout | Views `ResultBarViewTests` (`BarStaysOneLine`, `WideBarShowsAllTools`, `NarrowToolsOverflow`); L3 UI-14 |
| KB-01 | Z, I, H on the selected tree row run Zoom, Isolate, Hide (key raised on the row, offscreen); the menu shortcut resolves its item | plan, zoom | Views `LetterKeyRunsOnRow`, L1 `ShortcutZoomsTarget`; live (Revit 2026, Snowdon HVAC): a mouse click then Z did not zoom while the DevTools pane re-showed on every log line (focus moved to `MainPage`); with `PanelController` showing it only when not shown, click then Z zoomed. No automated test: the cause is `DockablePane.Show()` focus in Revit |
| DC-01 | Double-click an instance row selects only it and runs Select + Zoom on its element; double-click a category, family or type row opens and closes it with no zoom; a double-click on the chevron, eye or a row tool does only that button | selection, zoom, expand | L1 `OpenInstanceSelectsAndZooms`, `OpenGroupTogglesExpand`; Views `DoubleClickZoomsInstance`, `DoubleClickExpandsGroup`, `DoubleClickOnEyeOnlyHides` |
| HR-01 | Active view: group counts its hidden elements (plain *+N hidden*, warning color, row text color when selected), Whole model has none, an expand lists hidden children dimmed with the Hidden eye in id order, a fully hidden branch is listed | counts, rows | L1 `HiddenRowsTests` (`GroupCountsHiddenElements`, `WholeModelHasNoHidden`, `ExpandListsHiddenDimmed`, `FullyHiddenBranchListed`), Views `HiddenCountShowsInActiveView`, Sandbox `--explorer-shots` (`*-explorer-hidden-selected.png`) |
| HR-05 | Row layout at the docked width (320): a selected Type row with *+N hidden* keeps at least 24 px of its name and a whole eye; the tools replace the counts. Sandbox fake explainer names the fake view's cause for a dimmed row (the shots show a real cause) | bounds, cause | Views `SelectedRowKeepsNameAndEye` (fails without the swap: name 9 px), L1 `FakeCauseMatchesDimmed`, Sandbox `--explorer-shots` |
| HR-06 | A selected dimmed row is drawn at opacity 1 with the Hidden eye; an unselected dimmed row stays at 0.55 | opacity | Views `SelectedDimmedRowIsFull` (fails without the setter: 0.55), Sandbox `--explorer-shots` |
| HR-03 | *Why hidden?* only for a hidden element: disabled with *Shown in this view.* on a shown Active view element, enabled on a dimmed row and after the panel hides the element | menu state | L1 `HiddenRowsTests` (`WhyHiddenOffForShown`, `WhyHiddenOnForDimmed`, `WhyHiddenOnAfterHide`) |
| HR-04 | Railing parts drawn with their railing count as shown: the panel's Active view ids and quick counts include them, the oracle agrees | ids, counts | L2 `WhyHiddenTests.RailingPartNamesRailing` (WH-12), `ExplorerCountTests` (SC view ids and counts against `Oracle.ViewIds`) |
| HR-02 | Group Select and Hide skip hidden elements, a dimmed row acts alone and sets the Why hidden element, search finds hidden elements | ids | L1 `HiddenRowsTests` (`GroupSelectSkipsHidden`, `GroupHideSkipsHidden`, `DimmedRowActsAlone`, `DimmedRowSetsWhyElement`, `SearchFindsHiddenElement`) |
| PI-01 | Pick is off with no checked link or no checked category and on with a link; it sends the checked links only, skips the port with no link | enabled, args | L1 `PickTests` (`NoLinkDisablesPick`, `NoCategoryDisablesPick`, `LinkEnablesPick`, `PickSendsLinksOnly`, `NoLinkSkipsPort`), `ExplorerPickTests`; Views `PickOffWithoutLink`, `PickOnWithCheckedLink` |
| PI-02 | The picked set becomes the selection, one per element; Esc keeps the selection; empty pick says *Nothing picked*; a second pick is blocked; closing during a pick is quiet; with sync on the status line carries the count (no *Picked N*), with sync off *Picked N* counts distinct elements | state, status | L1 `PickTests` (`PickedBecomeSelection`, `EscapeKeepsSelection`, `EmptyPickSaysNothing`, `PickCountsEachElementOnce`, `SecondPickIsBlocked`, `CloseDuringPickIsQuiet`, `PickingRaisesChanged`); `ExplorerPickTests` (`PickUsesCheckedBoxes`, `SyncOffPickShowsCount`, `PickShowsCueUntilDone`, `SelectionScopeKeepsPick`); the adapter's one-reference-per-element `SetReferences` needs a person (MP-01) |
| PI-03 | `LinkPickFilter` for the Architectural link and Doors on real link references: its door passes, its wall, a door of another link and a Current model duct do not, the link instance does | door only | L2 `PickFilterTests.FilterKeepsCheckedOnly` |
| MP-01 | Pick in a real view: click and box a linked element without Tab, only checked links and categories, Esc ends it. Needs a person; recorded in the plan, not automated | manual | [link actions plan](../../plans/active/2026-11-20-models-context-link-actions.md) (passed 2026-11-23) |
| SE-01 | One engine: a branch Hide matches Select, scoped hide and show keep the eye right, a filter result reads hidden state, a filter Color marks the Explorer row, one instance leaves its group plain, a link row Halftone marks the row | plans, marks | L1 `SharedEngineTests` |

### Filter operators per value kind

Doors, Walls and Stairs are in the Architectural link; Ducts and Mechanical
Equipment in the Current model.

| ID | Kind, storage | Rule | Expected | Test |
|----|---------------|------|----------|------|
| TX-01 | Text, instance built-in | Doors Mark = "s10" | 1 | L2 matrix, L3 UI-01 |
| TX-02 | Text, Aa on | Mark = "s10" | 0 | L2 matrix, L3 UI-01 |
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
| NU-03 | Number with unit | Swing Angle > 45° | 130 | L2 matrix |
| NU-04 | Number with unit | Swing Angle = 90° | 130 | L2 matrix, L3 UI-03 |
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
| EN-02 | Enum on the type | Function ≠ Exterior | 817 | L2 matrix |
| EN-03 | Enum on the instance | Walls Location Line = Wall Centerline | 333 | L2 matrix |
| EN-04 | Enum, unknown name | Function = "No Such Function" | 0 | L2 matrix |
| EN-05 | Enum, unknown name | Function ≠ "No Such Function" | 1,120 | L2 matrix |
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
| GR-02 | OR: Mark = S10 or Swing Angle undefined | 12 | L2 matrix, L3 UI-02 |
| GR-03 | Nested OR inside AND (Walls) | 96 | L2 matrix |
| GR-04 | No rules: every checked element | 142 | L2 matrix |
| GR-05 | Empty nested group is true | 142 | L2 matrix |
| GR-06 | Nested AND inside OR | 68 | L2 matrix |
| GS-01 | Group categories narrow the parent (Doors / Walls) | 433 | L2 matrix |
| GS-02 | Group documents pick per file (Arch Doors / HVAC Ducts) | 1,195 | L2 matrix |
| GS-03 | Unresolved source under OR is skipped | 1 | L2 matrix |
| GS-04 | Unresolved source under AND matches nothing | 0 | L2 matrix |
| GS-05 | Group naming only the Current model, AND parent, link checked | 0 | L2 matrix |
| GS-06 | Nested category scopes intersect | 683 | L2 matrix |
| GS-07 | Connector, scope lists, warning, moves, collapse | state | L1 `FilterLogicTests`, `FilterTreeMovesTests`, Views `FilterLogicViewTests` |
| GS-08 | Drop decisions: up, down, onto itself, into, out of and across nested groups, group into its own descendant, zones with hysteresis, group header vs rules area, empty group, preview lifetime | effects, order, line | Views `FilterDropTests` |
| GS-09 | Drag preview, line and drop end to end | order changes, line shown | Sandbox `--drag-shots` in process; `--drag` real mouse is opt-in only ([ui.md](ui.md#how-drag-is-tested)) |
| GS-10 | Scope tags stay inside their box at 304 and 600 px, nested groups | bounds | Views `ScopeTagsStayInsideBox` |

### Case-sensitive rules

TX-02 to TX-05, TX-18, TX-19 above; widening and exact pass also in L1
`CaseSensitiveTests`.

### Selection sync, recount, edits

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| LV-01 | Edit runs once after 300 ms | one selection | L1 `SyncDebounceFiresOnce` |
| LV-02 | Model edit recounts, never selects | no selection sent | L1 `ModelEditRecountsWithoutSelecting` |
| LV-03 | Value edit (3 ducts get a comment), cache dropped as `DocumentChanged` does | 0 → 3 | L2 `ValueEditRecounts` |
| LV-04 | Copied duct: candidates stay until the cache drops (version does not change per transaction) | n → n, then n + 1 | L2 `AddedElementCountsAfterRefresh` |
| LV-05 | Selection sync pauses above 5,000 | paused, nothing sent | L1 `ResultSyncPausesAbove5000` |
| LV-06 | Selection sync pause shows in the panel: the `Filter.SelectionSync` toggle unchanged, line *Paused: over 5,000 matches*; the line goes away under the limit, with no categories, with no filter, with selection sync off | SC-02 > 5,000 | L1 `PauseEnds*` (logic), Views `PausedLineShowsOverLimit`, `PausedLineHidesUnderLimit` (binding), L3 UI-04 |
| LV-07 | Add / Remove keep the baseline | selection | L1 `AddModeKeepsBaseline` |
| LV-08 | Readiness: edit Busy, run Idle, generation grows | status | L1 `ActivityTests`, Views `ActivityViewTests` |

### Custom filters

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| CF-01 | New filter gets next free name, rename, unique, trimmed, Esc | state | L1 `FilterTabTests` (`NewFilterGetsNextName`, `NameMustBeUnique`, `EscRestoresLastName` …) |
| CF-02 | Switch with unsaved changes asks | pending | L1 `SwitchAsksWhenUnsaved` |
| CF-03 | Store round trip, scope kept, no `not` | equal | L1 `StoreKeepsScopeWithoutNot`, `CustomFilterLibraryTests` |
| CF-04 | Save, switch away, reload: same count | NU-04 | L3 UI-03 |

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
| PK-10 | Value counts of Swing Angle on Doors | total 142, values 131 | L2 `CatalogMatchesFacts(PK-10)` |
| PK-11 | Typing filters, ≤ 100 opens, tags, SetValue picks | UI state | Views `FilterViewTests` (`PickerTypingFiltersIgnoringCase`, `PickerValueSetsParameterName` …) |
| PK-12 | Value pattern picks a parameter in Revit | "Swing Angle" | L3 UI-03 |
| PK-13 | Instance / Type sections with row counts, header not a list item and not selectable, sticky on scroll, exact name first; a header click collapses its section, the choice survives closing the picker, a search shows every section | UI state | Views `PickerGroupsBySection`, `HeadersAreNotListItems`, `HeaderIsNotAnItem`, `HeaderStaysOnScroll`, `CollapsedSectionSurvivesReopen`, `SearchShowsCollapsedSections`; HandyControl `ExactMatchComesFirst` |
| PK-14 | Row: name and source icon per origin (none for built-in); categories only in the tooltip; no tags or warning colors | UI state | Views `RowIconFollowsSource`, `RowShowsNameOnly`, `RowHasNoTagsOrWarnings` |
| PK-15 | Tooltip rows present only with a value: built-in measured, built-in text, shared, project, family, yes/no; five then "+ N more" | rows | L1 `ParameterTipTests`, Views `TipShowsRowsAndLists` |
| PK-16 | Rule card: placeholder in the Current model's format, "Type" for a type parameter | UI state | L1 `PlaceholderUsesCurrentModel`, `TextHasNoPlaceholder`; Views `ValueBoxShowsExample`, `ChosenTypeParameterShowsType` |

### Visibility in the active 3D view (L2 `VisibilityMatchesApi`, `visibility.json`)

Each case activates `{3D}` through `UIDocument.ActiveView`, builds the plan the
panel sends, runs it through `RevitViewActions`, reads the view state through
the API, resets, and checks the reset. Images: the view before, after and after
reset. VI-09 to VI-12 keep their ids and data wording "All models"; the row is
now the category in the Categories box (its eye and menu), same T2 plan.
Category Isolate (VI-10, VI-12) hides every other hideable category inside
Temporary View Properties; the leftovers each case allows are categories Revit
never hides in a view. Annotation in a plan was checked by the spike only
(L3 plan: tags and grids gone), not by an L2 case. L1 `CategoryBoxTests` and the view
tests `CategorySectionTests` (`SelectChecksOnlyRow`, `AddKeepsBothChecked`, `BoxCheckKeepsOthers`)
cover check = selection.

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
| VI-09 | T2 Categories box Ducts | Hide | Temporary View Properties on, no temporary hide/isolate (category visibility, Gotcha 30); ducts hidden, terminals visible | L2 |
| VI-10 | T2 Categories box Ducts | Isolate | Temporary View Properties on, no temporary hide/isolate; Current model and every By Host View link show only ducts (center lines count as Ducts) besides duct systems, link instances and the case's leftovers; Reset restores every count | L2 |
| VI-11 | T2 All models Ducts | Color | category override colored, cleared by Reset | L2 |
| VI-12 | T2 Categories box Doors (in links) | Isolate | Current model and every By Host View link show only Doors (141, unchanged), link instances and the leftovers Cameras, Model Groups, Divisions, Multistory Stairs; Reset restores every count (Architectural 7,823 -> 183 -> 7,823) | L2 |
| VI-13 | T3 filter result GR-02 (link doors) | Hide | planned as T3, not run (Gotcha 13) | L2 |
| VI-14 | Linked ids of GR-02 without a rule | Color | refused: "Revit can color linked elements only by family, type or a rule filter." | L2 |
| VI-15 | T0 + T3 filter result GS-02 | Hide | planned as T0 + T3, not run | L2 |
| VI-16 | T4 link door type row | Color | `Model Context <n>` filter on the view with red, Temporary View Properties on; Reset deletes it and ends the mode | L2 |
| VI-17 | T0 ducts + T4 link door type | Halftone | element overrides and the panel filter halftone; Reset clears both | L2 |
| VI-18 | T4 link door type repeated 51 times | Color | refused: "Over 100 rules to color in a link.", view unchanged | L2 |
| VI-19 | T3 isolate GR-02 (link doors) | Isolate | planned as T3, not run (Gotcha 13) | L2 |
| VI-20 | T0 + T3 isolate GS-02 | Isolate | planned as T0 + T3, not run | L2 |
| VI-21 | T3 isolate rest, every loaded link in {3D} | Isolate | `LinkIsolation.RestOf` holds every linked Level and Scope Box the view draws and no Model Group; not run (Gotcha 13) | L2 `IsolateRestHoldsLinkedDatums` |

### Subcategories, parent rule, category actions, linked-hide record (L1; L2 `CategoryActionTests`, `category-actions.json`)

The L2 cases need `{3D}` active from a run of its own (Gotcha 21; `user-action-activate-view.cs` with
`ViewName` = `{3D}`), every link By Host View, and the Site link loaded. The full run activates `L3`, so they end
Inconclusive there; run `--filter CategoryActionTests` alone after the MCP view switch. CA-07 sets the Architectural
link to Custom inside a transaction group that it rolls back.

Product behaviors (cleanup audit numbering) and their rows: B20 subcategory rows SU-01, CA-01; B21 Site, Lines,
Topography left out CA-10; B30 box eye reaches links CA-02, HC-04, CA-07; B31 box menu on checked rows CB-02,
CA-05; B32 category Isolate VI-10, VI-12; B33 Reset and the own-settings notice HC-04, CA-07; B34 parent
overrides children PO-01, CA-03, CA-08, CA-09; B35 child Show refused PO-02, CA-04; B37 hidden category keeps
its count HC-01; B38 hidden in Whole model then Active view HC-02; B45 link children through Hide in View LH-01,
LA-02, UI-06; B46 link isolate refusals LA-03; B54 saved-hide dot LH-03; B55 one record of linked hides LH-01,
LH-02; model sync relay MS-01.

| ID | Use case | Expected | Test | Status |
|----|----------|----------|------|--------|
| CA-10 | No group under Site, Lines or Topography (points, sketch lines, contours) and no Center line group, in the Current model and the Site link | none | L1 `SubcategoryRuleTests`; L2 `NoisySubcategoriesLeftOut` | pass (run `20261125-010601`) |
| EY-01 | The eye counts only what the active view can draw: a Hide that reaches every drawn element of the row reads Hidden, Current model and link rows alike; Show and Reset clear it | Hidden, then Shown | L1 `EyeDrawnTests` (`LinkHideIgnoresUndrawn`, `CurrentHideIgnoresUndrawn`, `HiddenEyeShowsAgain`, `ResetForgetsUndrawn`); L3 UI-06 | pass |
| LA-03 | Link isolate refusals: Reveal Hidden Elements on, a view that cannot show links, over 10,000 linked elements to hide; the sentence shows, nothing is journaled, no eye changes | refusal text, state unchanged | L1 `LinkIsolateRefusalTests` (`RefusedIsolateChangesNothing` ×3, `TooManyNamesTheCap`); the adapter checks run before any Hide in View, so L2 does not run them (Gotcha 13) | pass |
| MS-01 | Model sync relay: a sync of the active model whose snapshot already reflects the version keeps it; a missed change rebuilds it and the counts follow; a sync of another model is ignored | builds, counts | L1 `ModelSyncTests` (`SyncKeepsReflectedSnapshot`, `SyncRebuildsMissedChange`, `SyncOfOtherModelIgnored`) | pass |
| CA-01 | Site link snapshot: Toposolid and its subcategory Sub-divisions, plain name, parent id | 2 + 8, parent Toposolid | L2 `SubcategoryCollected` | pass (run `20261125-010601`) |
| CA-02 | Box Hide of Walls reaches By Host View links (category visibility in Temporary View Properties) | Walls 0 in every view collector; Reset restores every count | L2 `BoxHideReachesLinks` | pass (run `20261125-010601`) |
| CA-03 | Hide Toposolid with Sub-divisions in the Site link | 10, then 0, then 10 | L2 `ParentHideCoversChild` | pass (run `20261125-010601`) |
| CA-04 | Revit fact behind the child Show refusal: Toposolid hidden alone takes Sub-divisions out too | Sub-divisions 0 | L2 `HiddenParentHidesChild` | pass (run `20261125-010601`) |
| CA-05 | Box Color is a category override in Temporary View Properties; linked walls checked on the images | override set, cleared by Reset | L2 `BoxColorIsCategoryOverride` | pass (run `20261125-010601`) |
| CA-06 | Keep with a category hide: the mode stays, Walls stay hidden, Reset ends it | Walls 0 after Keep; counts back after Reset | L2 `KeepLeavesCategoryHide` | pass (run `20261125-010601`) |
| CA-07 | A Custom link is not reached; the notice says so | *Links By Linked View or Custom keep their own settings.* | L2 `OwnSettingsLinkNotice` | pass (run `20261125-010601`) |
| CA-08 | Color on the Site link's Toposolid row: one panel filter with both category ids | {Toposolid, Sub-divisions}; none after Reset | L2 `ParentLinkFilterHasBoth` | pass (run `20261125-010601`) |
| CA-09 | Latest wins between panel view filters | parent on top, then child on top; none after Reset | L2 `LatestFilterOnTop` | pass (run `20261125-010601`) |
| SU-01 | Subgroup data: plain name, `ParentId`, parent and child rows, uncheck, child-only Isolate, link Hide counts | data, plans | L1 `SubcategoryRowTests` (7) | pass |
| PO-01 | Parent overrides children for Color, Halftone, Hide, select; Current model and link; latest wins; Reset clears both; box parent covers children | plans, marks | L1 `ParentOverrideTests` (14) | pass |
| PO-02 | Child Show refused while its parent is hidden | *Show its parent category first.* | L1 `ParentOverrideTests.ChildShowWaitsForParent`; L2 CA-04 | L1 pass |
| HC-01 | A category the panel hid (Hide or Isolate) keeps its Active view count and reads hidden; Current model, link, subcategory; Reset | counts unchanged | L1 `HiddenCategoryCountTests` (`HiddenCategoryKeepsCount`, `LinkHiddenCategoryKeepsCount`, `SubcategoryKeepsCountUnderParent`, `IsolateKeepsOtherCounts`, `ResetKeepsCount`) | pass |
| HC-02 | Category hidden in Whole model, then Active view | the view's own count, hidden | L1 `WholeModelHideThenActiveView` | pass |
| HC-03 | Keep with a category hide keeps the eye hidden; Show after Keep works | state | L1 `CategoryHideTests`; L2 CA-06 | L1 pass |
| HC-04 | Box action with a link By Linked View or Custom reports the notice | message | L1 `CategoryHideTests.EyeHideShowsLinkNotice`; L2 CA-07 | L1 pass |
| LH-01 | One record of linked hides (the journal): eye, bar count, Show, Reset, rename, view switch, isolate rest shown by Show, an explicit hide outlives it | agree | L1 `LinkHideRecordTests` | pass |
| LH-02 | Typed failure when Revit shows no linked element (`LinkedNotShownException`) | *linked stay hidden* message | L1 `LinkHideRecordTests.UnshownTypedFailureReports`, `PanelBarsTests` (`MenuRefusalShowsInPanel`, `ResetDropsUnshownOnce`, `EyeOnUnshownRestReports`) | pass |
| LH-03 | Active view, linked hide (T3): the row keeps its count, eye Hidden, so the saved-hide dot shows; after Keep the hides leave the bar and Reset, the eye and the dot stay, and the eye shows them again | count, eye, `IsHiddenInView`, dot pixels | L1 `LinkHideRecordTests.ActiveViewLinkHideKeepsRow`, `PanelBarsTests` (`KeepTakesLinkHidesOffBar`, `KeptLinkHideKeepsEyeAndDot`, `ResetLeavesKeptLinkHides`, `EyeShowsKeptLinkHide`), `JournalTests.KeepClearsConvertedChanges`; L3 UI-21 | L1 pass |
| TV-01 | View framing when Temporary View Properties turns on and off | same zoom corners and camera | MCP probe on `{3D}` (Revit 2026, 2026-11-24): zoom corners, eye and direction read before, after the mode on, after it off, after the panel's box eye Hide and after its Show; all five equal | not reproduced |
| BZ-01 | Edit mode: a view action stops with *Revit is busy.*; during a modal command a request waits, then runs (product: Layout) | message, then the run | none | open |

### Why hidden and nested families

Why hidden reads the active view only. The L2 cases build the cause in the
Snowdon model, ask `RevitVisibilityExplainer`, and read the cue; each case uses
the safety guard before and after. WH-01 to WH-06, WH-09 and WH-11 to WH-15 passed live on 2026-11-25
(Current results). The architecture README holds the message catalog.

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| WH-01 | Category off in the active view | *Category off: <category>* | L2 `WhyHiddenTests.CategoryOffNamesCategory` |
| WH-02 | Element hidden in the active view | *Hidden in view*, no Show | L2 `HiddenElementNoFix` |
| WH-03 | View filter with visibility off | *Filter: <name>* | L2 `ViewFilterNamesFilter` |
| WH-04 | Linked L4 door in the L3 plan (its bottom on the top clip) | *Above view range* | L2 `LinkedDoorAboveCutPlane`, Explicit: run alone after `user-action-activate-view.cs` with `ViewName` = `L3` (the full run stays in `{3D}`) |
| WH-05 | Element of a secondary design option | *Design option: <option>* | L2 `SecondaryOptionNamesOption` |
| WH-06 | Element visible in the active view | *No view setting hides it* | L2 `VisibleElementHasNoCause` |
| WH-07 | Cue text, check order, one cause per kind, *No view setting hides it* only alone, not-drawn, nested-family, ceiling, section-depth and railing cues, *Revit does not draw it in this view* is a found cause, journaled linked hide first, only Show is a fix | list | L1 `WhyHiddenTests` (headless, fake explainer) |
| WH-08 | Strip: open shows the first cause, follows the selected row, closes (✕, Esc, no row), newer open cancels older, an error becomes one cue, Show re-explains, category cue has no Show, menu needs one element | state | L1 `WhyHiddenStripTests`; Views `WhyHiddenViewTests` (strip hidden until opened, expand lists causes, Show and ✕ and Esc, sits above the Temporary bar) |
| WH-09 | Hidden linked elements of each loaded link in the L3 plan (panel rule: drawable in this view type, not shown) | a cause for each sampled element (never *No view setting hides it*); the evidence lists the first causes | L2 `LinkedHiddenAllHaveCause`, Explicit: run alone after `user-action-activate-view.cs` with `ViewName` = `L3` |
| WH-10 | Hidden counts skip sheets, no-geometry, view-owned (other view or any in a link), rooms and spaces in 3D, areas outside area plans | outside ids | L1 `HiddenRuleTests` |
| WH-11 | Element whose top touches the view depth (rolled back) | *Below view depth* | L2 `TouchingViewDepthNamesRange`, Explicit: run alone with `L3` active |
| WH-12 | Up to 10 balusters or supports left out of the view collector while their railing is drawn | none reads a fallback; one reads *Drawn with its railing* (the others name their view range or crop) | L2 `RailingPartNamesRailing` |
| WH-13 | Demolished element that Show All still draws (rolled back) | *No view setting hides it* | L2 `DrawnDemolishedHasNoCause` |
| WH-14 | Element of a link that the active view hides and never drew; Revit keeps running | *Link hidden in view* | L2 `HiddenLinkNamesLink`, Explicit: run alone on a copy of Snowdon Architectural with `{3D}` active |
| WH-15 | Wall behind the section line, and one beyond the far clip | *Behind the section line*, *Beyond far clip* | L2 `SectionDepthNamesSide`, Explicit: run alone on a copy of Snowdon Architectural with `Longitudinal Section` active |
| NE-01 | Current model table 738550 with nested chairs (or the first instance in the view with nested instances): Isolate, Hide, Halftone and Color through `RevitViewActions` reach each `GetSubComponentIds` chair; Reset shows them and restores their overrides | chairs follow the host | L2 `NestedFamilyTests.NestedChairsFollowHost`, Explicit: run alone on a copy of Snowdon Architectural as the active model with `{3D}` active (MCP `open_document`, then `user-action-activate-view.cs` with `ViewName` = `{3D}`) |

### User actions (L2 `UserActionTests`, `user-actions.json`)

Adapter level, Snowdon HVAC with the Architectural link. Each step records its time and
checks panel = oracle. Edits run in rolled-back transactions; no document is opened inside
a run. Open views of every type come from `scripts/models-context/user-action-open-views.cs`
(MCP); a missing type is Inconclusive.

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| US-01 | Value edit on three ducts, cache dropped, recount | 0, then 3 = oracle | L2 `EditedValueRecounts` |
| US-02 | Copied duct recount | n, then n + 1 = oracle | L2 `CopiedDuctRecounts` |
| US-03 | Deleted ducts recount | lower = oracle | L2 `DeletedDuctsRecount` |
| US-04 | Duct changed to a type of another family, Family Name rule | n, then n - 1 = oracle | L2 `ChangedTypeRecounts` |
| US-05 | Edit rolled back with its transaction group (API form of Undo) | 0, 3, 0 = oracle | L2 `UndoneEditRecounts` |
| US-06 | Active plan (MCP `user-action-activate-view.cs`, then the explicit test alone): Active view counts, both documents | panel = view collector | L2 `ActiveViewFollows` (explicit) |
| US-07 | Active 3D view (MCP `user-action-activate-view.cs`, then the explicit test alone) | panel = view collector | L2 `ActiveViewFollows` (explicit) |
| US-08 | Active sheet (MCP `user-action-activate-view.cs`, then the explicit test alone): link gives 0, `InView` null | Current model = collector, link 0 | L2 `ActiveViewFollows` (explicit) |
| US-09 | Active schedule (MCP `user-action-activate-view.cs`, then the explicit test alone) | same as US-08 | L2 `ActiveViewFollows` (explicit) |
| US-10 | Active drafting view (MCP `user-action-activate-view.cs`, then the explicit test alone) | same as US-08 | L2 `ActiveViewFollows` (explicit) |
| US-11 | Active legend (MCP `user-action-activate-view.cs`, then the explicit test alone) | same as US-08 | L2 `ActiveViewFollows` (explicit) |
| US-12 | Eight evaluate requests, each canceling the last | last = oracle, fresh count = oracle | L2 `RepeatedRequestsLastWins` |
| US-15 | After an MCP reload of Reload-Link: counts of every link, Snapshot reload outcome by version rule | panel = oracle; Kept, Patched or Rebuilt | L2 `ReloadedLinkMatchesOracle` (explicit, after `user-action-reload.cs` or `user-action-reload-same-path.cs`) |

| US-13 | Real Undo and Redo of a committed edit (MCP steps, live panel) | 0, 3, 0, 3, 0 | MCP + L3 `LiveStepTests` |
| US-14 | Pane hidden during a committed edit, then shown | 3 after one resync | MCP + L3 `LiveStepTests` |
| US-16 | Model switch and back | the other title, then HVAC with its filter | MCP + L3 `LiveStepTests` |
| US-17 | Hide, Isolate, Halftone, Color, Reset and Keep in a view whose template controls V/G (host elements, link instance, link category everywhere) | ok or one refusal; Reset restores overrides, no temporary mode, same template, model unmodified | L2 `LockedTemplateActionsStaySafe` (explicit) |
| US-18 | T4 Color on an Architectural door type in a plan whose template controls Filters (L3) | panel filter inside Temporary View Properties with red; Reset leaves no `Model Context <n>` filter, no mode, same template | L2 `LockedTemplateLinkColorDraws` (explicit) |

Safety guards run in every L2 case (`SafetyGuard`) and after L2 and L3 in the script
(`safety.json`).

### Live steps (L3 `LiveStepTests`, `live-steps.json`)

US-13, US-14 and US-16 need the live panel and a Revit step between reads, so each
read is its own L3 run. `LiveStepTests` is skipped unless `MODELS_CONTEXT_LIVE_STEP=1`,
so the script's full L3 run never runs it.

| ID | Step | Expected | Test |
|----|------|----------|------|
| LS-01 | Build Ducts + Comments equals `mc-live-step` (Whole model, Current model checked) | a count | `BuildsStepFilter` |
| LS-02 | Settle, read `Filter.ResultCount` | `MODELS_CONTEXT_EXPECT` | `CountIs` |
| LS-03 | Current model item of `ModelContext.Documents` (`ModelContext.Document`, title in HelpText) | starts with `MODELS_CONTEXT_EXPECT_MODEL` | `ShowsModel` |
| LS-04 | Checked items of `ModelContext.Documents` after an MCP view change (`user-action-activate-view.cs`) | the Current model plus each link the view draws = `MODELS_CONTEXT_EXPECT_DOCS` | `ChecksFollowView` |

```powershell
$env:MODELS_CONTEXT_LIVE_STEP = '1'; $env:MODELS_CONTEXT_REVIT_PID = '<pid>'
$ui = 'tests/RevitDevTool.ModelsContext.UiTests/RevitDevTool.ModelsContext.UiTests.csproj'
dotnet run --project $ui -- --filter BuildsStepFilter
$env:MODELS_CONTEXT_EXPECT = '3'; dotnet run --project $ui -- --filter CountIs
$env:MODELS_CONTEXT_EXPECT_MODEL = '<title>'; dotnet run --project $ui -- --filter ShowsModel
$env:MODELS_CONTEXT_EXPECT_DOCS = 'Current model,Architectural'; dotnet run --project $ui -- --filter ChecksFollowView
```

Step order (MCP steps are `execute_csharp_code` with the file in `scripts/models-context/`):

- **US-13:** `BuildsStepFilter` (0) → `user-action-edit.cs` → `CountIs` 3 → `navigate_history` undo 1 →
  `CountIs` 0 → redo 1 → `CountIs` 3 → undo 1 → `CountIs` 0.
- **US-14:** after US-13 (count 0): `user-action-pane.cs` (`Show = false`) → `user-action-edit.cs` →
  `user-action-pane.cs` (`Show = true`) → `CountIs` 3 → `user-action-edit.cs` (`Clear = true`) → `CountIs` 0.
- **US-16:** `open_document` of a second model (it becomes active) → `ShowsModel` with
  `MODELS_CONTEXT_EXPECT_MODEL` and `MODELS_CONTEXT_HOST_TITLE` set to its title →
  `user-action-switch.cs` (switches back to the HVAC model) → clear `MODELS_CONTEXT_HOST_TITLE`, `ShowsModel` with the
  HVAC title → `CountIs` 0 (the HVAC session kept its filter).
- **US-17:** `user-action-template-view.cs` (duplicates `L3` as `MC Template Locked` with a template
  that controls every parameter) → `user-action-activate-view.cs` with `ViewName = "MC Template Locked"` →
  L2 `--filter LockedTemplateActionsStaySafe` alone. The view and template stay until the model is closed
  without saving.

### Sweep (L2 `SweepTests`, `sweep.json`, explicit)

Panel = oracle on whatever model is active with all its loaded links (no titles, no golden
values), for the federated Snowdon copy, Golden Nugget and サンプル設備. Run each by its method
name; files land in the run folder.

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| SW-01 | Every category of every document, Whole model, and Active view in whatever non-template view is active (links count only in model views); a rolled-back itemized schedule per schedulable category of the active model as a second reference (links are not scheduled: they cannot be written). `MODELS_CONTEXT_SWEEP_SCHEDULES` caps the schedules (60) | panel = oracle; schedule differences with a reason in `sweep-counts.json` | `SweepCountsMatchOracle` |
| SW-02 | Every category with elements, every catalog parameter: Has value, Has no value, Equals the most common value, Contains a middle part (text), at least the median (numbers; the active model only, as `TreeBuilder`). Native Revit rule per document and, in the active model, a `ParameterFilterElement` per rule in one rolled-back transaction. `MODELS_CONTEXT_SWEEP_LIMIT` caps evaluations (6,000, spread evenly) | panel ids = oracle ids; native agreement by reason, unexplained rows in `sweep-mismatches.json` | `SweepParametersMatchOracle` |
| SW-03 | Seeded random trees over 1 to 3 of all loaded documents and all their categories; `MODELS_CONTEXT_SWEEP_TREES` limits the count (100) | panel = oracle; a failing tree in `sweep-tree-<id>.json` | `SweepTreesMatchOracle` |
| SW-04 | Cold, warm and invalidated snapshot, quick counts, catalog, a value list and a filter per document; longest call, memory per million | no Revit call over 1 s; ratings in `sweep-perf.json` | `SweepTimingsPerDocument` |

Runs with the [0064](../../decisions/0064-model-context-annotation-in-explorer.md) rule, Revit 2026, 2026-11-26:

- サンプル意匠 as the Current model (its link unloaded): SW-01 run `20261126-190145` PASS, Whole model 7,478 = 7,478,
  floor plan `平面詳図 7階` 818 = 818 (Dimensions, Wall, Door and Room Tags among them); schedules 22 of 28 equal, 6
  with a known reason. SW-04 run `20261126-190212` PASS: snapshot 17,325 rows, cold 291 ms (longest call 101 ms), warm
  108 ms; the same file as a link before 0064 read 11,087 rows, warm 48 ms.
- Golden Nugget Architektur as the Current model, floor plan `UG 1_Genehmigung` active, Gebäudetechnik link loaded:
  SW-01 crashed Revit (`ExecutionEngineException` in `WhereElementIsNotElementType`): `Oracle.ViewIds` built the link
  view collector for a link the view does not draw ([README](README.md#gotchas) Gotcha 1). The oracle now builds it
  only in a model view whose collector holds the link instance.
- After that fix, Revit 2026 PID 14484 with the window restored: SW-01 passes everywhere, panel = oracle and no crash.
  Golden Nugget (Whole model 5,121, link 6,971): plan `UG 1_Genehmigung` 431 with the hidden link 0 (run
  `20261126-191925`), section `WP-01` 486 (`-192012`), legend `Legende Entwurf` 13 (`-192047`), sheet `GOLDEN NUGGET`
  18 (`-192123`). サンプル意匠 (Whole model 7,478): section `断面図A` 533 (`-192237`), legend `05 壁リスト 全て` 283
  (`-192305`), sheet `展開図 1階` 31 (`-192332`).
- Deployed add-in with sliced snapshot ids: SW-04 on サンプル意匠 (run `20261126-195820`) has its longest snapshot call
  at 36.6 ms (was 101.3), no hitch over 50 ms; the snapshot takes 231 ms both cold and warm, in 9 calls. SW-01 on Golden
  Nugget `UG 1_Genehmigung` (run `20261126-195958`): 431 = 431, hidden link 0 = 0.
- Final deploy, one id pass again: SW-04 on サンプル意匠 (run `20261126-203920`) reads the snapshot in 103 ms cold,
  105 ms warm and 116 ms invalidated, in 6 calls; the longest snapshot call is 31 ms and no call in the run is over 50 ms.

### Robustness, headless and performance

| ID | Use case | Expected | Test |
|----|----------|----------|------|
| NF | Every case in `scopes.json` and `filters.json` also run as plain Revit rules (`ParameterFilterRuleFactory`, no panel fixes), and for the Current model as a `ParameterFilterElement` in a rolled-back transaction | panel = oracle; native differences listed with a reason in `native-comparison.json` | L2 `CompareWithNative` |
| RT | 200 seeded random trees from the real catalog and values (`random-filters.json`), depth up to 5, up to 50 rules, Aa, group scopes, whole model | panel = oracle; a failing tree is written as a ready case | L2 `TreeMatchesOracle` (`MODELS_CONTEXT_RANDOM_TREES` limits the count) |
| MC | A stored-format JSON filter run through the core ports with no panel view (`headless-filters.json`), and three evaluations started together | = panel and oracle; no exception | L2 `HeadlessFilterTests` |
| PF | Cold, warm and invalidated time of snapshot, quick counts, scope ids, catalog, value index and filter match per document; longest Revit call; memory per million (a model under 100,000 elements is judged as 100,000, so fixed name and type costs do not scale up); 20 repeats without memory growth (`perf-budgets.json`) | within budget and the `perf/2026.json` baseline; `perf.json` in the run folder | L2 `PerformanceTests` (`MODELS_CONTEXT_RECORD_PERF=1` records the baseline) |

### L3 flows (`PanelFlowTests`, `BoxAndBarTests`, `ui-flows.json`)

| ID | Flow | Expected | Test |
|----|------|----------|------|
| UI-01 | TX-01, then Aa on | TX-01, then TX-02 | `CaseToggleChangesCount` |
| UI-02 | Connector to OR (GR-02) | GR-02 | `ConnectorMakesOrGroup` |
| UI-03 | Build NU-04 (picker takes "Swing Angle"), save, new filter, reload | parameter "Swing Angle", NU-04, NU-04 again | `SavedFilterReloads` |
| UI-04 | All categories in the link (SC-02), then None | Selection sync on, line *Paused: over 5,000 matches*, then no line | `SyncPausesAboveLimit` |
| UI-05 | Eye on the link model row, Reset | bar "1 change", then no bar | `LinkHideShowsTemporaryBar` |
| UI-06 | Eye on Doors under the link (T3, Hide in View), Reset | eye Hidden and bar "1 change", then eye Shown and no bar | `LinkDoorsHideThenReset` |
| UI-07 | Isolate tool on the first door type under the link (part A), Reset | bar with a count, no error, type eye Shown, another link category Hidden; then Shown and no bar; Isolate and Reset times | `LinkTypeIsolateThenReset` |
| UI-08 | Color tool on the first door type under the link, first picker swatch, Confirm (part B), Reset | bar with a count, no error, row color `#F44336`; then no color and no bar; Color and Reset times | `LinkTypeColorThenReset` |
| UI-09 | Box tree on the Site link: check Toposolid, open it, uncheck then check Sub-divisions, its only child (CB-04) | Sub-divisions On; Toposolid Off; then On | `BoxTreeChecksChildren` |
| UI-10 | Box search "Sub-div" with Toposolid closed (CB-04) | rows Toposolid, Sub-divisions; chevron open | `BoxSearchOpensParent` |
| UI-11 | Box None, select Walls, add Doors (CB-03) | exactly Walls and Doors checked | `BoxRowsSelectMany` |
| UI-12 | Only Toposolid checked, expand the Site link and Toposolid (CB-05) | Toposolid 10, Sub-divisions 8 | `SiteNestsSubdivisions` |
| UI-13 | Box eye on Walls with only the Architectural link checked, Reset (CB-04, B30) | eye Hidden, bar with a count; then eye Shown, no bar | `BoxEyeHidesLinkWalls` |
| UI-14 | New filter, Categories All, at the docked width (RB-01) | one line; sync toggle inside the bar, left of the arrow; every tool in the bar or behind the arrow | `ResultBarKeepsSync` |
| UI-15 | Explorer sync off, Filter sync off then on above 5,000 (EX-11, LV-06) | Filter stays On; paused line gone with it off, back on; Explorer stays Off | `SyncTogglesAreSeparate` |
| UI-16 | Eye on Ducts (Current model), select a duct instance row, close the strip (WH) | *Temporary hide/isolate* with Show; closed | `WhyHiddenNamesTempHide` |
| UI-17 | Eye on Doors under the link, Keep, Cancel, Reset (B61) | *… link elements stay hidden in this view*; Cancel keeps the bar; Reset clears it | `KeepAsksForLinkHides` |
| UI-18 | Row context menu on Doors under the link (Shift+F10), Hide, Reset (B56) | menu lists Zoom, Isolate, Hide, Halftone, Color, Why hidden (Select only while sync is off); eye Hidden, bar with a count; then no bar | `RowMenuHidesLinkDoors` |
| UI-19 | Categories box menu on checked Walls (Shift+F10): Isolate, Reset, Halftone, Reset (B31) | bar with a count, then no bar, both times | `BoxMenuIsolateHalftone` |
| UI-20 | Result row menu (Shift+F10) on TX-01: Hide, Reset (B91) | TX-01; bar with a count; then no bar | `ResultMenuHidesMatches` |
| UI-21 | Active view: eye on Doors under the link, Keep and confirm, then the eye again (LH-03, B54) | no dot; dot and eye Hidden after Keep; no dot and eye Shown after Show. The dot (an `Ellipse`, no UIA peer) is counted in a PrintWindow capture of the row between `Explorer.Row.Count` and `Explorer.Row.Eye`, pixels near HandyControl `WarningColor` read from `ColorsDark.xaml` | `SavedDotFollowsKeep` |

UI-09 to UI-17 change only temporary state and reset it; UI-09, UI-10 and UI-12 need the Site link loaded
(Inconclusive otherwise). WPF does not implement UI Automation `ShowContextMenu`, so the context menus (UI-18 to
UI-20) open with one Shift+F10 on the focused row (`RevitPanel.MenuItem`; it takes the keyboard focus for that
moment, one more reason L3 is opt-in). The menus themselves are covered without focus by the L1 view tests, which
open them offscreen (`PanelUi.OpenRowMenu`, `MenuListsActions`, `CategoryMenuListsActions`). The menu is a popup
window of Revit's process; the item is found by
AutomationId (`Menu.<Action>`, `Explorer.CategoryMenu.<Action>`) and run by its Invoke pattern, which closes the menu.
`RevitPanel.MenuItems` lists the items through the raw view walker: the control view of this menu leaves out its
first visible item (Zoom), which the popup capture (`menu` image, the popup window itself) shows on screen.

## Mismatches found

Open, Revit 2024 (run `20261118-093432`, pid 45728, 2026-11-18). L2 127 of 131
passed; L3 5 of 6 after the helper fixes below (UI-01 failed once while the
desktop was in use, then passed alone):

| ID | Expected | Oracle | Panel | Cause |
|----|----------|--------|-------|-------|
| SC-02 | 7,746 | 7,700 | 7,700 | The 2024 Architectural sample has fewer elements; panel = oracle. Per-version golden values to decide |
| SC-06 | 780 | 765 | 765 | Same sample difference (active view through the link); panel = oracle |
| SB-01 | 0 failures | 11,612 samples | 4 failed | Structural link: `REBAR_MIN_LENGTH` and `REBAR_MAX_LENGTH` on two rebar sets do not find their own value (`11' - 10 3/8"`, `25' - 11 1/2"`). Not seen on 2026; to investigate |
| UI-04 | 7,746 | — | 7,700 | Golden value from the 2026 sample (as SC-02) |

Closed on 2026-11-18 (test helpers, not the panel): hidden controls stay in the
UIA tree as offscreen elements. Three value editors share `Filter.Rule.Value`
(two hidden), the hidden temporary bar keeps `Temporary.Text` ("0 changes"), and
a hidden `Filter.DiscardSwitch` was invoked as if shown. `Shown`, `Gone` and
`Last` in `RevitPanel` now take on-screen elements only (UI-01–UI-03, UI-05,
UI-06).

Closed on 2026-11-21, Revit 2026 (run `20261121-031343`, L2 439 of 439, L3 8 of
8). UI-07: the edit that Isolate made fired a context reload, which canceled the
running action before it journaled the hidden rest, so no bar showed and Reset
could not show the link again. A started visibility action now finishes; menu
and row tool refusals show in `ModelContext.Message`. UI-08 (test helper): the
first unnamed button was a slider repeat button, not a swatch; `FirstSwatch` now
takes `Button` class elements only.

List a row here (ID, expected from the oracle or the Revit API,
panel value, cause seen in `mismatched-elements.md` from `WriteMismatchedElements`) when the panel differs;
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
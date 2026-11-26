# Execution Plan: Model Context Annotation in Explorer

Date: 2026-11-26

## Status

Completed (2026-11-26). Decision [0064](../../decisions/0064-model-context-annotation-in-explorer.md)
accepted. Final deploy run in full: L2, L3, the sweeps, `ExportSnapshots` and
the docked pane pass (see Result).

## Outcome

In Active view, Explorer and its Categories box list the annotation the view
shows (tags, text, dimensions, title blocks, viewports, grids, levels, detail
groups) next to model elements, with the same tree, counts, hidden rows and
actions. The scope follows the element: an element with an owner view
(any category) counts only in Active view, in its own view; an element with
no owner view counts in both scopes. Whole model gains Grids, Levels,
Reference Planes, Scope Boxes, Elevation markers and loses the view-owned
Detail Items and Detail Lines it lists today. Categories the user cannot find
in Visibility/Graphics never show.

## Context

- Product: [models-context.md](../../product/models-context.md) Scope,
  Explorer, Categories box, Hidden elements.
- Decisions: [0062](../../decisions/0062-model-context-panel.md),
  [0063](../../decisions/0063-model-context-ux-and-agent-foundation.md),
  [0064](../../decisions/0064-model-context-annotation-in-explorer.md).
- Code:
  - `source/RevitDevTool.Tools/ModelsContext/Documents/ElementReader.cs`:
    `ModelCategoryIds` is the one category rule; used by `AnyOptionFilter`,
    `OwnedCategoryIds`, `ParameterCatalogReader`, `ParameterPresence`.
  - `source/RevitDevTool.Tools/ModelsContext/Scripts/ExportSnapshots.cs`
    keeps its own copy of the rule.
  - `source/RevitDevTool.Tools/ModelsContext/Documents/ElementDrawing.cs`:
    `DrawOf` returns `None` for view-owned elements and for a null bounding
    box.
  - `source/DevTools.ModelsContext/Documents/SubcategoryRule.cs`.
  - `tests/RevitDevTool.ModelsContext.LiveTests/Oracle/Oracle.cs` keeps an
    independent copy of the category rule; it follows 0064 too.

### Baseline (HVAC, Revit 2026, 2026-11-26)

`Snowdon Towers Sample HVAC.rvt`, links loaded, measured through MCP with
`FilteredElementCollector`. The view used per type is the one with the most
elements.

Annotation categories in the model (view-owned / model-wide):

| Category | View-owned | Model-wide |
|----------|-----------:|-----------:|
| Duct Tags | 427 | 0 |
| Space Tags | 221 | 0 |
| Mechanical Equipment Tags | 76 | 0 |
| Text Notes | 63 | 0 |
| Flex Duct Tags | 34 | 0 |
| Viewports | 23 | 0 |
| Schedule Graphics | 19 | 0 |
| Title Blocks | 17 | 0 |
| Generic Annotations | 15 | 0 |
| Reference Planes | 4 | 7 |
| Grids | 0 | 15 |
| Elevations | 0 | 12 |
| Levels | 0 | 11 |
| Scope Boxes | 0 | 5 |
| Section Boxes (excluded) | 0 | 3 |
| Guide Grid | 0 | 1 |
| Automatic Sketch Dimensions (excluded) | 28 | 0 |
| Detail Groups (Internal, included) | 25 | 0 |

Per view:

| View | Elements | Annotation today missing from Explorer |
|------|---------:|----------------------------------------|
| Floor plan L3 | 1,048 | 123: Duct Tags 73, Space Tags 17, Grids 15, Mech Equipment Tags 10, Flex Duct Tags 5, Text Notes 3; Detail Groups 1 |
| Ceiling plan L3 | 1,020 | 96: Duct Tags 51, Space Tags 17, Grids 15, Mech Equipment Tags 10, Text Notes 3; Detail Groups 1 |
| Elevation North - Mech | 2,796 | 11: Levels 11 |
| Drafting "How to use this project 2" | 30 | 7: Text Notes 5, Reference Planes 2 |
| Sheet M000 Cover Sheet | 23 | 10: Text Notes 5, Viewports 2, Title Blocks 1, Schedule Graphics 1, Generic Annotations 1 |
| 3D {3D} | 4,816 | 31: Grids 15, Levels 11, Scope Boxes 5 |

View-owned elements of Model categories that leave Whole model: Lines 34
(detail lines), Raster Images 14, Detail Items 4.

Checked: no element of a category hidden from the UI passes today's filter.
The Model categories hidden from the UI (Water Loops, Air Systems, Zone
Equipment, Electrical Analytical *) have no elements here; the new rule drops
them anyway.

### Read cost on every sample (Revit 2026, 2026-11-26)

Python through MCP, so absolute times are slower than the C# reader; the
ratio is what counts. "Free" = no owner view, read today (model) or added
(annotation). "View-owned" = every view of the document. Share = view-owned
read time over the whole read.

| Document | Views | Model free | Annotation free | View-owned | ms (free / owned) | Share |
|----------|------:|-----------:|----------------:|-----------:|------------------:|------:|
| Snowdon HVAC | 86 | 2,877 | 54 | 951 | 167 / 44 | 21% |
| Snowdon Architectural | 404 | 6,152 | 1,084 | 5,969 | 304 / 226 | 43% |
| Snowdon Structural | 117 | 1,475 | 71 | 1,746 | 92 / 96 | 51% |
| Snowdon Electrical | 198 | 3,003 | 51 | 2,012 | 133 / 122 | 48% |
| Snowdon Plumbing | 148 | 6,563 | 54 | 402 | 265 / 30 | 10% |
| Snowdon Facades | 50 | 408 | 162 | 137 | 40 / 7 | 15% |
| Snowdon Site | 104 | 105 | 34 | 80 | 9 / 4 | 31% |
| Golden Nugget Gebäudetechnik | 426 | 7,269 | 101 | 1,935 | 313 / 86 | 22% |
| Golden Nugget Architektur | 510 | 4,215 | 753 | 4,002 | 419 / 219 | 34% |
| サンプル設備 | 611 | 5,361 | 171 | 1,948 | 251 / 119 | 32% |
| サンプル意匠 | 322 | 7,452 | 541 | 8,671 | 335 / 411 | 55% |

- No view was opened; the active view stayed the same for every read.
- Per element, a view-owned read costs the same as a model read (about
  0.05 ms here). The extra time grows with the number of tags, dimensions
  and detail lines, not with the number of views.
- Architectural models carry the most: up to 1.2× their model elements
  (Lines 3,561, Text Notes 1,513, Wall Tags 902 in サンプル意匠).
- Links never draw their view-owned elements in the host (0064 Decision 3),
  so a link reads free elements only. A federated model then pays the
  view-owned read for the Current model alone: HVAC 951 instead of 11,297
  for all seven Snowdon documents.
- Wires (Snowdon Electrical 436) are a Model category and view-owned; under
  0064 Decision 2 they leave Whole model like detail lines.

Gap: HVAC has no Dimensions, Sections, Callouts, Detail views, Legends,
Revision Clouds, Door or Room Tags. A second baseline on
`Snowdon Towers Sample Architectural.rvt` opened on its own (do not save)
covers them.

## Scope

In scope:

- One category rule (0064 Decision 1) shared by `ElementReader` and
  `ExportSnapshots`.
- Scope rule: an element with an owner view, of any category, only in Active
  view; an element with no owner view in both scopes.
- Hidden counts and dimmed rows for annotation in Active view.
- Select, Hide, Isolate, Halftone, Color on annotation rows and categories.
- Sheets, drafting views and legends list their content.
- Oracle and live tests on HVAC and Architectural baselines.
- Product doc: Scope table, Explorer category rule, Hidden elements.

Out of scope:

- Annotation inside links, including linked datums.
- Review checks, tagging, tag-to-host in the UI.
- Revision clouds across views (0064 Follow-Up). In scope: clouds of the
  active view list, select and filter by their Revision like any element.
  Samples hold only 7 clouds (サンプル意匠 5, Snowdon Architectural 2); a
  revision baseline needs a copy with clouds of several revisions on plans
  and sheets.
- Filter view changes beyond what the shared rule brings (see Decisions).

## Approach

1. **Second baseline.** Close HVAC without saving, open Architectural alone,
   run the same two surveys, add the table above. Pick fixed views per type
   (plan, section, callout/detail, drafting, legend, sheet, 3D) and record
   their names so live tests use the same ones.
2. **Category rule.** Replace the `CategoryType.Model` check with: Model or
   Annotation, `IsVisibleInUI`, plus Detail Groups, minus the 0064 exclude
   list. One method, used by `ElementReader` and `ExportSnapshots`.
   Focused unit test for the rule on plain data (category type, visible flag,
   built-in name).
3. **Scope rule on one snapshot.** The snapshot keeps reading the whole
   document once (`RevitModelSource.BuildSnapshotAsync`), now with the
   view-owned elements of every view; each record already carries its owner
   (`ElementDrawing.OwnerOf`). Scopes cut by owner, with no second read:
   - Whole model: records with no owner view
     (`ElementOwnerViewFilter(ElementId.InvalidElementId)` in `CollectIds`).
   - Active view: what the view shows, as today; a view switch needs no new
     read.
   Reading needs no active or drawn view: the owner view is a database
   property. Measured on HVAC with the Cover Sheet active (Python, 66 views):
   view-owned elements of every view 951, collect 4 ms, read 34 ms, next to
   2,877 model elements read in 159 ms. Across all eleven samples the
   view-owned share is 10–55% (table above). A link snapshot skips
   view-owned elements (`ElementOwnerViewFilter(InvalidElementId)`), so only
   the Current model pays. Confirm with the C# reader on サンプル意匠 as the
   Current model (the worst case, 8,671 view-owned); fall back to a per-view
   read only if the snapshot time there grows by more than its share here.
4. **Drawing.** Check `DrawOf` on Grids, Levels, Reference Planes, Scope
   Boxes, Elevation markers: a null bounding box must not make a model-wide
   datum look hidden. Fix in `ElementDrawing` if needed.
5. **Actions.** Run each action on an annotation row and category in the
   live host; Color paints lines; record any Revit refusal as a short cue.
6. **Oracle.** For each baseline view, the Active view counts per category
   equal `FilteredElementCollector(doc, viewId)` restricted to the rule; the
   Whole model counts equal the model-wide rows of the baseline.
7. **Docs.** Update `docs/product/models-context.md` and the live test
   catalog; index 0064.

## Risks And Recovery

- **Filter view inherits annotation.** `ParameterCatalogReader` and
  `ParameterPresence` use the same rule. Annotation categories many filters
  cannot take are already guarded by `filterable`; check that Whole model
  Filter results do not change for model categories. Recovery: keep a Model
  only set for the Filter path until Filter is decided.
- **Whole model counts drop for Lines, Detail Items, Raster Images.** Live
  tests or catalog checks that compare Whole model with a Detail Items
  schedule must change to the new rule; the Oracle drops view-owned elements
  in Whole model the same way.
- **View switch counts.** Active view must show only the new view's owned
  elements after a switch. Test: switch L3 plan → Cover Sheet → L3 and
  compare counts with the baseline each time.
- **Snapshot size in heavy views.** A plan with thousands of tags. Measure the
  L3 plan read time before and after; the slice reader keeps the UI free.
- **Isolate keeps annotation.** Once annotation is listed, isolating a model
  category still hides annotation; check that isolating Duct Tags with Ducts
  keeps both.
- Recovery: the rule is one method; revert it to Model only.

## Progress

- [x] HVAC baseline measured.
- [x] 0064 written (Proposed).
- [x] Read cost measured on all eleven samples (Python).
- [x] Architectural baseline views picked and counted (サンプル意匠, Golden Nugget
  Architektur; Snowdon Architectural is a link of HVAC and was not opened).
- [x] Category rule + unit test.
- [x] Scope rule.
- [x] Drawing check for datums (Scope Boxes and Guide Grid then set to no draw).
- [x] Actions on annotation, live (Duct Tags on L3).
- [x] Read cost in C# on サンプル意匠: keep read-all (Decisions).
- [x] Oracle on HVAC (L2 run `20261126-185127`, 449 pass, 0 fail).
- [x] Oracle on the architectural views: SW-01 panel = oracle on サンプル意匠 and
  Golden Nugget Architektur, plan, section, legend, sheet and Whole model (test
  matrix, Sweep).
- [x] `ExportSnapshots` runs live and matches SC-01 (deployed add-in).
- [x] Full redeploy, L2 and the `{3D}` category actions pass.
- [x] Docked pane on HVAC: Duct Tags 73 in `L3`, Whole model 2,928.
- [x] L3 full run: 28 of 28 after the UI-11 fix (final deploy run).
- [x] Warm snapshot back to one id pass: サンプル意匠 warm 105 ms.
- [x] Product doc.
- [x] Test catalog.

## Decisions

- 2026-11-26: The scope follows view dependence, not category type: a
  view-owned element of any category counts in Active view only; an element
  with no owner view counts in both scopes (user decision, see 0064
  Decision 2).
- 2026-11-26: View-owned elements of every view are read once with the
  snapshot, because reading them opens no view and costs about the same per
  element as model elements (HVAC: 951 elements, 38 ms). User decision: keep
  read-all as long as it does not cost opening views. Fallback to a per-view
  read only if the heavy-model measurement says so.
- 2026-11-26: Detail Groups count although Revit types them Internal; they
  are user-placed and visible in Visibility/Graphics.
- 2026-11-26: Filter view takes the same rule unless step 6 shows a change in
  model category results.
- 2026-11-26: Step 3 keeps read-all. C# on サンプル意匠 as the Current model
  (SW-04, run `20261126-190212`): snapshot 17,325 rows, cold 291 ms in 5 slices
  (longest 101 ms), warm 108 ms, invalidated 154 ms; no Revit call over 1 s. The
  same file read as a link before 0064 (run `20261119-231009`): 11,087 rows, warm
  48 ms. Warm grew 2.24×, the 2.2× that a 55% view-owned share predicts; on HVAC
  with the same code the time per row stayed flat (2,929 rows 35.6 ms, 3,904 rows
  43.6 ms). The per-view fallback is not needed.

## Validation

- Focused proof: unit test for the category rule;
  `dotnet run --project tests/DevTools.ModelsContext.Tests/DevTools.ModelsContext.Tests.csproj`.
- Integration or end-to-end proof: live oracle on HVAC and Architectural
  baseline views in Revit 2026; L2 live tests for Explorer counts per scope.
- Repository-required checks: build skill for touched projects;
  `pwsh -File scripts/format-lint.ps1` once at the end.

Run so far (Revit 2026, 2026-11-26):

- L1 core: 632/632 (`dotnet run --project tests/DevTools.ModelsContext.Tests/DevTools.ModelsContext.Tests.csproj -f net8.0-windows`).
- L2 HVAC: run `20261126-185127`, 449 pass, 0 fail. SC-01 2,928, SC-02 9,743,
  SC-05 1,041, SC-06 1,956, AN-01 to AN-03 pass.
- Sweep SW-01 on サンプル意匠 (run `20261126-190145`): Whole model 7,478 = 7,478
  (40 categories), floor plan `平面詳図 7階` 818 = 818 (30 categories; Dimensions
  139, Wall Tags 122, Door Tags 18, Room Tags 15 in the view).
- SW-04 on サンプル意匠: run `20261126-190212`, pass (Decisions).

Deploy run (`scripts/build-host.ps1 -HostApp Revit -Year 2026`, fresh Revit 2026,
HVAC with six links):

- L2 full: run `20261126-194036`, 447 pass, 0 fail, 8 Inconclusive (CA-02 to
  CA-09, by design); SB-01 passes. Safety ok.
- `CategoryActionTests` alone in `{3D}`: run `20261126-194505`, CA-01 to CA-10 pass.
- L2 + L3 with `-Ui`: run `20261126-194559`. L2 again 449 tests pass, 0 fail. L3:
  27 pass, 1 fail (UI-11), 4 skipped (live steps). Safety ok after L2, after L3 and
  at host end.
- `ExportSnapshots` on the deployed add-in: Current model 2,928 (SC-01),
  Architectural 9,743 (SC-02), Structural 1,475, Facades 528, Plumbing 6,563,
  Electrical 3,003, Site 119. Each equals that document's Whole model count in
  SW-01 run `20261126-195723` (HVAC, 197 categories, 0 mismatches).
- Docked pane (UIA, read only, scope switched and switched back): Whole model
  Current model 2,928; Active view `L3` Current model 1,041 with Duct Tags 73,
  Architectural link 1,956.
- SW-04 on サンプル意匠 after the sliced snapshot ids (run `20261126-195820`):
  longest snapshot call 36.6 ms (was 101.3), no hitch; snapshot total 231 ms cold
  and 231 ms warm, in 9 calls.
- SW-01 on Golden Nugget `UG 1_Genehmigung` (run `20261126-195958`): 431 = 431,
  hidden link 0 = 0, Whole model 5,121 and 6,971.

Final deploy run (redeploy after the warm snapshot and UI-11 fixes, fresh Revit
2026, HVAC with six links):

- L2 + L3 with `-Ui`: run `20261126-202825`. L2 447 pass, 0 fail, 8 Inconclusive
  (CA-02 to CA-09, by design). L3 28 of 28 pass, 4 live steps skipped; UI-11
  checks Doors and Walls. Safety ok after L2, after L3 and at host end.
- `CategoryActionTests` alone in `{3D}`: run `20261126-203659`, CA-01 to CA-10 pass.
- SW-04 on サンプル意匠: run `20261126-203920`. Snapshot cold 103 ms, warm 105 ms,
  invalidated 116 ms, in 6 calls; longest snapshot call 31 ms; no call over
  50 ms in the run (longest 44 ms, a cold filter).
- Docked pane (UIA): Active view `L3` Current model 1,041 with Duct Tags 73;
  Whole model Current model 2,928.

## Result

Live runs pass on the shared models:

- HVAC L2 and L3 on the deployed add-in: run `20261126-202825`, 0 fail, L3 28 of
  28; CA-01 to CA-10 in `{3D}`.
- Architectural sweep: SW-01 panel = oracle in a plan, a section, a legend and a
  sheet, plus Whole model, on サンプル意匠 and Golden Nugget Architektur. That
  includes a loaded link the plan does not draw, which counts 0 (runs in the test
  matrix, Sweep).
- `ExportSnapshots` matches the Whole model count of every document.
- Read cost: the heaviest sample (サンプル意匠, 17,325 rows) reads in about
  105 ms, with no Revit call over 50 ms.
- The docked pane shows the annotation counts (Duct Tags 73 in `L3`) and the
  Whole model cut (2,928).

Fixed during the deploy rounds: UI-11 (focus on the Categories box cleared
its checks) and a warm snapshot that took as long as a cold one.

Nothing is open. 0064's accept condition (the plan's live runs pass on the
shared models) is met; the leader decides on Accepted.

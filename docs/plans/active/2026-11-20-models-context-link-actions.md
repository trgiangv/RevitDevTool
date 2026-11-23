# Execution Plan: Model Context Link Actions

Date: 2026-11-20

## Status

Active. D, G, C, E, F, the cleanup H, A and B are built (2026-11-21); the live
runs of A and B and the manual Pick check are open.

## Outcome

A filter result that holds linked elements supports the same daily actions as
Current model elements, within what Revit allows, under
[0063](../../decisions/0063-model-context-ux-and-agent-foundation.md):

1. **Isolate and graphic overrides on specific linked elements** (halftone,
   color) reach the chosen element ids inside a link, or the panel shows a
   short cue for the part Revit refuses. A whole category is not this work: it
   is already controlled through the view's visibility (T2).
2. **Active select by category and source file.** Today the user must press
   Tab to pick a linked element, and Scope = Selection only reads what was
   selected before. Explorer gains a *Pick* icon button beside its search box:
   the user clicks or boxes in the view, and only elements of the ticked
   links and categories can be picked (links only; Current model elements are
   picked freely in Revit already), the way an `ISelectionFilter` works in
   an add-in command. Linked elements are picked without Tab. The result is the
   Revit selection, in any scope. Nothing richer (no panel filter as the pick
   rule): this level is enough.
3. **One engine.** Explorer and Filter differ only in UI. Both read through the
   same engine (snapshots, parameter reads, evaluator, actions); the new
   actions are added once in the engine and reached from both.
4. **Why hidden.** For a selected dimmed row (an element in the Current model
   or a link), a strip at the bottom of the pane lists what hides it in the
   active view, as short cues. Read only;
   a fix appears only where an existing tier already does it (Show element,
   Show category). The same check is an engine call for the MCP tool.
5. **Nested families act as a whole.** Hide, isolate and overrides on a family
   instance also reach its shared nested instances, if Revit does not already
   do so.
6. **Explorer scope boxes.** Explorer gets a Categories box under the documents
   box; the *All models* tree level goes away. Source and Categories decide what
   the tree lists, and Pick reuses them, so no new category picker is needed.
   Active view keeps listing visible elements and counts the hidden ones (part
   E). Filter keeps its own flow: its categories stay inside the filter.

## Context

- Limits found so far: [0062](../../decisions/0062-model-context-panel.md)
  Context 5, 11, 12 and rejected alternatives 29–31. Revit has no temporary
  hide, isolate or per-element override for a linked element; T3 hides through
  the internal Hide in View command; isolate by hiding the rest works but is
  saved with the view (about 730 ms for 7,818 hides, about 5.5 s to restore).
- Context 3: a host view filter on built-in and shared parameters reaches
  linked elements when the link displays By Host View.
- Context 8: Revit can hide or override by a filter, but cannot select by one.
- Link Hide 534 ms and Show 2,420 ms on the Revit thread are still open in the
  [host matrix plan](2026-11-19-models-context-host-matrix.md) (UI-05/06).
- Linked element selection already works through link references
  (`SetReferences`, [0036](../../decisions/0036-revit-monitor-link-element-tokens.md)).
- Behavior: [product/models-context.md](../../product/models-context.md).
- User demand and prior art: 0063 Context. The pyRevit hackathon #1 entries
  were read as reference (code not reused):
  - *Why Not Here?* runs 10 checks for one Current model element in a chosen
    view: owner view, Hide in View, category, view filters (`PassesFilter` per
    hidden filter), workset (view, default, closed), phase, design option,
    view range (with a Fix that moves the offset), MEP discipline above the
    cut plane, crop and section box (with a Fix that grows the crop). Each fix
    has an Undo. Gaps: no links, no temporary hide/isolate, no subcategory; a
    filter that fails to evaluate counts as hiding.
  - *FilterMan* expands nested shared families with `GetSubComponentIds`
    before isolate, hide and color, because isolating the host instance alone
    showed nothing for families made only of nested shared instances. Model
    Context has no such step today.

## Scope

In scope:

- Spikes on Revit 2026 with Snowdon (federated) and Golden Nugget.
- Isolate specific elements inside one link and across mixed results.
- Halftone and color for specific linked elements.
- Active select in Scope = Selection by category and source file.
- Check that Explorer and Filter share the engine for reads and actions; move
  any duplicate path into the engine.
- Why hidden for one row in the active view, Current model and linked.
- Nested shared families in hide, isolate and overrides.
- Product, ADR and test updates for what lands.

Out of scope:

- Nested links (0062 keeps them out of T3).
- Category-wide isolate or overrides (view visibility, T2, already exists).
- The panel filter as a pick rule.
- Color by value, Excel export/update, bulk edit (later actions under 0063).
- Why hidden for a view other than the active one, and fixes that change view
  range, crop, worksets, phase or design option (cue only in this plan).
- Expand by MEP system (0062 Decision 13, context strip).
- Other Revit years until 2026 passes; then the 0062 year checks.

## Approach

### A. Isolate specific elements inside a link

The target is a set of element ids inside a link (from a filter result or
Explorer rows below a link's model row), not a category.


| Option | What it does | Known cost or risk |
|---|---|---|
| A1 Hide the rest | Temporary isolate of the link instance, then T3 Hide in View for the rest of that link (Context 12) | Saved with the view; restore about 5.5 s at 7.8k; journal must survive switches |
| A2 A1 for mixed results | T0 temporary isolate for Current model ids plus A1 per link | Two kinds of state for one Reset |
| A3 Refuse with a cue | Keep Isolate disabled below a link's model row | No gain |

Measure A1 at 1k, 10k and 50k elements; Reset after edits, undo, link reload
and view switch; locked templates.

### B. Graphic overrides for specific linked elements

Same target as A: chosen element ids inside a link.

| Option | What it does | Known cost or risk |
|---|---|---|
| B1 Rule view filter | Build a `ParameterFilterElement` from the panel rules when every rule is expressible natively (the SW-02 native compiler) and apply it to the view; reaches By Host View links | Adds a project element (0062 "no model writes"); not for By Linked View or Custom links; regeneration cost |
| B2 Whole link only | T1 override of the link instance (exists) | Not per element |
| B3 Cue only | Halftone and Color stay disabled for linked elements | No gain |

B1 needs a decision from the user before it is built: a temporary filter
element named for the panel, removed on Reset and on pane close, versus
keeping "no model writes". Spike: which results are expressible, regeneration
time on Snowdon, undo entries, behavior with a locked template.

### C. Active select by category and source file

- Problem today: picking a linked element in Revit needs Tab, and Scope =
  Selection only reads the preselection.
- UI: a *Pick* icon button beside the Explorer search box. It uses the ticked
  documents and the Explorer Categories box (G); no control of its own, no
  dialog. Status cue while picking: "Picking · Esc to finish".
- Pick is for links only (`ObjectType.LinkedElement`): Current model elements
  are already picked freely in Revit. It is enabled when at least one link is
  ticked and picks elements of the ticked link instances only, whether or not
  the Current model is also ticked. With no link ticked it is disabled,
  tooltip "Tick a link to pick in it".
- The `ISelectionFilter` checks the link instance id and the category id (set
  checks, no parameter reads during the pick).
- The picked set becomes the Revit selection (links through `SetReferences`)
  in any scope; Selection sync and Scope = Selection show it with no extra
  step.
- Spike questions: does `ObjectType.LinkedElement` pick linked elements without
  Tab; does box selection work for them; does the pick start safely from the
  panel (external event, panel focus); Escape, view switch and document close
  during the pick.

### D. One engine for Explorer and Filter

- List where Explorer and Filter read elements, parameters and visibility, and
  where they run actions. Any path only one of them uses moves into the
  engine, so both get the same counts, link handling and speed.
- A, B, C and E are added once in the engine (one action port, one Revit
  adapter path) and reached from Explorer rows, filter results and the future
  MCP tool.

### E. Why hidden

- Where hidden rows come from (Scope = Active view, part G): each group row
  counts its hidden elements in its count text ("+12 hidden"); clicking it
  lists that group's hidden elements dimmed; search also finds hidden
  elements and shows them dimmed. No extra button: Pick is the only new
  control in the search row. Hidden = in the tree's sources and categories but not in the
  view's collector, one set difference per refresh. Group actions (Select,
  Hide, Isolate on a category row) act on visible elements only; a dimmed row
  is acted on only when it is selected itself.
- Answer: selecting one dimmed row opens the *reason strip* at the bottom of
  the pane, above the Temporary bar (which stays visible). One line: the first
  cause and its fix, if any (for example "Category off: Duct Fittings ·
  Show"), plus a chevron that expands the full list of causes ("Filter:
  Walls-Demo", "Workset closed", "Below view range"; "No cause found" when no
  check hits). The strip follows the selected row while it is open, closes on
  ✕, Esc, or when the selection has no dimmed row. No dialog, no view
  picker, nothing inserted into the tree. The context menu keeps *Why
  hidden?* to open it for a row the user has not selected.
- The strip is a plain row in the pane's grid with HandyControl stock styles;
  no `hc:Drawer`, whose adorner may not work inside a Revit dockable pane.
- Order of checks, first hit first, each a read on the Revit thread:
  1. Temporary hide or isolate active.
  2. Element hidden in view (`IsHidden`).
  3. Category or subcategory off.
  4. View filter with visibility off that the element passes; a filter that
     fails to evaluate is "possible", never "hides".
  5. Workset hidden in the view, hidden by default, or closed.
  6. Phase and phase filter.
  7. Design option not shown.
  8. View range or far clip (plan and section depth).
  9. Crop region or section box.
  10. View-specific element owned by another view.
- Linked element, in addition and first:
  - link instance hidden, its category or its workset off;
  - link display mode: By Host View uses the host view's category, filter and
    phase; By Linked View and Custom use the link's own view settings, and the
    cue names the mode;
  - element hidden by T3 (journal) or in the link's own model;
  - then checks 3–9 against the link document, in host coordinates for 8 and
    9.
- Why hidden only explains. It never locks or changes any action: Hide, Show,
  Isolate, Halftone and Color stay available on every row as before, and an
  active temporary hide or isolate is itself one of the causes it reports.
- Fix buttons only where a tier exists: Show (T0/T3) for check 2 and T3 hides,
  Show category (T2) for 3. Everything else is a cue; it never changes view
  range, crop, worksets or phases.
- Spike questions: which checks the link view collector already answers in
  one call; cost per row on Snowdon (target under 50 ms); MEP discipline
  above the cut plane (the hackathon check) as part of 8.
- Tests: an L2 case per check that builds the cause in a throwaway view, asks
  Why hidden, and reads the cue; links on Snowdon for the By Host View and By
  Linked View cases; safety guard before and after.

### F. Nested shared families

- Spike: on Revit 2026, do `IsolateElementsTemporary`, `HideElementsTemporary`
  and `SetElementOverrides` on a host instance reach its shared nested
  instances (`GetSubComponentIds`)? Same question for T3 Hide on a linked
  host instance.
- If not, the engine expands the ids before the action and Reset uses the
  same expanded set. Counts in the panel stay per row (no new rows).

### G. Explorer scope boxes

- Explorer reuses the Filter's Categories section (search, All · None, count
  per row) under the documents box. Default: every category ticked. The list
  holds the categories present in the ticked documents and the current scope.
  Unticked categories are kept in memory as exclusions, so a category that
  appears later (an edit, a new link) shows by default.
- In Explorer each category row also carries the view's category visibility
  (T2: the Current model and every By Host View link), replacing the *All
  models* tree level:
  - the tick (left) decides what the tree lists; the eye (right, after the
    count) hides or shows the category in the view, read back from the view;
    a hidden category's row dims;
  - the tick is the row selection (click, Shift, Ctrl select and tick); the
    row menu Isolate, Halftone and Color act on the ticked rows; the eye acts
    on its own row (2026-11-23, decisions below);
  - Isolate, Halftone and Color sit in the row context menu, for one or many
    selected rows; no extra buttons on the row;
  - the eye tooltip names the scope ("Hide in view: Current model + 2 links
    (By Host View)") and the links it does not reach (By Linked View, Custom),
    keeping the 0062 Decision 1 hint;
  - a view template does not disable them: hide, isolate, halftone and color
    run in Revit's temporary modes, which work when a template locks
    Visibility/Graphics (0062 Decision 6); only Keep may meet the template
    (0062 spike 5).
- The tree starts at each ticked document's model row (Model › Category ›
  Family › Type › Instance); per-document rows keep T0, T1 and T3.
- Filter keeps its own flow: the same Categories section without the eye or
  the menu, its ticks part of the filter definition and saved with a custom
  filter. Switching Explorer and Filter switches context; the documents box
  stays shared (0062 Decision 3).
- Scope = Selection: the documents and Categories boxes stay visible for Pick;
  the tree still shows the selection.
- Amend 0062 Decisions 1, 4, 5 and 11 when this lands.

## Risks And Recovery

- B1 writes to the model: built only after the user accepts it; Reset and pane
  close remove the element; a failed removal is reported in one sentence.
- A1 hides are saved with the view: the journal records them and Reset shows
  them again, as T3 does today.
- A pick left open when the pane closes or the document changes: the pick ends
  with Revit's cancel path; no state is kept.
- Internal command use (T3) may change in a Revit update: the 0062 year checks
  run before release.

## Progress

- [x] D: map Explorer and Filter paths; list duplicates to fold into the engine
      (Spike results, D).
- [x] Feature folders: core, adapter, views and tests split by feature
      (Documents, Scope, Explorer, Filters, Parameters, Visibility, Selection,
      Panel); namespaces follow folders.
- [x] H: cleanup before G. Split `ExplorerTabViewModel` (1,561 lines),
      `ModelContextViewModel` (842), `VisibilityController` (637) by
      responsibility; methods over Sonar cognitive complexity 15 (S3776,
      counted by reading: the analyzer build was not allowed) get early
      returns and named helpers; adapter readers (`ParameterCatalogReader`,
      `ParameterLocator`, `RevitFilterEvaluator`, `RevitModelSource`) and
      `FilterLibraryViewModel` the same. No behavior change; 369 + 103 tests
      are the net.
- [x] G: Categories section in Explorer with the eye and row menu (T2),
      *All models* level removed, Active view "+N hidden" with dimmed rows
      (2026-11-21). Open: the L3 run by AutomationId.
- [x] C: Pick engine and UI (`PickController`, `LinkPickFilter`, Explorer
      button). Spike on Revit 2026 done; open: the manual Pick check (click and
      box without Tab) and a live L3 step for the cancel path.
- [x] E: Why hidden engine and UI (strip above the Temporary bar, menu item).
      Open: the live L2 `WhyHiddenTests` run (WH-01 to WH-06).
- [x] F: nested shared instances expand for T0 isolate, hide and overrides.
      Open: the live L2 `NestedChairsFollowHost` (not written yet).
- [x] D: one engine built (rows resolve to `ElementRef`s once, hidden read-back
      and override marks by `ElementRef`, branch actions reach the whole branch).
- [x] Spike C (active select by category and source file) on Revit 2026.
- [x] Spike F (nested shared families) on the Current model: Revit does not
      reach nested instances (Spike results, F); T3 on a linked host already
      hides its nested instances. Expansion built for T0.
- [x] Spike E (Why hidden): cost per row and check order on By Host View
      (Spike results, E). By Linked View and Custom still open.
- [x] Spike A1: timings taken from 0062 Context 12 (Spike results, A1); A
      waits for demand.
- [x] Spike B1 on a templated plan (Spike results, B1): works only inside
      Temporary View Properties; the model write decision stays with the
      owner.
- [x] Amend 0062 Decisions 1, 4, 5, 11 and 13 for G, C, E and F (2026-11-21);
      A and B amend Decisions 2, 5, 7, 13, alternatives 3, 29, 31 and the
      tradeoffs (2026-11-21).
- [x] Product doc, architecture pages, test matrix rows (headless and view
      tests pass: core 431, views 120).
- [x] Live L2 and L3 on Revit 2026, Snowdon HVAC (2026-11-21, run
      `artifacts/models-context-tests/20261121-*`): L2 435 passed, 0 failed,
      110 skipped (explicit); WH-04 alone on plan L3 passed; L3 13 passed,
      0 failed, 3 skipped; safety ok after L2 and L3, `EndStateIsUnchanged`
      passed. The first run failed only WH-04 (it needs a plan; now Explicit).
- [x] 2026-11-21 rerun after the last changes (run `20261121-005619`): L2
      436 passed, 0 failed, 111 skipped (PI-03 `FilterKeepsTickedOnly`
      included); L3 13 passed, 3 skipped; safety ok. NE-01
      `NestedChairsFollowHost` passed alone on the Architectural copy in
      `{3D}`. The TestAdapter now reports `Assert.Inconclusive` as skipped
      (`TestNodeProperties.ToStateProperty`; WH-04 in `{3D}` reads skipped).
- [x] A and B built and proven live (run `20261121-031343`, Revit 2026,
      Snowdon HVAC): L2 439 recorded cases passed (VI-14..VI-20), US-18
      alone on templated L3 passed, L3 8 flows passed including UI-07 link
      isolate and UI-08 link type color through the panel; safety ok after
      L2, after L3 and at host end. Fixed on the way: a started visibility
      action no longer shares the Explorer token (a reload canceled the
      journal write after Revit had hidden 5,717 linked elements); refusals
      now show in the panel message line.
- [x] Manual Pick check by the owner (2026-11-23, Revit 2026, Snowdon HVAC,
      plan L3, Architectural link ticked): with every category ticked the
      pick (click and box, no Tab, Finish) gave 4,203 references, all in the
      Architectural link and none in the Current model; with only Doors ticked
      the picks gave linked doors only (panel log: 24 elements, then the
      selection read 1 linked door). Open question: the panel logged 614
      elements for the 4,203-reference pick (references vs. elements in
      scope); worth a check. The filter itself is covered by PI-03 and Esc by Spike C. Status of
      [0063](../../decisions/0063-model-context-ux-and-agent-foundation.md)
      stays Proposed until the manual check and the no-training check pass.
- [x] A (isolate specific linked elements) built 2026-11-21: `LinkIsolateStep`,
      `RevitViewActions.IsolateWithLinksAsync` + `LinkIsolation` (rest from the
      active view's link collector, model categories, minus targets, nested and
      hosts; cap 10,000; one outer `TransactionGroup`), journal of the rest,
      Reset and eye show it, a Show that reads back 0 drops and reports once.
      L1 `PlannerTests`, `PanelBarsTests`; L2 VI-19, VI-20 plan only. Open: the
      live run (manual or L3) of a real link isolate, its time at 1k/10k, and
      whether `LinkedHideShow`'s group nests inside the outer group as expected.
- [x] B (overrides for specific linked elements) built 2026-11-21: T4
      `LinkFilterStep` (Family Name + Type Name for rows, the filter's rules
      for results, cap 100), `LinkFilters` registry and sweep, `LinkFilterRules`
      native compiler in the adapter, delete on Reset, save, save as, sync and
      pane close. L1 `PlannerTests`, `PanelBarsTests`; L2 VI-16 to VI-18,
      US-18 (explicit, templated L3). Open: the live L2 run; the MCP spike for
      which family and type name parameters are filterable did not run (Revit
      pid 43588 did not answer within 30 min), so `LinkFilterRules` picks at run
      time between `ALL_MODEL_FAMILY_NAME` / `SYMBOL_FAMILY_NAME_PARAM` and
      `ALL_MODEL_TYPE_NAME` / `SYMBOL_NAME_PARAM` and refuses when none is
      filterable; the transaction in `DocumentSaving` follows the API doc and
      was not run (no save in tests).
- [x] 2026-11-23 owner fixes (hidden counts, Categories multi-select, UI-07/08)
      proven in run `20261123-211123` (Revit 2026, Snowdon HVAC): L2 441
      passed, 0 failed, 113 skipped; L3 15 passed, 3 skipped (8/8 flows);
      safety ok after L2, after L3 and at host end. UI-08 now finds the color
      picker among Revit's owned popups, picks a swatch and confirms before it
      leaves Explorer, and closes the picker on every path; no fixed sleeps.
      Evidence and the safety baseline restart per `dotnet test` session
      (`TestSession`).
- [x] 2026-11-23 a menu open clears an old panel error (`MenuOpenClearsOldError`).
- [x] WH-09 `LinkedHiddenAllHaveCause` on L3: the first run found 2 sampled
      hidden linked elements without a cause (Facades link, nested window
      trims 658698 and 998942: parent drawn, child off by its family's
      settings). Fix: a nested element that is not drawn and has no cause of
      its own takes its parent's causes, else "Hidden by its family: <parent
      family>". After the redeploy (Revit 2026 pid 35052) WH-09 and WH-04
      passed on L3, and the full run `20261123-214720` passed: L2 441 passed,
      0 failed, 113 skipped; L3 15 passed, 3 skipped; safety ok after L2,
      after L3 and at host end. L1 core 469/469, views 125/125.
- [x] 2026-11-23 tick = selection in the Explorer Categories box and category
      Isolate as a category hide in Temporary View Properties (decisions
      below; Spike results, Category isolate by category hide; the first cut
      with a view filter ran as `20261123-223355`). VI-10 and VI-12 assert the
      outcome on the Current model and every By Host View link: VI-12 Isolate
      Doors leaves only doors (Current model 4,815 → 8, Architectural 7,823 →
      183, Doors 141 kept) plus the listed leftovers, and Reset brings every
      count back. Full run `20261123-232055` (Revit 2026 pid 2004, Snowdon
      HVAC): L2 441 passed, 0 failed, 113 skipped; L3 15 passed, 3 skipped;
      safety ok after L2, after L3 and at host end. L1 core 471/471, views
      126/126.

## Decisions

- 2026-11-20: Active select (C) is the first gap to build: no model writes and
  no new Revit limits expected. Its rule is category plus source file only.
- 2026-11-20: A and B target specific element ids; category-wide stays with
  view visibility (T2).
- 2026-11-20: Order D → G + C (+F) → E → A → B (G first: Pick reuses its
  boxes). E is read only, answers a daily
  pain no add-in covers for links, and also serves the MCP tool; A and B wait
  for real demand because of their Revit limits and model writes.
- 2026-11-20: E runs in the active view only and fixes only through existing
  tiers; view range, crop, worksets and phases stay cues.
- 2026-11-20 (user): Explorer gets a Categories box under the documents box
  and drops the *All models* level; default all categories ticked. Filter keeps
  its own categories and flow; Explorer and Filter are two contexts.
- 2026-11-20 (user): Explorer reuses the Filter's Categories section; each
  row gets one eye (T2 Hide/Show in view); Isolate, Halftone and Color go to
  the row context menu. Filter's section has no eye or menu.
- 2026-11-20 (user): in the Categories section the tick is the selection;
  no separate row selection. Simple, and the user knows what is ticked.
  *Replaced 2026-11-23.*
- 2026-11-23 (user): replaces the 2026-11-20 "the tick is the selection". The
  Explorer Categories list has its own row selection (click, Shift, Ctrl,
  keyboard); the row menu (Isolate, Halftone, Color) acts on the selected rows
  and is disabled with *Select categories* when none is selected. The tick only
  decides what the tree lists; the eye acts on its own row. Filter's section
  keeps the tick as the filter definition. Why: with every category ticked
  (the default) Isolate changed nothing, and Shift/Ctrl did not work.
  Category isolate also keeps the RVT Links category, because
  `IsolateCategoriesTemporary` alone hid every link instance.
  *Replaced 2026-11-23 (tick = selection; category isolate = view filter).*
- 2026-11-23 (user): replaces the decision above. In the Explorer Categories
  box the tick is the selection again, as one state: Shift/Ctrl selecting rows
  ticks exactly those rows and ticking a box selects it; Isolate, Halftone and
  Color act on the ticked rows. Filter's section keeps its own ticks. Category
  Isolate means only the ticked categories stay: inside Temporary View
  Properties it hides every other category the view can hide (all types:
  model, annotation, analytical, imports), except the kept ones, their parents
  and RVT Links; Reset ends the mode. No model element is written. (A first cut
  the same day used one rule-less view filter on filterable model categories;
  the owner asked for everything else hidden, and the category hide covers
  that and more, so it replaced the filter.) Why:
  the owner tested Isolate Doors on Snowdon HVAC and nothing changed, because
  temporary category isolate never reaches inside links; a view filter reaches
  every By Host View link. Links By Linked View or Custom get a one-line panel
  message.
- 2026-11-20 (user): Pick is an icon button beside the Explorer search, for
  links only: enabled when a link is ticked, picks in the ticked links,
  disabled with no link ticked (Current model elements are picked freely in
  Revit already).
- 2026-11-20 (user): Active view lists visible elements and counts hidden ones;
  search and a click on "+N hidden" list hidden elements dimmed (no *Show
  hidden* button); group actions act on visible elements only.
- 2026-11-21 (user): build B now. Halftone and Color on specific linked
  elements create one temporary `ParameterFilterElement` per request, applied
  inside Temporary View Properties; Reset and pane close delete it; a failed
  delete is one sentence. This is the one allowed model write.
- 2026-11-21 (user): build A now. Isolate on specific linked elements =
  temporary isolate of the link instance (with any Current model ids) plus T3
  Hide in View of the rest of that link in the active view; the journal keeps
  the hides so Reset shows them again.
- 2026-11-21 (user): the manual Pick check waits for the user.
- 2026-11-21: Why hidden offers a Fix only where a panel action really changes
  the result: Show for a temporary hide on a Current model element (T0) and
  for a linked element hidden in view (T3 Unhide). A permanent hide on a
  Current model element and a category off in Visibility/Graphics are cues
  only: the panel's T0 and T2 actions are temporary (0062 Decision 6) and
  cannot undo them.
- 2026-11-20: Why hidden shows in a reason strip at the bottom of the pane,
  above the Temporary bar, following the selected dimmed row; not inside the
  tree.

## Spike Results

### D (2026-11-20, code map)

Both tabs already reach one action entry (`ContextMenuViewModel` →
`VisibilityController.RunAsync` → `VisibilityPlanner` → `IViewActions`), and
selection goes through `PanelRuntime.SelectAsync`. To fold into the engine:

1. Branch ids: `VisibilityPlanner.AddBranch` uses `snapshot.IdsUnder` (whole
   model, secondary design options included, Scope ignored) while
   `ExplorerTabViewModel.ElementsOf` uses the scoped ids. Hide, isolate and
   overrides on a branch can act on more elements than Select and the counts.
   Resolve rows to `ElementRef`s once in the engine and plan only from them.
2. Category merge written three times (`ContextData.MergedGroups`, inline in
   `ShowQuickCounts`, unused `SnapshotAggregator.MergeGroups`): one home in
   `SnapshotAggregator`, which part G also uses.
3. `ScopeCounter.Count` counts secondary design option elements;
   `SnapshotAggregator` and `InModel` skip them.
4. Hidden state: Explorer reads back for its visible rows; Filter only asks
   `IsHidden`, so a Filter result never shown in Explorer reads "not hidden".
   One engine read-back by `ElementRef` (also serves E).
5. Override marks are recorded per row; Filter requests carry no rows, so
   halftone or color from Filter is not marked. Mark by `ElementRef`.
6. Link references built twice (`RevitSelectionPort.LinkReferences` drops
   unresolved links, `LinkElements.ReferencesOf` throws): keep `LinkElements`.

### F (2026-11-20, Revit 2026, Architectural copy opened as the Current model)

`{3D}`, three hosts (Table-Dining Round w Chairs, Kitchenette, Window-Sliding
Double), read with `IsElementVisibleInTemporaryViewMode`, the view collector
and screenshots:

- `IsolateElementsTemporary(host)`: nested instances hidden (table without its
  chairs).
- `HideElementsTemporary(host)`: nested instances stay visible.
- `HideElements(host)` (permanent): nested `IsHidden` false.
- `SetElementOverrides(host)`: the nested chairs keep their own graphics.

So the engine expands ids with `GetSubComponentIds` (recursive) before T0
isolate, hide and overrides, and Reset uses the same set
(`NestedComponents.AddWithNested` in the adapter).

Linked host (Snowdon Federated, host `{3D}`, Architectural link 1362429, table
738550): the internal Hide in View (T3) on the linked host reference hid the
host and its four nested chairs (link view collector before: all visible,
after: none); undo restored all. T3 needs no expansion. The HVAC model has no
host with nested shared instances; the Architectural link has 126, the
Reload-Link 23.

### C (2026-11-20, Revit 2026, host `{3D}`)

`Selection.PickObjects(ObjectType.LinkedElement, filter, "Picking · Esc to
finish")` started inside an external event turn (the panel's dispatch path);
Escape sent to Revit ended it with
`Autodesk.Revit.Exceptions.OperationCanceledException` after 12.4 s, no
error, Revit responsive. Picking without Tab, box selection and the filter
result need a user click; recorded as a manual check.

### B1 (2026-11-21, Revit 2026, Snowdon HVAC, plan L3 with template "Mechanical Plan", Architectural link By Host View)

- A rule filter (Doors, Width > 0) with a red override added straight to the
  view: create 0 ms, add and override 25 ms, regenerate 6 ms, but nothing
  changed on screen. The template controls Filters; `AddFilter` still
  succeeds and `GetFilters()` lists it, while Revit draws only the
  template's filters.
- The same filter added inside Temporary View Properties (the mode the panel
  already uses for Halftone and Color, 0062 Decision 6): the linked doors drew
  red; 689 ms including the mode switch and regeneration. Undo removed the
  filter element and ended the mode.
- So B1 must run inside Temporary View Properties, and its cost is one
  `ParameterFilterElement` in the model while the mode is on. That model write
  is still the owner's call before B1 is built.

### Category isolate (2026-11-23, Revit 2026, Snowdon HVAC, `{3D}` without template, six links By Host View)

The owner's Isolate on Doors (`IsolateCategoriesTemporary` with RVT Links) was
still active: the host view collector returned only the 6 link instances, while
the link collectors still returned every element (Architectural 7,823).

One rule-less filter with 169 filterable model categories (all but Doors and RVT
Links), visibility off, inside a rolled-back transaction group: apply 1,680 ms.
Active view link collector, before → after: Architectural 7,823 → 296 (Doors 141
→ 141), Structural 1,466 → 23, Facades 564 → 40, Plumbing 6,750 → 15, Electrical
2,669 → 13, Site 92 → 3. What stayed besides Doors: Lines, room and space
separation, model groups, divisions, wall openings, multistory stairs (not
filterable) and levels, scope boxes, adaptive points (not model). After the
rollback every count was back and no filter was left.

### Category isolate by category hide (2026-11-23, Revit 2026, Snowdon HVAC)

Inside Temporary View Properties, hide every `Settings.Categories` entry that
`CanCategoryBeHidden` allows except Doors and RVT Links; active view collectors,
before → after; then disable the mode:

- `{3D}` (no template): 321 hidden (208 annotation, 102 model, 11 analytical),
  57 refused, 222 ms. Current model 4,816 → 9 (RVT Links 6, Cameras 2, one
  uncategorized); Architectural 7,823 → 183 (Doors 141, Model Groups 29,
  Divisions 10, Multistory Stairs 3); Structural, Facades, Plumbing, Site → 0;
  Electrical → 1 (Divisions). The view filter left 296 in Architectural (lines,
  room and space separation, levels, scope boxes too).
- Plan L3 (template controls V/G; 384 categories refused outside the mode, 54
  inside): 142 hidden (47 annotation), 185 ms. Current model 1,048 (duct tags
  73, space tags 17, grids 15, …) → 7 (RVT Links 6, one uncategorized);
  Architectural 1,130 → 45 (Doors 24, Divisions 10, Model Groups 8,
  Multistory Stairs 3); Electrical → 1; the rest → 0.
- Disabling the mode (1.5–2.3 s with regeneration) restored every host and link
  count exactly in both views.

### A1 (from 0062 Context 12, not re-measured)

Isolate inside a link by hiding the rest through T3: about 730 ms for 7,818
hides and about 5.5 s to show them again, saved with the view. No new spike
was run: the numbers already decide that A1 waits for demand.

### E, first try (2026-11-20)

The link view collector built for a plan that was not the active view crashed
Revit 2026 natively (README Gotcha 1). Why hidden reads links only in the
active view, through `LinkElements.InView`.

### E (2026-11-20, Snowdon Federated, active plan L3, Architectural link By Host View)

- Link view collector in the active view: 1,130 visible in 2 ms. All 17,760
  model-category elements of the link read in 259 ms; checks 3–9 over the
  16,653 not visible took 301 ms (about 18 µs each). One row is far under the
  50 ms target; a group's "+N hidden" is one set difference.
- Causes found: view range 12,043, category 1,304, design option 63; the rest
  had no cause from checks 3–9.
- The unexplained samples sit above the cut plane (doors and walls of L4/L5,
  z ≥ 32.2 ft, view depth 18.8 ft, top 32.2 ft). Check 8 compares with the cut
  plane and the view depth, not the top: cues "Above cut plane" and "Below view
  depth".
- The "not visible" set holds elements no view draws (materials, sketches,
  legend components, rail path lines). Hidden counts start from the snapshot's
  categories and instances, not from every element of the link.
- Check 10 (owner view) runs before the view range: detail lines of the link
  matched the view range by their box.
- Host view filters on linked elements: `PassesFilter` of a host
  `ParameterFilterElement` on a link element was not run (cross-document risk).
  Evaluate through the panel's own evaluator with the filter's rules, or cue
  "Filter: <name> (possible)".

## Validation

- Focused proof: L2 tests per action on Snowdon links (counts read back with the
  link view collector), safety guard before and after; for E, one L2 case per
  check (cause built in a throwaway view, cue read back).
- Integration proof: L3 steps by AutomationId for the new controls; the pick
  itself needs a user, so its cancel path is checked by UIA (Escape) and its
  result by a manual run recorded in this plan.
- Repository-required checks: build skill, L1, `scripts/format-lint.ps1`.

## Result

Complete after implementation.

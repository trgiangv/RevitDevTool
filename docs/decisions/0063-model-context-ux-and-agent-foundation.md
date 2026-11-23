# 0063 Model Context: UX and Agent on One Foundation

Date: 2026-11-20

## Status

**Proposed.** Extends [0062](0062-model-context-panel.md); 0062 keeps the panel
scope, tiers and link limits. Accepted once the first action built under this
decision (see the plan) passes its no-training check.

## Context

The filter core is now verified on real models: on three heavy models
(federated Snowdon, Golden Nugget, a Japanese sample) the panel matches an
independent oracle on about 9,500 rule evaluations and 300 random trees, and
matches Revit schedules, with no crash and calls under about 100 ms
([host matrix plan](../plans/active/2026-11-19-models-context-host-matrix.md)).

Revit add-ins in the same space (DiRoots OneFilter, Ideate Explorer and
BIMLink, ModPlus, Filter+) cover more actions: color by value, bulk parameter
edit, Excel round trip, room-based search, warnings. They work as dialogs or
spreadsheets run one pass at a time, need training, and OneFilter does not
filter linked elements. Excel export and update are not hard to build
(DiRoots SheetLink already does them). Coordination tools such as Revizto work
on an exported copy and serve a different job.

What users fight in Revit (Autodesk Revit Ideas, AUGI, the pyRevit hackathon,
2026-11):

- **Select by condition.** Long-standing ideas ask to use filters as a
  selection tool, select by properties, and add a filter box to the status
  bar. Revit can hide or override by a filter but cannot select by one.
- **Links.** Selection filters skip links, a parameter must exist in the host
  to filter a link, and picking a linked element needs Tab.
- **"Why is this element not here?"** About a dozen visibility mechanisms
  stack (element hide, category, filter, workset, phase, design option, view
  range, crop, temporary modes, link display). Users check them one dialog at
  a time.
- **Isolate by MEP system** is a frequent complaint.

The two top entries of the pyRevit hackathon #1 (2026) build two of these as
separate windows: *Why Not Here?* (10 visibility checks with Fix and Undo) and
*FilterMan* (category tree, condition chips, Excel, isolate and color). Both
read the Current model only, run once and do not follow edits; FilterMan
compares formatted strings and caps a search at 2,000 elements.

The question is what makes Model Context worth using next to them, and how it
grows without turning into another tool that needs training.

## Decision

1. **The edge is UX and Agent, not feature count.** A feature is added only
   when it keeps the panel usable without training. Few controls, a clear flow,
   short status cues (no guide sentences), HandyControl stock look.
2. **One result, many actions.** The filter result (Current model and link
   elements, kept live) is the one object every action takes: select, pick,
   hide, isolate, color, export, edit. A new action adds at most one control and
   no setup step of its own (no category or column picker that repeats what the
   filter already says).
3. **No Run button.** Results stay current with edits, undo and redo; an action
   always works on what the panel shows now.
4. **Links are first class.** Every action is designed for mixed results. Where
   Revit refuses a linked element (0062 Context 5, 11), the panel says so with a
   short cue and the action covers what it can; it never switches link display
   modes or writes to the model behind the user's back.
5. **Explorer and Filter are two UIs on one engine.** Element reads, parameter
   reads, link handling and actions live in the engine once; neither view keeps
   its own path. A new action is added to the engine and reached from both.
6. **Agent uses the same core.** An MCP filter tool builds the same filter
   definition, runs the same evaluator and shows its filter in the panel, so the
   user sees and checks what the agent found before an action runs. No second
   query path for agents.
7. **Correctness stays proven.** Each new read path joins the oracle and sweep
   suites before it ships; safety guards stay mandatory in live tests.
8. **No-training check.** A new action is accepted when a first-time user gets
   its result from the panel without reading docs; the UI test for it uses only
   AutomationIds and the steps a user would take.

## Alternatives Considered

1. **Match OneFilter and Ideate feature by feature.** More dialogs and options;
   loses the reason to switch.
2. **Excel first** (export, edit, import as the main flow). Easy to build, but a
   batch workflow that needs format rules and training.
3. **Agents write their own queries through `code_mode`.** Flexible, but each
   answer is unverified and the user cannot see it in the panel.
4. **A separate agent UI.** Two ways to filter, two sets of bugs.
5. **A separate tool window per job** (a visibility checker next to a filter
   browser, as in the hackathon entries). Each one repeats element pick, view
   choice and link handling; the panel already has the row and the view.

## Consequences

Positive:

- One learning step (build a filter) covers every action.
- Agents get verified, live model context without writing code.
- Link support stays the visible difference from other add-ins.

Tradeoffs:

- Some actions other tools have will come later or not at all when they need
  their own setup flow.
- Link actions are bounded by Revit (0062 Context 5): per-element isolate and
  overrides inside a link need the work in the
  [link actions plan](../plans/active/2026-11-20-models-context-link-actions.md).

## Follow-Up

- [Link actions plan](../plans/active/2026-11-20-models-context-link-actions.md).
  Built (2026-11-21): one engine for Explorer and Filter (part D), active
  select by category and source file as Pick (part C), Explorer scope boxes
  (part G), nested shared families (part F). Open: isolate and graphic
  overrides for specific linked elements (parts A and B).
- *Why hidden* (same plan, part E), built (2026-11-21): a read-only visibility
  check for one row, Current model or linked, in the active view; a Show only
  where an existing tier already undoes the cause. The same check serves the
  MCP tool.
- No-training check (Decision 8) pending: the live L2 and L3 runs for these
  actions and the manual Pick check (a click or box in the view needs a person).
  Status stays Proposed until they pass.
- MCP filter tool on the same core (0062 direction).
- Expand by MEP system: the deferred context strip (0062 Decision 13), once a
  MEP user asks for it.
- Later actions, one at a time under item 2: color by value, Excel
  export/update of the result (with a preview of what changes before it
  writes), bulk edit.

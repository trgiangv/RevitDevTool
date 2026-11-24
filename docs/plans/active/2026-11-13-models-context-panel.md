# Execution Plan: Models Context Panel

Date: 2026-11-13

## Status

Active

## Outcome

A dockable **Model Context** panel in Revit 2023+ that browses, filters and
selects elements of the Current model and its links, and shows, hides,
isolates, halftones or colors them in the active view, as specified in
[0062](../../decisions/0062-model-context-panel.md). The core and the views run
and are tested out of process on a fake model; the Revit adapter is proven by
live L2 and L3 suites.

## Context

- Decision: [0062](../../decisions/0062-model-context-panel.md) holds behavior,
  architecture, UI and targets, storage, ownership and the open spike steps.
  The design canvas *Model Context Panel - UX directions* is secondary; 0062 wins.
- Behavior and structure: [product/models-context.md](../../product/models-context.md),
  [architecture/ModelsContext](../../architecture/ModelsContext/README.md).
  Tests and golden values:
  [test-matrix](../../architecture/ModelsContext/test-matrix.md).
- Boundaries: [host-boundaries](../../agents/host-boundaries.md). Core is shared
  (`source/DevTools.ModelsContext`, no host API, no WPF). Views live in
  `source/DevTools.Presentation/Views/ModelsContext` (no separate Views project).
  The Revit adapter is `source/RevitDevTool.Tools/ModelsContext`.
- Controls in `source/DevTools.UI`: `FlatTreeList`, `AutoCompleteComboBox`
  (parameter picker) and `TagCheckComboBox` (the documents box, not the
  HandyControl `CheckComboBox`). `libs/HandyControl` is never edited.
- Tests: `DevTools.ModelsContext.Tests`, `.Views.Tests`, `.Tests.Shared`, the
  `DevTools.ModelsContext.Sandbox` app (`--measure`), and the live projects
  `RevitDevTool.ModelsContext.LiveTests` (L2) and `.UiTests` (L3).
- Live setup: MCP `launch_host` without a file, then `code_mode` `open_document`
  (a model opened at startup breaks pane creation on Revit 2024, see Open).
  FlaUI drives only the panel's own controls.

## Scope

In scope:

- The core, views, `DevTools.UI` controls, the Revit adapter (T0-T3, pane,
  filter compiler and catalog), tests, sandbox, CI wiring and docs.
- The open spike steps and version runs in Open below.

Out of scope:

- Context strip and context popup (0062).
- Replace / writing parameter values.
- An AutoCAD adapter (the core allows it; it is not built here).
- Nested links in T3.

## Approach

Phases ran in order; the main session kept this plan, reviewed each diff and
recorded proof. Remaining work is the Open list. Full phase history is in git
(`git log main..HEAD`, plan revisions before 2026-11-19).

## Done

| Phase | What landed | Latest proof |
|-------|-------------|--------------|
| 0 Scaffolding | Core, Tests, Tests.Shared projects, slnx, CI wiring (`ci-jobs.json`, `ProjectGraph`) | Builds on all TFMs; `Build.Tests` green |
| 1 Core model and engine | Model, ports, engine (snapshots, deltas, scope counts, planner, selection guard, journal, debouncer), seeded fake model | Core tests on net48, net8, net10 |
| 2 View models | `ModelContextViewModel`, context bar, `ExplorerTabViewModel`, filter tab, temporary bar, context menu | Core tests |
| 3a-3c Controls, views, sandbox | `FlatTreeList`, icons, views, `Views.Tests`, sandbox, lifecycle (sessions, generations, version rule, quick counts) | Core and Views tests |
| 3d Views move and perf | Views moved into `DevTools.Presentation/Views/ModelsContext` (guard `PresentationXamlUsesNoSelfUris`), stock HandyControl styles, `RowIdIndex` | Expand 200 instances 13-43 ms, snapshot 37 MB per million (Release sandbox) |
| 4 Revit adapter T0-T2 | `DocumentRegistry`, model source, events, selection port, view actions, filter evaluator and catalog, pane, ribbon command, snapshot export script | Tools builds for 2023, 2025, 2026, 2027; merged host builds; live via L2/L3 |
| 4b Filter run and read-back | Revit filters run in their own document (`GetElementFilter()`); store errors reach the status line; read-back asks only touched ids; ribbon icon | Core, Views, host builds |
| 5 Spike and T3 | T3 decided 2026-11-15: Hide in View and Show for link rows through `LinkedHideShow` (Reveal Hidden + `invokeCommandHandler`, one undo entry, journal-scoped Reset); Isolate, Halftone, Color on link elements disabled. Verdicts in 0062 | L3 UI-06 |
| 5b Panel UI revision | Explorer/Filter switch and scope as icon button groups, Selection sync toggle, `TagCheckComboBox` documents box (last tick stays, Current model unticking like a link), `PanelButtonGroup` UIA peer, Expand/Collapse all | Views tests |
| 5c Parameter identity | One match key per parameter kind, derived kinds and operators, per-document catalog and compile, family parameters native, case-sensitive two-pass text rules, picker by category, docs pages | Live FlaUI check on Revit 2026 matched the API counts; L2 matrix |
| 5d Filter logic | AND/OR connector, group scope (documents, categories), drag and Alt+Up/Down reorder, renamed operators, store without `not`, view model splits (`ParameterScopes`, `FilterTreeMoves`, `CustomFilterLibrary`, `FilterRunner`), Flow 9 picker rows (Instance/Type sections, source icons, tooltip, no tags) | L1 `FilterLogicTests`, `FilterTreeMovesTests`, Views `FilterLogicViewTests`, `FilterDropTests`; L2 GS/GR cases |
| 6 Docs and live suites | Product and architecture pages, test matrix, L2 (adapter in Revit) and L3 (panel flows), golden values from the Revit 2026 sample | Run below |
| 7 steps 1-3, 6 | Revit 2024 deploy, MCP setup, first 2024 run, budgets measured | Run below |
| 7 L3 hands-off | `RevitPanel.Choose` via `ItemContainer`, `Shot` via `PrintWindow`, `Act` logs Revit coming to front | L3 13/13 (6 flows, 7 data rules) in 73 s, run `20261118-133615` |
| 7 perf and first load | Quick counts one collector plus 30 ms slices (EX-05 4,044 to 103 ms); a rule raises `Parameters` only for a new list and the picker groups only while open (example 4 build 1,812 to 83 ms at 100k); `FilterTabView` built on first Filter tab open | Sandbox `--measure`, Revit 2026 live |
| 7 picker sections | Collapsible Instance/Type sections (choice kept, search shows all), name-only rows | Views 103/103, core 341/341 |
| 7 launch_host | Daemon `BridgeTimeout` 10 min | Needs `pack daemon` and reinstall to take effect |

Latest live runs (2026-11-18):

- Revit 2026, run `20261118-103516`: L2 130/130 in 110 s, L3 6/6. Later L3 run
  `20261118-133615`: 13/13 in 73 s.
- Revit 2024, run `20261118-093432`: L2 127/131 in 287 s, L3 5/6. Mismatches come
  only from sample golden values (SC-02, SC-06, UI-04) and SB-01 rebar; listed in
  the test matrix under Mismatches. Revit 2024 runs about 2.6 times slower
  (visibility about 5 times).

Performance, pass of 2026-11-18 (sandbox `--measure`, Release; budgets from 0062):

| Check | Budget | 100k | 1M |
|-------|--------|------|----|
| Group parameter list cold / warm | <= 5 ms / hit | 5.2 / 0.08 ms, hit | 1.1 / 0.005 ms, hit |
| Scope pruning per document | <= 1 ms | 1.4 us | 7 us |
| Picker keystroke, 3k items | <= 16 ms | 18-21 ms median | 17-22 ms |
| Live edit to result | <= 500 ms (300 ms debounce) | 333-433 ms | 340-476 ms |
| UI stall | < 50 ms | misses, see below | misses, see below |

Still over the 50 ms UI budget:

- Layout of new rule templates: 47-154 ms for 4-6 nodes, about 15-25 ms per rule
  from the HandyControl templates; shows when a filter loads many rules.
- First load: 100-300 ms longest stall (33-59 ms in Phase 3d on a quieter
  machine; three Revit sessions ran during the measure).
- `example 4` evaluated: 80-140 ms.

Header expand read-back (`GetIdsHiddenPermanentlyOrTemporarilyAsync`) asks for all
in-scope ids on first expand, in slices, cached per view: no UI stall, kept.

## Open

- [ ] Pre-warm the panel on Revit idle (hides about 80% of the first build for about
  0.3 s of idle) and a lighter rule template. Measure with sandbox `--measure`
  (first load, rule template layout); target under 50 ms.
- [ ] Revit 2026 visibility costs: VI-11 takes 2.5 s plus a 2.7 s reset, and Reset
  writes prior element overrides to the permanent view. Run `measure_vis.cs`
  (not in the repo; recreate it or measure with `scripts/test-models-context.ps1`)
  on a session with mains power.
- [ ] SB-01 rebar on Revit 2024: `REBAR_MIN_LENGTH` and `REBAR_MAX_LENGTH` on two
  rebar sets in the Structural link fail to find their own value (4 of 11,612
  samples); not seen on 2026. Investigate with `WriteMismatchedElements`.
- [ ] Revit 2024 pane gap: panes registered in `ApplicationInitialized` (Model
  Context and the main DevTools pane) exist only when a document opens after that
  event, so Revit started with a model leaves them "not created" and the ribbon
  buttons do nothing (`ToggleVisibility` returns when `PaneExists` is false).
  Fix candidate: register in `OnStartup`; prove with `launch_host` with a file on
  2024. Revit 2026 is not affected.
- [ ] T3 `code_mode` spike step on 2024 (Hide in View, Reveal + Unhide, temporary
  checks) through `execute_csharp_code`; L3 UI-05/UI-06 already pass on 2024.
- [ ] Open spike steps from 0062 that 2024 can answer: view template locking V/G,
  500 refs hid 548, link display modes, T3 timing at 1k/10k/50k. Record results
  here and in 0062.
- Per-version golden values and the Revit 2023, 2025 and 2027 live runs (0062 Accepted) live in
  [2026-11-19-models-context-host-matrix](2026-11-19-models-context-host-matrix.md) Progress.
- [ ] Later splits, off the critical path: `ModelContextViewModel` (event inbox,
  context loader) and `ExplorerTabViewModel` (five parts).

## Risks And Recovery

- T3 Show depends on an internal Revit call (Reveal Hidden plus
  `invokeCommandHandler`): when missing or without effect on a version, link rows
  keep Hide and disable Show (0062). Verify per year in Open.
- A post-and-Idling queue for link commands failed from the panel and crashed
  Revit once; only the synchronous `LinkedHideShow` path is allowed.
- Link view collector ignores temporary hide: rows the panel hid stay in the tree
  from the journal; no design change.
- A port that needs a host object: stop and amend this plan; no host type in the
  core.
- Recovery: work is in small commits on `feature/model-context-panel`; revert a
  commit without touching earlier ones. Never rewrite below a release tag.

## Decisions

Policy lives in 0062, [product/models-context.md](../../product/models-context.md)
and [architecture/ModelsContext](../../architecture/ModelsContext/README.md). Task-local:

- 2026-11-13: groups are `long GroupId`; All models rows carry the Current model
  `DocumentKey`; the planner returns no partial plans; `SelectionGuard` records
  expire after 2 s on `IClock`; rule values are text and the adapter parses units.
- 2026-11-13: default scope Active view, Selection sync on; an empty custom filter
  matches nothing and Live sends nothing; Duplicate drops unsupported rules and
  names the copy "X (2)".
- 2026-11-14: an unloaded link stays ticked with rows from the kept snapshot and
  "not loaded" as the reason; the version check compares `VersionGUID` only;
  "too large" means changed ids over 20% of the snapshot; changes while the pane
  is hidden mark the snapshot dirty.
- 2026-11-15: parameter match key by kind; a Family parameter in one document and
  a Project parameter in another with the same name and data type are one picker
  item with both origins.
- 2026-11-18: Revit 2024 is the live test host on this machine; Revit and the
  sample open through MCP; `scripts/test-models-context.ps1` takes the pid; FlaUI
  drives only the panel.
- 2026-11-18: L3 must not take the user's mouse, keyboard or screen.
- 2026-11-18: host log routing stays as is; Model Context Trace shows only at the
  Trace setting.

## Validation

- Focused proof: the two L1 commands in the
  [test-matrix Run section](../../architecture/ModelsContext/test-matrix.md#run):
  `dotnet run --project tests/DevTools.ModelsContext.Tests/DevTools.ModelsContext.Tests.csproj -f net10.0-windows`
  and the same for `DevTools.ModelsContext.Views.Tests`. Last: core 341/341,
  Views 103/103.
- Integration proof: `pwsh -File scripts/build-host.ps1 -HostApp Revit -Year <year>`,
  MCP `launch_host` and `open_document`, then
  `pwsh -File scripts/test-models-context.ps1 -RevitPid <pid> [-Year <year>]`
  (L2 then L3). Sandbox performance: `--measure`.
- Repository-required checks: build skill per touched project,
  `pwsh -File scripts/format-lint.ps1` once per task, CI `result` green.

## Result

Complete after implementation.

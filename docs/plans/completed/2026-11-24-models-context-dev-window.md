# Model Context window command

## Goal

Change Model Context code and see it in Revit without restarting Revit. `RevitDevTool.Tools` gets one
`IExternalCommand` that opens the real panel (`ModelContextView` on the Revit adapter) in a plain
modeless WPF `Window`, not the Dockable Pane. Run it through Command Execution (panel or MCP
`run_command`) on the built `RevitDevTool.Tools.dll`: each run loads that build fresh in an isolation
session ([assembly-isolation.md](../../product/assembly-isolation.md)), so build, close the window, run
again. This comes before the remaining Model Context work
([link actions](../active/2026-11-20-models-context-link-actions.md), [host matrix](../active/2026-11-19-models-context-host-matrix.md)).

## Decisions

1. **Command in `RevitDevTool.Tools`.** `RevitDevTool.Tools.ModelsContext.Pane.ModelsContextWindowCommand`,
   beside the pane code. It does not use the add-in's `Host` DI, because a fresh load has none: it
   builds its own services through the same wiring the pane uses.
2. **One wiring path.** The pane and the window build `ModelContextServices` and subscribe events
   through the same adapter code. Refactor `ModelsContextExtensions` / `ModelsContextPane` so both use
   it, with no copied wiring. Events subscribe from a `UIApplication` (command) as well as a
   `UIControlledApplication` (startup).
3. **Fresh copy, own executor.** A Command Execution run loads its own copies of `RevitDevTool.Tools`
   and its dependencies (`RevitDevTool.Core`, `DevTools.ModelsContext`, `DevTools.Presentation`).
   Static state (`RevitContextExecutor`, `RevitProfiler`, `LinkViewFilters`, `ViewActivations`) is per copy.
   The command makes sure its copy's executor can raise its `ExternalEvent` (created inside `Execute`,
   an API context).
4. **One panel at a time.** Before opening, the command hides the docked Model Context pane and closes
   any Model Context window left by an earlier run (found by AutomationId `ModelContext.Window` among
   the WPF windows, since its type belongs to an older load). Two live copies would sweep each other's
   `Model Context <n>` filters (gotcha 25) and both answer Revit events.
5. **Close tears down.** Closing the window unsubscribes Revit events, unwatches view activations,
   disposes the view model and removes panel view filters (the pane-hide path). Temporary modes and
   recorded overrides live in that load's memory only: press Reset before closing.
6. **Window.** Modeless, owned by the Revit main window, title from `ModelContextViewModel.PanelTitle`,
   AutomationId `ModelContext.Window`, about 360 x 800, HC `RegionBrush` background. Nothing throws out
   of `Execute`: a failure returns `Result.Failed` with one message.

## Known limits

- `DevTools.UI` (FlatTreeList, TagCheckComboBox, AutoCompleteComboBox, theme) binds to the add-in's
  loaded copy (gotcha 14). Edits there still need a Revit restart.
- net48 years (2024 and below) load each run as a new copy that is never unloaded. Restart Revit after
  many runs. Revit 2026 (collectible ALC) is the main target.

## Steps

- [x] Verify Decision 3 in code: running `RevitDevTool.Tools.dll` from its build output through Command
      Execution loads fresh copies, and the add-in's ILRepacked copies cause no type clash.
- [x] Refactor the wiring (Decision 2), no behavior change; core, view and adapter builds pass.
      `ModelsContextPanel` holds start, build and stop; the command subscribes through
      `RevitContext.UiControlledApplication` (built from the `UIApplication`), so `Subscribe` keeps one signature.
- [x] Add the command (Decisions 1, 4–6); build 2026, 2025 and 2022.
- [x] Doc: the run loop (build command, `run_command` path and entry, known limits) in
      `docs/architecture/ModelsContext/test-matrix.md`; link from the architecture README Testing section.
- [x] Live check on Revit 2026 (owner announces it, it takes focus): run the command, change a visible
      string, rebuild, close, run again: the new window shows the change, no restart. Hide a linked
      element, Reset, close; no crash; no `Model Context <n>` filter left.
      Found and fixed on the way: XAML that named core types bound to an older loaded copy (gotcha 28,
      guard `NoCoreTypesInXaml`); a user close ran `Stop` outside the API context (now through
      `RevitProfiler.RunAsync`); the window logs to `models-context-window_*.log`.

## Result

Done 2026-11-24. The window command, the shared wiring and the run loop doc landed, and the live reload
loop passed on Revit 2026. The one note found on the way, the orange dot (`Explorer.Row.SavedHide`)
missing on a linked Doors row hidden through Hide in View, moved to the linked-hide record work: test
matrix LH-03 (L1 `ActiveViewLinkHideKeepsRow` passes; the L3 check stays with the UI round).

## Validation

| Check | Result |
|-------|--------|
| Builds (core, views, adapter 2026/2025/2022) | Pass 2026-11-24 (also add-in 2026 compile-only) |
| Core 492 and view 132 tests still pass | Pass 2026-11-24 (net10: 492/492, 132/132) |
| Live reload loop on 2026 | Pass 2026-11-24, pid 30328, Snowdon HVAC: 6 runs in one session (4+ copies loaded); title change shown after rebuild; Current model hide + Reset; linked Doors hide (T3) + Reset; close by rerun and by user, both log "closed"; view clean (no temporary modes, 0 `Model Context` filters) |

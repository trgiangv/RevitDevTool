# Execution Plan: Model Context Vocabulary and Structure Cleanup

Date: 2026-11-27

## Status

Active. Independent review done (2026-11-27); waits for the visibility race fix
(A/B/C/D) and the eye-selection / Sheets work to be committed first.

## Outcome

One name per concept across core, adapter and views, matching the product
words in [models-context.md](../../product/models-context.md); no dead members
or leftover workarounds in the read-back and visibility code; behavior
unchanged (L2 and L3 stay green).

## Context

- Product words: `docs/product/models-context.md`, `docs/product/filters.md`.
- Architecture: `docs/architecture/ModelsContext/README.md` (read-back, tiers,
  journal, ledger).
- Code: `source/DevTools.ModelsContext/**`,
  `source/RevitDevTool.Tools/ModelsContext/**`,
  `source/DevTools.Presentation/Views/ModelsContext/**`.
- Repo rules: no "Gate"/"Shim" vocabulary; "check/checked" only for checkboxes.

## Vocabulary

| Concept | Today | Target |
|---|---|---|
| Ask Revit which Current model ids are hidden in the active view | `HiddenProbe`, probe, `ProbeOf`, `VisibilityPlan.ReadBack`, `readBack` parameter | `ReadBackRequest`, `RequestFor` |
| Its answer | `HiddenRead`, `PlanOutcome.ReadBack`, `_read`, `TakeRead`, `Seed` | `ReadBackResult`, `TakeReadBack`, `Store` |
| Ids already read | `Checked`, `CheckedOf`, `checkedIds`, `SilentChecked` | `ReadIds`, `ReadOf`, `SilentRead` |
| Run the read-back | `ReadAsync`, `ReadBackAsync`, `RefreshHiddenAsync`, `ReadHiddenAsync` ×2 | `ReadBackAsync` at every layer |
| Inner cache entry | `ReadBackCache.ReadBack` | `ReadBackState` |
| Drawable but not in the view collector | `OutsideIds` | `UndrawnCandidateIds` |
| Rows shown dimmed | `Unseen*`, `TreeRow.HiddenCount` | `Dimmed*`, `DimmedCount` (AutomationId follows; update UI tests) |
| Hidden but out of scope | `DocumentState.Outside` | `HiddenOutOfScope` |
| Parameter no rule can see | `Silent*`, `ParameterPresence.UnseenIds` | `Silent*` only (`SilentIds`) |
| No owner view, primary option | `InWholeModel`, `InModel`, `modelOnly`, `FreeFilter` | `InWholeModel`, `wholeModelOnly`, `NoOwnerViewFilter` |
| Listed categories / with elements | `ListedSet`, `OwnedCategoryIds`, `modelCategories` | `ListedCategorySet`, `CategoriesWithElements` |
| Panel hid it | `HiddenElements/Categories/Links`, `HidByPanel`, `IsCategoryHidden` (controller) | `PanelHidden*`, `IsHiddenByPanel` |
| Revit V/G has it off | `IsCategoryOff`, `HiddenChecks.IsCategoryHidden` | "category off" for the Revit cause only |
| Ids the panel took from the view collector | `CategoryOffIds`, `HiddenInViewIds` | `TakenFromViewIds` |
| Eye state for a document | `HiddenIds` | `EyeClosedIds` |
| Load/action race | `ActionVersion`, `PanelRuntime.Actions`, `Drop()` | `RunningActions`, `TryRefreshNowOrDefer()` |
| Sent-selection echo | `SelectionGuard`, `PanelRuntime.Guard` | `SentSelectionEcho`, `SentSelection` |
| Lock objects | `_gate` | `_lock` |

## Scope

In scope, wave 1 (low risk):

- The renames above (types, members, parameters, docs, tests, AutomationIds).
- Delete dead members: `PlanOutcome.None`; `IRevitModelEvents` (inject
  `RevitModelEvents`); move `UsesLinkFilter` to a test helper or document T4.
- `ReadBackCache` holds one view entry instead of a dictionary cleared on
  every view change.
- `VisibilityController.ApplyAsync`: one `Touch` on the final view; no `Drop`
  right before the store.
- `RefreshHiddenAsync` folds into `ReadBackAsync`.
- `KeepAsync`/`ResetAsync` stop bending to the generic `ActAsync<T>`.
- `PanelRuntime` moves to `Panel/PanelRuntime.cs`.
- `ExportSnapshots`: drop the redundant `modelCategories.Contains`; the test
  matrix says it shares `CategoryRule` (not an independent oracle).
- `ViewDraws.Draws(ViewDraw, ViewKind)` used by the core and `HiddenChecks`.

In scope, wave 2 (structure):

- `PlanExecutor.ApplyAsync` returns a result record (notice, read-back) instead
  of `Take*` side fields; only the last Revit call of an action carries the
  read-back request.
- The core ledger sends the panel's off categories in the read-back request;
  `RevitViewActions._categoriesOff` and its `Rename` go.
- Split `VisibilityController`: `ViewLedger` (panel intent) and a view hidden
  state (eye answers, read-back, not-drawn set).

Out of scope until a live trace shows the need:

- `ReadBackCache._touched` growth.
- Merging `SelectionGuard` and `IsLocalSelectionPending`.

## Approach

1. Commit the race fix and the eye-selection / Sheets work first.
2. Wave 1 in one worker; the reviewer reads the diff against this table.
3. Wave 2 in one worker after wave 1 is committed; L2 and L3 live runs.

## Risks And Recovery

- AutomationId renames break UI tests silently: grep tests and live test data
  for every renamed id in the same change.
- Wave 2 touches the read-back path that keeps eyes right: run the visibility
  L2 cases and the race reproduction from the race report.
- Recovery: each wave is one commit; revert it.

## Progress

- [x] Independent review (2026-11-27).
- [ ] Race fix and eye-selection work committed.
- [ ] Wave 1.
- [ ] Wave 1 reviewed.
- [ ] Wave 2 and live runs.

## Validation

- Core, views and UI-rule tests; CommandBuild; L2 (and L3 for wave 2) on
  Revit 2026.
- `scripts/format-lint.ps1` once per wave.

## Result

Pending.

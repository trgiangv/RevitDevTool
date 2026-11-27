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
| Ask Revit which Current model ids are hidden in the active view | `HiddenProbe`, probe, `ProbeOf`, `VisibilityPlan.ReadBack`, `readBack` parameter | `ReadBackRequest(Document, Ids)` on `VisibilityPlan.ReadBack`, `RequestFor` |
| Its answer | `HiddenRead`, `PlanOutcome.ReadBack`, `_read`, `TakeRead`, `Seed` | `ReadBackResult(Document, Read, Hidden)` on `PlanOutcome.ReadBack`, `TakeReadBack`, `Store` |
| Ids already read | `Checked`, `CheckedOf`, `checkedIds`, `SilentChecked` | `Read`, `ReadOf`, `SilentRead` |
| Run the read-back | `ReadAsync`, `ReadBackAsync`, `RefreshHiddenAsync`, `ReadHiddenAsync` ×2 | `ReadBackAsync` at every layer; the port takes a `ReadBackRequest` and returns a `ReadBackResult` |
| Inner cache entry | `ReadBackCache.ReadBack` | `ReadBackState` |
| Drawable but not in the view collector | `OutsideIds` | `UndrawnIds` |
| Rows shown dimmed | `Unseen*`, `TreeRow.HiddenCount` | `Dimmed*`, `DimmedCount` (AutomationId follows; update UI tests) |
| Hidden but out of scope | `DocumentState.Outside` | `HiddenElsewhere` |
| Parameter no rule can see | `Silent*`, `ParameterPresence.UnseenIds` | `Silent*` only (`SilentIds`) |
| No owner view, primary option | `InWholeModel`, `InModel`, `modelOnly`, `FreeFilter` | `InWholeModel`, `wholeModelOnly`, `NoOwnerViewFilter` |
| Listed categories / with elements | `ListedSet`, `OwnedCategoryIds`, `modelCategories` | `ListedCategorySet`, `UsedCategoryIds` |
| Panel hid it | `HiddenElements/Categories/Links`, `HidByPanel`, `IsCategoryHidden` (controller) | `ViewLedger.HiddenElements/HiddenCategories/HiddenLinks` (kept: `Categories` beside `CategoryOverrides` reads ambiguous); ledger `Hides(element, categoryId)` (hidden by id, category, link or isolation) and `HidesCategory(id)` (hidden or isolated out); controller `Hides(element)` (by id or isolation, plus linked hides) and `HidesCategory(id)` (hidden categories) |
| Revit V/G has it off | `IsCategoryOff`, `HiddenChecks.IsCategoryHidden` | "category off" for the Revit cause only |
| Ids the panel took from the view collector | `CategoryOffIds`, `HiddenInViewIds` | `TakenIds`; ledger `CategoryTaken`, `ElementTaken` (`DocumentIds.Add(document, ids)`) |
| Eye state for a document | `HiddenIds` | `HiddenIds` (code names behavior; "eye" is UI wording only) |
| Row hide/show state and toggle | `RowEye`, `TreeRow.Eye`, `EyeOf`, `ToggleEyeAsync`, `EyeToolTip`, `HighlightEyeTargets` | `HideState` (enum and `TreeRow.HideState`), `StateOf`, `ToggleHiddenAsync`, `HideHint`, `ToggleHidden`, `HighlightTargets`; `TreeRow.IsLinkHidden`, `CategoryItemViewModel.IsHidden`, `ClearSelectedInside`, `IsSending`; XAML names, AutomationIds and code-behind handlers keep "Eye" |
| Load/action race | `ActionVersion`, `PanelRuntime.Actions`, `Drop()` | `RunningActions` (`Version`), `Defer()` (false when idle) |
| Sent-selection echo | `SelectionGuard`, `PanelRuntime.Guard` | `SentSelectionEcho`, `SentSelection` |
| Lock objects | `_gate` | `_lock` |
| Forget cached or planned state | `ReadBackCache.Drop`, `DropSource`, `DropReadBack`, `DropLinkFilters` | `Forget`, `ForgetSource`, `ForgetReadBack`, `ForgetLinkFilters` ("Drop" leaves Visibility) |

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
- Merging `SentSelectionEcho` and `IsSending`.

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
- [x] Wave 1 (2026-11-27, uncommitted): renames, dead members, single-slot
  `ReadBackCache`, one `Touch` per attempt, `ReadBackAsync` at every layer,
  `PanelRuntime.cs`, `ViewDraws.Draws`. Core 653, views 183, UI rules 7 pass.
- [x] Wave 1b (2026-11-27, uncommitted): short names (`Hides`, `TakenIds`, `Defer`, `UndrawnIds`, `ReadBack` records); the read-back request is built per attempt for the view that ran.
- [ ] Wave 1 reviewed.
- [ ] Wave 2 and live runs.

## Validation

- Core, views and UI-rule tests; CommandBuild; L2 (and L3 for wave 2) on
  Revit 2026.
- `scripts/format-lint.ps1` once per wave.

## Result

Pending.

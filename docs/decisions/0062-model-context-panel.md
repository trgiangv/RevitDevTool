# 0062 Model Context Panel

Date: 2026-11-13

## Status

**Proposed.** The behavior below is agreed. T3 (actions on single elements
inside a link) waits on the go/no-go spike in Follow-Up. T0–T2, the tree, the
filter engine and selection sync use public API only and do not wait.

## Context

`RevitDevTool.Tools` gets a dockable **Model Context** panel for daily model
work: browse, filter and select elements, then show, hide, isolate, halftone,
color, box or zoom the result in the active view. Elements in loaded RVT links
must work the same way as elements in the Current model.

**Vocabulary.** *Current model* is the open document the user works in; the
panel never says "host". *Link* is one loaded RVT link instance. *All models*
is the tree group that acts on a category everywhere. "Host" appears below
only inside Revit API terms (host view, host `ElementId`, By Host View).

Design canvas: *Model Context Panel — UX directions* (page "Panel + user
flows", flows 1–7). Where the canvas and this ADR differ, this ADR wins: the
notes under Flow 5 still mention an Explore mode (dropped, Alternatives 5). The "Explorations v1" page is kept as history
and uses older words.

Revit limits what a host view can do to elements inside a link. Each item is
marked *verified* (read from the Revit API assemblies 2023.0–2027) or
*believed* (not yet run on a host).

1. `HideElements`, `HideElementsTemporary`, `IsolateElementsTemporary` and
   `SetElementOverrides` take host `ElementId`s. A linked element has none, so
   these calls reach a link only as a whole `RevitLinkInstance` (verified).
2. Host category visibility, permanent or temporary, reaches link elements only
   when the link displays By Host View, and then applies to the host and every
   such link at once (believed).
3. A host view filter is a new element in the project. Under By Host View it
   also applies to link elements for built-in and shared parameters (believed);
   link-only project parameters cannot drive it.
4. `RevitLinkGraphicsSettings` (`GetLinkOverrides` / `SetLinkOverrides`) exists
   from Revit 2024: display mode and `LinkedViewId`; 2025 adds view filter type,
   view range and object styles. No version exposes per-category settings of
   Custom mode (verified). Revit 2023 cannot read a link's display mode; 2024
   cannot tell whether a Custom link uses host or linked view filters.
5. The UI can Tab-select a linked element and hide, isolate or override it.
   API code reaches those commands only through `Selection.SetReferences` with
   `Reference.CreateLinkReference`, then `UIApplication.PostCommand`.
   `PostableCommand` has `HideElements` and `OverrideByElement` but no
   temporary hide or isolate (verified); the temporary commands need
   `RevitCommandId.LookupCommandId`, and whether `CanPostCommand` accepts them
   is unknown. `OverrideByElement` opens a modal dialog (believed).
6. `FilteredElementCollector(hostDoc, viewId, linkInstanceId)` returns the
   linked elements visible for that link in a host view. It is in Revit 2023.1
   and later, not in 2023.0 (verified). The first call per view can rebuild
   view geometry and be slow.
7. Temporary state is readable only per host element
   (`IsElementVisibleInTemporaryViewMode`). Nothing reports temporarily hidden
   categories or hidden linked elements (verified).
8. Revit can hide or override with a filter, but cannot select by one.
9. `UIApplication.SelectionChanged` (2023+) forbids changing the selection or
   the document inside the handler (verified).
10. `UIDocument.ShowElements` accepts host ids only (verified).

## Decision

### 1. Scope and versions

- Revit 2023 and later, latest update of each version (2023 means 2023.1 or
  later, which has the link view collector). The panel is built under
  `REVIT2023_OR_GREATER`; the Revit 2022 add-in does not register it.
- Only two hints read a link's display mode and are built under
  `REVIT2024_OR_GREATER`: the names of affected links in the
  *All models* row tooltip (§7) and the "in view" marker on link filters (§5). On
  2023 the tooltip says "and links that share this view's visibility" without
  names, and link filters have no marker. A Custom link's view filter type needs 2025; on 2024
  such a link shows no marker either.
- The panel never writes element data and never creates an element: no
  parameters, no Extensible Storage, no view filters, no views. It changes only
  the active view's visibility and graphics and existing link instance
  overrides.
- Replace (writing parameter values) is not part of the panel. It can become a
  separate tool that takes the panel's result as input, works on the current
  document only, and writes in one undoable transaction.

### 2. Layout

One dockable pane, 320 px design width, HandyControl Light and Dark (primary
`#326CF3`, radius 4, 12 px text). Top to bottom:

1. **Pane title** with the active view name.
2. **Context bar.** Scope as three icon buttons in a `ButtonGroup`
   (Selection, Active view, Whole model) and one `CheckComboBox` of documents.
   Every tab reads this bar.
3. **Tabs:** Tree (browse, select, and show, hide, isolate, halftone, color in
   the active view) and Filter.
4. **Temporary bar** (yellow), only while the view is in a temporary mode:
   *Temporary · N changes* with **Keep** and **Reset**.
There is no fixed actions bar. Actions live in one **context menu** with an
icon per item, opened on the target itself:

- **Tree:** right-click on the selected rows (or Shift+F10 / the Menu key). A
  right-click on an unselected row selects it first.
- **Filter:** right-click on the result row, or its **⋯** button.

Menu items, top to bottom, each with its icon:

| Item | Does |
|------|------|
| Select in Revit | Sends the target to the Revit selection (one way). Hidden while the tab syncs on its own (Tree Sync on, Filter Live on), shown when sync pauses. |
| Zoom | Zooms the active view to the target. |
| Section box | Sets the 3D view's section box around the target (3D views only). |
| — | |
| Isolate | Same as the row's Isolate (§7). |
| Hide / Show | Same as the row's eye. |
| Halftone | Same as the row's Halftone. |
| Color… | Same as the row's Color; opens the color picker. |
| — | |
| Reset view | Same as Reset on the temporary bar; shown only in temporary mode. |

Items follow the tier rules (§9): an item that goes through T3 carries the
orange dot, an item that cannot run on the target (Halftone on link rows below
the model row, Section box in a 2D view) shows disabled with the reason as its
tooltip. Every item also has a shortcut shown in the menu (Z, B, I, H).

An action or row that goes through T3 carries a small orange dot. A control
that cannot run shows disabled with a one-line tooltip giving the reason.

### 3. Documents are one list

- The current document and each loaded link instance are equal entries in the
  documents `CheckComboBox`. The current document is labelled **Current
  model** everywhere in the panel; links show their file name. One entry per
  link instance; duplicate file names get an instance suffix. Entries are `DocumentKey`s (§11), not `Document`
  objects.
- The box shows one line: ticked document names joined by commas, cut with an
  ellipsis, full list in the tooltip. `DevTools.UI/Controls/CheckComboBox`
  carries a private fork of HandyControl's `CheckComboBox` with a one-line
  summary instead of tags, an "All" item that stays in sync without a tag panel,
  and "All" that skips disabled entries (§12 fork rule).
- Default: the current document only. Links are opt-in; newly loaded links
  start unticked. At least one entry stays ticked. Unloaded links show
  disabled and "All" skips them.
- The UI never splits the panel into host and link areas. Where data comes from shows only in
  counts and tooltips.
- With Scope = Selection the selection decides the documents: the documents
  list is hidden and the tree shows selected elements from every document. The
  ticked list returns with Active view or Whole model.
- The ticked list is kept in memory per document session, not in settings.

### 4. Tree

- Levels: **Model › Category › Family › Type › Instance**, one icon per level.
  Only ticked documents appear, *Current model* first, then links.
- An **All models** group sits at the top of the tree. It lists each category
  found in any ticked document, merged, with the total count, and has no level
  below Category. It exists for visibility (§7); selecting one of its rows
  selects that category in every ticked document.

```
▾ All models                 category everywhere (T2)
    Ducts          3,156  👁
▾ Current model              this model only (T0)
  ▸ Ducts            0    👁
▾ MEP-Mech.rvt  👁 ◐ ■ ◎    whole link (T1); rows below: this link only (T3)
  ▸ Ducts        3,156  • 👁
```

- Every row shows its branch count in grey, open or closed. Under Scope =
  Selection the counts are of the selection. A blue badge shows how many
  selected elements a closed row holds; open rows have no badge.
- Whole model counts the main model and primary design options. Active view
  counts what the view collector returns.
- Counts come from the document snapshot (§11). When a snapshot is older than
  the last model change it is rebuilding, the status line shows *Updating…*.
- Rows: 28 px, stock HandyControl selection look (solid primary, white text).
- The Instance level loads on expand and pages ("200 of 1,402 · more").
- **Status line** at the bottom-left: *N selected* and **Clear**. When part of
  the selection is in documents that are not ticked, it adds **Show in tree**,
  which ticks those documents and reveals the rows.
- Search matches only the text shown on rows: model, category, family, type
  and instance names, and Element Id (`TokenParser`, so `linkInstanceId@id`
  works). Parameter values are the Filter tab's job.
- The control is a flat virtualized list (§12), not WPF `TreeView`.

### 5. Filter tab

One mode: categories plus rules.

- **Source combo** lists custom filters, then each ticked document's Revit
  filters grouped under the model name: rule-based (funnel icon) and selection
  filters (cursor icon). Filters applied to the view show "in view": Current
  model filters from the active view, link filters from the link's `LinkedViewId`
  when the link displays By Linked View (2024+), or Custom with linked view
  filters (2025+). Next to it:
  - **+** adds a custom filter named with the next free `New filter N`, name in
    edit mode.
  - **Delete** removes the selected custom filter; disabled for Revit filters.
  - **Save** is enabled only while the custom filter has unsaved changes (a dot
    after its name).
  - Names are trimmed, never empty, and unique among custom filters ignoring
    case. An invalid name shows a red box and a one-line reason and blocks Save
    and Enter; Esc or leaving the box restores the last valid name. Double-click
    or F2 renames.
  - Switching away from a filter with unsaved changes asks inline
    (Save · Discard), not in a dialog.
- **Categories:** checkbox list with counts and search, folds to a summary
  line. Checked categories are always "any of".
- **Rules:** a set matches **All (AND)** or **Any (OR)**; sets nest (+ Set).
  Each rule is parameter, operator, value, on two lines. Operators follow
  Revit's names and the parameter's storage type (equals … does not end with,
  has a value / has no value) plus *parameter exists* / *parameter missing*.
  Numeric input is parsed with `UnitFormatUtils.TryParse` against the units of
  the document it runs in; ElementId values (Level, Phase) pick by name. The
  value dropdown lists real values in the checked documents with counts and a
  `[No parameter]` row (value index, §11). **+ Rule** and **+ Set** are small
  icon buttons.
- **Revit filters** open read only. A rule-based filter runs in its own
  document (`GetElementFilter()` AND `ElementMulticategoryFilter(GetCategories())`);
  its rules display read only through a reader that turns
  `ElementLogicalFilter` / `ElementParameterFilter` / `FilterRule` trees into
  rule rows, with an *unsupported rule* row for rules it cannot show (global
  parameter association). A selection filter gives `GetElementIds()`; missing
  ids are skipped; in a link they become link references. A Current model
  filter is never run against a link document. *Duplicate* copies a Revit filter into a
  custom filter.
- **Result row:** match count, selection mode **New / Add / Remove**,
  **Live**, and the **⋯** button that opens the context menu (§2) for the
  result.
- **Live** is on by default. Each change of categories or rules updates the
  Revit selection after a ~300 ms debounce. The selection when Live starts is
  the baseline for Add and Remove, so edits never accumulate. Live pauses above
  about 5,000 elements; *Select in Revit* then shows in the menu.

### 6. Selection sync

- **Tree: Sync** (two ways), **Filter: Live** (result to Revit only). Switch
  states live in memory per session.
- **Sync on:** selecting tree rows selects in Revit (a Type or Category row
  selects all its instances); selecting in Revit opens and highlights rows. Up
  to about 50 selected elements the tree opens each path and scrolls to the
  first; above that rows stay closed with badges and one hint line (*Too many
  to expand · Expand all*). Above about 5,000, sync pauses for that change and
  *Select in Revit* shows in the menu.
- Sync is shown only with Scope = Active view or Whole model. With Scope =
  Selection the switch is hidden and *Select in Revit* shows in the menu.
- Revit to tree: `SelectionChanged` gives ids and link references; rows are
  found through the `(DocumentKey, ElementId)` index of the snapshot. The
  handler only records the event; all work runs later on the panel's executor,
  never inside the handler.
- Both directions are debounced (about 150–300 ms). The panel records the set
  of references it sent and drops the next `SelectionChanged` whose set
  matches it. The same guard covers Live and the T3 queue.

### 7. Visibility on tree rows

There is no separate Layers tab: visibility lives on tree rows. Controls are
icon buttons with tooltips and no column header: **Show** (the eye) is always
visible; **Isolate**, **Halftone** and **Color** appear on the hovered or
selected row. They always work on the active view, whatever the Scope, and on
the branch's elements that are in that view. A row acts on exactly the group
it sits in, and never on anything else:

- **All models** category rows act on the category itself
  (`HideCategoriesTemporary`, `IsolateCategoriesTemporary`,
  `SetCategoryOverrides`): Revit applies it to the current model and every link
  that shares the view's visibility (By Host View). The tooltip says so and, on
  2024+, names those links.
- **Current model** rows, at any level, act on that branch's elements by
  element id (`HideElementsTemporary`, `IsolateElementsTemporary`,
  `SetElementOverrides`). Links keep their state, so a user can hide Ducts in
  the current model and keep the linked Ducts. Elements drawn after the click
  are not covered until the eye is clicked again; Keep stores element hides,
  not a category hide.
- A **link's** model row carries all four controls for the whole link instance
  (T1). Rows below it offer Show and Isolate only (T3); Halftone and Color for
  a link are on its model row.
- The *All models* and *Current model* group rows are headers only, with no
  controls.
- **Isolate** shows only that row's target and hides everything else
  temporarily in the active view, the same word and meaning as the Isolate
  action; Reset brings everything back.
- The eye has three states: shown, hidden, and partly hidden (some elements of
  the branch are hidden). It is read back from the view for Current model
  rows and from the journal for link rows.
- Rows the panel hid stay in the tree, dimmed with the eye off, even when the
  Active view collector no longer returns them, so they can always be shown
  again. Elements hidden outside the panel are found with Scope = Whole model.
- When an *All models* row is hidden, rows of that category in the other groups
  show the eye as hidden by *All models* and are disabled until it is shown
  again.
- Hovering an eye highlights every row the click will change.

### 8. Visibility: temporary first

Temporary mode is chosen for a shared model: it is not saved with the view's
permanent state, works when a view template locks Visibility/Graphics, and one
call resets it.

- Hide, isolate and show use `HideElementsTemporary`,
  `IsolateElementsTemporary`, `HideCategoriesTemporary`,
  `IsolateCategoriesTemporary`, and for linked elements the temporary UI
  commands (T3).
- Halftone and Color run inside Temporary View Properties
  (`EnableTemporaryViewPropertiesMode` with the view's own id when it has no
  template, after `CanEnableTemporaryViewPropertiesMode`). Changes made there
  are dropped on exit, so Keep does not cover them.
- **N changes** is counted by the panel; Revit cannot report it.
- **Keep** converts hide/isolate to permanent
  (`ConvertTemporaryHideIsolateToPermanent`). When T3 changes are pending, Keep
  asks inline: *N hidden link elements become permanent. Undo them later with
  Reveal Hidden Elements.* (Keep · Cancel).
- **Reset** disables both `TemporaryHideIsolate` and `TemporaryViewProperties`
  and clears the T3 journal. Revit also drops temporary state when the model
  closes.
- **The eye** is one toggle on the clicked row, or on every selected row when
  the clicked row is selected. Revit has no per-element temporary unhide, so
  turning an eye back on resets temporary mode and re-applies the rest in one
  request.

### 9. Tiers, resolved per request

| Tier | Applies when | Mechanism |
|------|--------------|-----------|
| T0 Current model | Its elements, or a Current model category row | API, by element id |
| T1 Link instance | Target is a whole link | API on the `RevitLinkInstance` id |
| T2 Category everywhere | An *All models* row | `HideCategoriesTemporary` / `IsolateCategoriesTemporary` / `SetCategoryOverrides` |
| T3 Inside one link | One link's category, a rule result, instances | Evaluate in the link document, `SetReferences`, `PostCommand` |

A result spanning documents splits: current-document matches go T0, link
matches go T3. T2 never switches a link's display mode. Select, Box and Zoom
on linked elements always use public API (Zoom through
`UIView.ZoomAndCenterRectangle` on the link box transformed by
`GetTotalTransform`). Nested links are out of scope for T3.

**If the go/no-go spike fails** (the temporary commands cannot be posted on
link references), T3 visibility actions are removed: link rows keep Select,
Box and Zoom, and their eyes and Isolate are disabled with the reason.

### 10. PostCommand queue

- Single flight. `CanPostCommand` is checked first. Command ids resolve in one
  place.
- The user's selection is snapshotted, replaced by link references, and
  restored after the command runs.
- Multi-step requests advance on `Idling` as a state machine; a request that
  does not finish within a few idles is dropped and the journal marked stale.

### 11. Architecture

The panel is three layers. The core and the views never reference a host API,
so they build, run and test out of process; a host plugs in through ports. The
structure fits any host with a current model, attached models (Revit links,
AutoCAD xrefs) and grouped elements.

```
DevTools.ModelsContext               core, no host API, no WPF
   Model/       DocumentKey, ElementRef, ElementRow, TreeRow, DocumentSnapshot,
                TreeSchema, FilterDefinition, VisibilityRequest, VisibilityPlan
   Engine/      SnapshotAggregator, DeltaApplier, ScopeCounter, ValueIndexCache,
                VisibilityPlanner, SelectionGuard, TemporaryJournal, Debouncer
   Ports/       IModelSource, IModelEvents, ISelectionPort, IViewActions,
                IFilterEvaluator, IHostFilterCatalog, ICustomFilterStore, IClock
   ViewModels/  ModelContextViewModel, ContextBar, TreeTab, FilterTab,
                TemporaryBar, ContextMenu
DevTools.ModelsContext.Views         XAML only (DevTools.UI); every control has an AutomationId
DevTools.UI/Controls                 FlatTreeList, summary CheckComboBox template, icons
RevitDevTool.Tools/ModelsContext     Revit adapter, implements the ports
   RevitModelSource, RevitModelEvents (event hub), RevitSelectionPort,
   RevitViewActions (T0–T2), PostCommandQueue (T3), RevitFilterEvaluator,
   RevitFilterCatalog, DocumentRegistry, ModelsContextPane
```

**Port rules.**

- Only plain data crosses a port: `DocumentKey`, `ElementRef(DocumentKey,
  long)`, structs, strings. No host object, no host thread affinity.
- The core decides, the adapter executes. `VisibilityPlanner` turns a request
  and each document's declared capabilities (hide by id, hide a group
  everywhere, needs a reference command) into a `VisibilityPlan`; the Revit
  adapter names its steps T0–T3 (§9) and runs them. Group rules (§7) and tier
  splits are therefore tested without Revit.
- `TreeSchema` comes from the adapter: Revit gives Category › Family › Type ›
  Instance; another host gives its own levels.
- `FilterDefinition` is host-neutral (categories, sets, rules, operators by
  value kind). The adapter compiles it (Revit: `ElementFilter`, below); a fake
  evaluates it over fake parameter data.
- Time and threads enter through `IClock` and a scheduler port, so debounce,
  Live and the self-caused-selection guard run under a fake clock.

**Out of process.** A fake model (generated: about 100k elements and three
links, or replayed from a snapshot JSON exported from a real model through MCP
`execute_csharp_code`) implements every port. It drives the core tests, the
FlaUI view tests and a standalone sandbox app that hosts the panel, so layout,
flows and the §12 targets are checked before Revit is involved.

The rest of this section describes the Revit adapter.

**Identity.**

- `DocumentKey` = Current model document identity + link instance id (`long`,
  invalid for the Current model itself). UI objects hold keys, never `Document`, `Element`,
  `RevitLinkInstance` or `Reference`. `DocumentRegistry` resolves a key on each
  call and returns nothing for a closed or unloaded document.
- Several instances of one link share one `Document`, so snapshots are keyed
  by the link `Document` and shared; references, transforms and the journal
  are keyed by `DocumentKey`.
- Sync and selection use `(DocumentKey, ElementId)`. `UniqueId` is read only
  when the T3 journal records an action.

**Threading.**

- Revit API calls run only on the Revit thread through
  `RevitContextExecutor.RaiseAsync` with a `CancellationToken`. Collection
  runs in slices of about 2–5k elements or 30 ms per call, awaited one after
  another from the WPF side; a slice never queues the next one.
- Grouping, counting and index building run on `Task.Run` over plain records.
- Nothing from the Revit API crosses threads: only `long`, `int`, `string` and
  plain structs.
- One cancellation source per (scope, ticked documents); any change cancels it.
- Debounce lives in the core (`Debouncer` on `IClock` and `IScheduler`); the
  WPF side implements `IScheduler` with `DispatcherTimer` stop/start, the
  pattern already in the repo.

**Document snapshot** (plain data, no Revit objects):

- `ElementRow` struct per element: id, type index, category index, level id,
  workset id (about 32 bytes).
- `TypeRow` table: type id, type name, family name, category; a name table so
  each string is stored once.
- `Dictionary<long, int>` id → row; aggregate nodes Category › Family › Type
  with counts.
- Built in one pass over `WhereElementIsNotElementType()` with a model
  category filter. Instance names page in on expand. Parameter values are not
  in the snapshot.
- Estimated 8–10 MB per 100k elements, so about 30–40 MB for the Current model and three
  links (to be measured).

**Scopes.** Whole model reads the snapshot aggregates. Active view and
Selection collect an id set (view collector, or the link view collector for
links) and recount it against the snapshot rows in managed code. The id set is
dropped on `ViewActivated` and on any `DocumentChanged`.

**Invalidation.**

- Current model `DocumentChanged`: deleted ids decrement their node chain, added ids
  are read and counted, modified ids (model categories) are re-read and moved
  if type, category or level changed, modified types or families patch
  `TypeRow`. More than about 2,000 changed ids marks the snapshot dirty and it
  rebuilds on next use. While the pane is hidden, changes only mark dirty.
- Undo and Redo go through the same handler; if the spike shows their ids are
  unreliable, they mark dirty instead. They always mark the T3 journal stale.
- Link snapshots are stamped with the link document's
  `Document.GetDocumentVersion` (`VersionGUID`, `NumberOfSaves`), present in
  every supported version. A version changes when the file is saved or synced,
  not per transaction. On reload (`RevitLinkType` modified, link instance added,
  `IsValidObject` false, or `GetLinkDocument()` another object):
  - same `VersionGUID` → keep the snapshot as is, no collection;
  - different → `linkDoc.GetChangedElements(oldVersionGuid)` gives created,
    modified and deleted ids, applied through the same delta path as the
    Current model;
  - the old version is unknown to the new file, or the delta is larger than
    about 20% of the snapshot → rebuild in slices.
- The Current model follows `DocumentChanged` live. After Reload Latest or a
  sync, `GetChangedElements(lastSeenVersion)` is used as a check that the
  delta path missed nothing; on mismatch the snapshot rebuilds.
- A link source shared by several open Current models keeps one snapshot with
  a reference count; it drops when the last user closes or the link unloads.
- `ValueIndexCache`: built lazily per (snapshot, parameter, category set),
  quick filters first, in slices, cancelled when the rule changes. At most 500
  distinct values ("more…"), at most 8 indexes (least recently used dropped).
  Current model indexes drop on `DocumentChanged`.
- Parameter catalog per snapshot: name → BuiltInParameter, shared GUID or
  `ParameterElement` id, storage type and spec. Rebuilt when a
  `ParameterElement` is added or deleted. Caches are never static and never
  shared across documents.

**Filter compiler.** `RevitFilterEvaluator` compiles a `FilterDefinition` per document to one
`ElementFilter`: `ElementMulticategoryFilter` and `ElementIsElementTypeFilter`
first, then `ElementParameterFilter` from `ParameterFilterRuleFactory` by
storage type ("exists" = has value OR has no value). Parameter keys resolve in
each document separately.

**Events.** `RevitModelEvents` subscribes once to `SelectionChanged`,
`DocumentChanged`, `ViewActivated`, `DocumentClosing` and, only while the T3
queue is busy, `Idling`. Panel-level parts listen while the pane is visible;
tab-level handlers listen only while their tab is open, and a tab that comes
back resyncs once. A hidden pane pauses everything.

**Lifecycle.** Each open Current model has its own session (ticked documents,
expansion, selected filter, Sync/Live, journal pages per view). Every
asynchronous job carries the session and a generation number; a result whose
session closed or whose generation moved on is dropped on the UI thread, never
applied. Every port call resolves its `DocumentKey` through `DocumentRegistry`
first and does nothing for a closed, unloaded or replaced document.

| Event | Panel does |
|-------|------------|
| View activated, same model | Cancel scope jobs, drop view id sets, read the new view's temporary state; tree expansion kept; Active view counts recount from the snapshot. |
| Active model switched | Cancel the old session's jobs, park its session in memory, show the new model's session (built on first show). No call touches the old model afterwards. |
| `DocumentClosing` | Cancel that session's jobs, drop its session, journal and snapshots (shared link snapshots by reference count), release every key. If the close is cancelled, the session rebuilds on next show. |
| Link unloaded | Its rows disable with the reason; its snapshot is kept until reload decides by version. |
| Link reloaded | Version rule above. |
| Undo / Redo | Delta path; journal stale. |
| Revit busy (modal command, edit or sketch mode, no active document) | Actions disable with "Revit is busy"; nothing is queued for later. |
| Family document | Works as a Current model without links. |
| Pane hidden | Pause; on show, resync once. |

Every adapter call is wrapped: a failure becomes a one-line status in the
panel and never throws into Revit.

### 12. UI controls and performance

- **Flat tree list** (`DevTools.UI`): a `ListBox` with `SelectionMode=Extended`,
  `VirtualizationMode=Recycling`, `ScrollUnit=Item`, fixed 28 px rows, in a `*`
  grid row (never inside a `StackPanel`, an Auto row or an outer
  `ScrollViewer`). The HandyControl `ListBoxItem` margin and minimum height are
  overridden.
- Rows are `TreeRow` objects (level, kind, text, count, selected-inside,
  expanded, parent, lazy children, `DocumentKey`, id). Expand inserts a range,
  collapse removes the run of deeper rows; both raise one `Reset` through a
  small range-capable collection. Selection lives in a `HashSet<TreeRow>` on the
  view model.
- Row template: no converters; indent, count text and icon are precomputed or
  picked by `DataTrigger` from frozen `StaticResource` geometries. Brushes stay
  `DynamicResource` for theme switching. A count refresh swaps the visible list
  once instead of raising per-row `PropertyChanged`.
- Value dropdowns use a `VirtualizingStackPanel` with recycling as
  `ItemsPanel` and a fixed `MaxDropDownHeight`; values load on open.
- **Fork rule.** A HandyControl control that needs more than a Style is copied
  into `DevTools.UI/Controls/<Name>/` under the `DevTools.UI.Controls`
  namespace and changed there. `libs/HandyControl` (a submodule) is never
  edited.
- **Scale.** The panel must stay responsive at millions of elements across
  several models. Rules that follow from it:
  - No object per element: snapshots are arrays of structs and name tables;
    objects exist only for visible rows (bounded by expansion and paging).
  - First paint does not wait for the snapshot: Category rows and counts come
    first from one quick count per category
    (`FilteredElementCollector.OfCategoryId(...).GetElementCount()`), then
    Family and Type levels fill in as slices finish.
  - Deltas update counts in place; a full rebuild happens only when the
    version rule or the delta size says so.
  - Active view id sets are cached per (view, model) and dropped by
    `DocumentChanged`, not recollected on every expand.
  - All collection runs in cancellable slices; aggregation runs off the UI
    thread on plain data.
- Targets, Release, Revit 2024 and 2026:
  - 100k elements and three links: first Category rows < 300 ms, expand a
    200-instance page < 50 ms, Live change to Revit selection < 200 ms after
    the debounce, about 40 realized row containers.
  - 1M elements across five models: first Category rows < 1 s, full counts
    without blocking the UI for more than 50 ms at a time, snapshot memory
    < 64 MB per million elements.
  - No `Document` retained after a model closes ten times.

### 13. State and storage

| State | Source |
|-------|--------|
| Temporary hide/isolate, current document | Read back from the view |
| Category visibility, overrides, link instance | Read back from the view |
| Temporary changes count | Panel, per (DocumentKey, view) |
| Section box | `View3D.GetSectionBox` |
| T3 changes to linked elements | `TemporaryJournal`, in memory |
| Document list, Tree Sync, Filter Live, selection mode | In memory per session |
| Custom filters | Settings (`DevTools.Settings`) |

- The journal stores `(linkInstance id, element UniqueId, action)`. It clears
  with Reset, on `DocumentClosing` and on link reload; Undo/Redo marks it stale.
  Link rows it does not know show the eye as unknown.
- A custom filter stores categories, sets and rules. A parameter is stored by
  `BuiltInParameter` name, shared parameter GUID, or name, in that order.
  Element ids are never stored.

### 14. Deferred

- **Context strip** (expand selection by System, Connected, Type, Family,
  Level, Workset, Host, Room, with keys and history). Not in the first release:
  the meaning of each expansion across documents is not settled. It returns as
  its own ADR amendment.
- **Context popup** at the cursor (a hotkey that opens the panel's actions next
  to the pointer). Not in the first release.

### 15. Ownership

- Core (model, engine, ports, view models): `source/DevTools.ModelsContext/`,
  shared layer, no host API, same target frameworks as `DevTools.UI`.
- Views: `source/DevTools.ModelsContext.Views/`, XAML on `DevTools.UI`.
- Revit adapter, pane and T3 queue: `RevitDevTool.Tools/ModelsContext/`
  (0036). Link references reuse `Selection/ElementSelector`; token search
  reuses `Selection/ElementSearcher`, which groups by link instance, not by
  link document.
- Tests: `tests/DevTools.ModelsContext.Tests` (core and view models on fakes),
  `tests/DevTools.ModelsContext.Views.Tests` (FlaUI, AutomationIds),
  `tests/DevTools.ModelsContext.Tests.Shared` (fake model),
  `tests/DevTools.ModelsContext.Sandbox` (standalone app).
- Dockable pane registration: `RevitDevTool.Core` `DockablePaneProvider`
  (0007). Revit calls go through `RevitDevTool.Core` `RevitContextExecutor`.
- Shared controls (flat tree list, row template, forked `CheckComboBox`, icon
  geometries): `DevTools.UI/Controls`. `libs/HandyControl` is not edited.

## Alternatives Considered

1. **Public API only (T0–T2).** Cannot hide one link's categories or act on
   link-only data. Kept as the default wherever it is exact, and as the
   fallback if the T3 spike fails.
2. **PostCommand for everything.** No read-back, fragile timing, untestable.
3. **View filters to apply rule results.** Each is a new project element.
   Rejected by §1.
4. **Extensible Storage for view state or the T3 journal.** Writes model data.
5. **Separate Explore mode** (category → parameter → values browser). Folded
   into the rule value dropdown.
6. **Revit filters only, no custom rules.** Links are read only and new Revit
   filters would be new elements.
7. **Find & Replace in the Filter tab.** Replace writes element data and cannot
   run in links.
8. **Selection mode Intersect.** Overlaps Scope = Selection.
9. **Host versus link UI.** Replaced by one documents list.
10. **Inline "Save as custom filter" box.** Replaced by + / Delete / Save.
11. **One global sync switch for Tree and Filter.** Tree syncs both ways and
    Filter one way; each tab keeps its own.
12. **Snapshot the selection for Scope = Selection.** Hiding Sync is simpler.
13. **A fixed actions bar at the bottom** (Select, Isolate, Hide, Box, Zoom).
    Took a row of space on every tab, sat away from the rows it acted on, and
    repeated the row controls. Replaced by the context menu (§2).
14. **Selection summary in the context strip, per-document counts.** Replaced
    by the tree status line.
15. **WPF `TreeView`.** Hierarchical virtualization costs a panel per open
    node, scrolling to an unrealized path walks containers level by level, and
    multi-select is not built in.
16. **Cache `Element` objects or rebuild on demand** (RevitDBExplorer's
    approach). Pins documents, breaks on link reload, and rebuilds the whole
    tree on every refresh.
17. **`UniqueId` as the sync key.** Revit reports ids and references; storing
    UniqueIds costs about 12–15 MB per 100k elements for no gain.
18. **Revit 2024 as the minimum.** 2023 lacks only `RevitLinkGraphicsSettings`,
    which feeds two hints. Not worth dropping 2023 users.
19. **One category row per document with a hidden side effect** (a current
    model row that also hides the category in By Host View links). Users could
    not hide a category in the current model only. Replaced by *All models*
    plus element-based Current model rows.
20. **A separate Layers tab** (per-model category list with visibility
    controls). It repeated the tree's Model › Category levels; its only own
    parts, the *All models* group and always-listed hidden rows, moved into the
    tree.
21. **Cysharp ObservableCollections for tree rows.** Thread-safe collections,
    filtered views and a WPF dispatcher bridge, MIT. Not adopted for the
    first release: the visible row list is small (expansion and paging bound
    it), its Slim WPF bridge rejects range changes, and the cost at scale sits
    in the snapshot and in Revit collection, which a collection library does
    not change. Revisit if search or sorting over large expanded lists needs
    filtered views.
22. **Switch links to By Linked View to keep them visible.** Changes link
    overrides behind the user's back.
23. **Permanent `HideElements` for T3.** No API unhide; kept only as what Keep
    produces after an explicit confirmation.

## Consequences

Positive:

- One way to reach elements in any document; links need no special UI.
- Normal use leaves no trace in the saved model except what the user chose to
  Keep.
- Selecting by an existing Revit filter, including filters inside links.
- Settings stay small: named custom filters only.
- Caches hold plain data, so closed models and reloaded links are never
  pinned.

Tradeoffs:

- T3 relies on PostCommand and may be cut by the spike.
- Halftone and Color cannot target single elements or categories inside a
  link; they apply to the whole link.
- Turning one eye back on in temporary mode resets and re-applies, which may
  flicker.
- Revit 2022 users do not get the panel. On 2023 two link hints are generic.
- Counts can lag a large model change by one rebuild.
- Box is limited to existing 3D views.

## Follow-Up

- Spike on a live host with links, Revit 2023 and 2026. Step 1 decides T3.
  1. **Go/no-go:** `LookupCommandId` and `CanPostCommand` for Temporary
     Hide/Isolate Element and Category with link references selected; post
     each and record what it does to linked elements.
  2. `SetReferences` with references to elements without graphics in the view
     (MEP systems, unplaced rooms).
  3. `HideCategoriesTemporary` / `IsolateCategoriesTemporary` with links in By
     Host View, By Linked View and Custom.
  4. Temporary View Properties: overrides dropped on exit, Current model and link
     instance.
  5. Transactions and undo: whether temporary hide/isolate, Reset and Keep need
     a transaction and how many undo entries each eye click adds; Keep on a view
     whose template controls V/G; worksharing ownership of the view.
  6. Link view collector as read-back: does it honour section box, phase, V/G
     and temporary hide? If it does, compare it against the link document to
     read hidden linked elements and replace the journal. Time it at 10k and
     50k elements. Also check whether `FilteredElementCollector(doc, viewId)`
     returns elements hidden temporarily or permanently, and whether
     `HideElementsTemporary` accepts ids that are not in the view (§7).
  7. Timing of post to `Idling` at about 1k references; `SetReferences` plus
     Temporary Hide at about 10k and 50k; `HideElementsTemporary` for a
     Current model category of about 50k elements.
  8. Undo and Redo visible in `DocumentChanged` with usable ids.
  9. Flicker of reset plus re-apply.
  10. Document version: `GetDocumentVersion` before and after a link reload
      with and without a save of the link file; `GetChangedElements(oldGuid)`
      on a reloaded link (ids returned, cost at 100k and 1M, exception when the
      old version is unknown); same after Reload Latest on a workshared
      Current model; cloud models.
  11. Quick per-category `GetElementCount()` time on a 1M-element model.
- Benchmark the flat tree list against the §12 targets on a model of about
  100k elements with two to three links, and on a generated 1M-element model
  across five documents in the sandbox.
- After the spike, set Accepted or amend, then add
  `docs/product/model-context.md` and `docs/architecture/ModelsContext/`.
- Execution plan: [2026-11-13-models-context-panel](../plans/active/2026-11-13-models-context-panel.md).

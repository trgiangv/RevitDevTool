# 0062 Model Context Panel

Date: 2026-11-13

## Status

**Proposed.** The behavior below is agreed. Tier 3 (PostCommand) and the
items marked *spike* wait on the live-host spike in Follow-Up. Everything else
uses public API only.

## Context

`RevitDevTool.Tools` gets a dockable **Model Context** panel for daily model
work: browse, filter, expand a selection, and show, hide, isolate, halftone,
color, box or zoom the result in the active view. Elements in loaded RVT links
must work the same way as elements in the current document.

Design canvas: *Model Context Panel — UX directions* (page "Panel + user
flows", flows 1–7). Design intent page: *Model Context Panel*.

Revit limits what a host view can do to elements inside a link:

1. `HideElements`, `HideElementsTemporary`, `IsolateElementsTemporary` and
   `SetElementOverrides` take host `ElementId`s. A linked element has none.
   These calls reach a link only as a whole `RevitLinkInstance`.
2. Host category visibility, permanent or temporary, reaches link elements only
   when the link displays By Host View, and then applies to the host and every
   link at once.
3. A view filter resolves parameters in the host document, so link-only data
   cannot drive it. A view filter is also a new element in the project.
4. `SetLinkOverrides` / `RevitLinkGraphicsSettings` set the link display mode
   and linked view. The per-category settings of Custom mode are, as far as we
   know, not in the API (*spike*).
5. The UI can Tab-select a linked element and hide, isolate or override it.
   API code reaches those commands only through `Selection.SetReferences`
   with `Reference.CreateLinkReference`, then `UIApplication.PostCommand`.
6. `FilteredElementCollector(hostDoc, viewId, linkInstanceId)` returns the
   linked elements visible in a host view. It is present in the Revit 2023 API
   although the 2023 documentation does not list it.
7. Temporary state is readable for host ids
   (`IsElementVisibleInTemporaryViewMode`). Nothing reports which linked
   elements a view hides.
8. Revit can hide or override with a filter, but cannot select by one.

## Decision

### 1. Scope of the product

- Revit 2023 and later. The panel is built only under `REVIT2023_OR_GREATER`;
  the Revit 2022 add-in does not register it.
- The panel never writes element data and never creates an element: no
  parameters, no Extensible Storage, no view filters, no views. It changes only
  the active view and existing link instances.
- Replace (writing parameter values) is not part of the panel. It can become a
  separate tool that takes the panel's result as input, works on the current
  document only, and writes in one undoable transaction.

### 2. Layout

Top to bottom, in one dockable pane (HandyControl, light and dark themes):

1. **Pane title** with the active view name.
2. **Context bar.** Scope as three icon buttons in a `ButtonGroup`
   (Selection, Active view, Whole model) and one HandyControl `CheckComboBox`
   of documents. Every tab and the context strip read this bar.
3. **Context strip**, only while something is selected: eight expand buttons
   with icons (System, Connected, Type, Family, Level, Workset, Host, Room).
   Shortcut keys S C T F L W H R are in the tooltips. A short history allows
   going back. The strip does not repeat a selection summary; the count lives
   in the tree status line.
4. **Tabs:** Tree, Filter, Layers.
5. **Temporary bar**, only while the view is in a temporary mode:
   *Temporary · N changes* with **Keep** and **Reset**.
6. **Actions bar:** Select, Isolate, Hide, Box, Zoom on the current selection
   or filter result. **Select** is one way (panel to Revit) and is hidden while
   the open tab syncs on its own (Tree with Sync on, Filter with Live on).

### 3. Documents are one list

- The current document and each loaded link instance are equal entries in the
  documents `CheckComboBox` (Element Finder `DocumentItem` model, 0036). One
  entry per link instance; duplicate file names get an instance suffix.
- The box shows one line: ticked document names joined by commas, cut with an
  ellipsis, full list in the tooltip. Stock HandyControl renders tags in a
  `UniformSpacingPanel` whose `ChildWrapping="Wrap"` is fixed in the template
  (no property), so the box would grow in height. `DevTools.UI` carries a copy
  of the `CheckComboBox` template without `PART_Panel` and with a summary
  `TextBlock`; the control already skips tag handling when the panel is
  missing. Popup, "All" item and item style stay stock.
- Default: the current document only. Links are opt-in. Newly loaded links
  start unticked. At least one entry stays ticked. Unloaded links show
  disabled.
- The UI never labels host versus link (no "link only" tags, no separate link
  switch). Where data comes from shows only in counts and tooltips.
- With Scope = Selection the selection decides the documents: the documents
  list is hidden and the tree shows selected elements from every document.
  The ticked list is kept and returns with Active view or Whole model.
- The ticked list is kept in memory per document session, not in settings.

### 4. Tree

- Levels: **Model › Category › Family › Type › Instance**, one icon per level.
  Only ticked documents appear.
- Counts above Instance come from one aggregation pass per document and scope,
  cached. Host caches update from `DocumentChanged`; link caches live until the
  link reloads.
- Every row shows the element count of its branch in grey, open or closed.
  Under Scope = Selection the counts are of the selection. A blue badge shows
  how many selected elements a closed row holds; open rows have no badge
  because their children show the selection.
- Rows use the stock HandyControl `TreeViewItem` look: 28 px rows, selected
  row in solid primary with white text.
- The Instance level loads on expand and pages ("200 of 1,402 · more").
- **Status line** at the bottom-left of the tree: *N selected* and **Clear**.
  When part of the selection is in documents that are not ticked, it adds
  **Show in tree**, which ticks those documents and reveals the rows. No
  per-document counts: model names are too long for the pane.
- Search matches only the text shown on rows: model, category, family, type
  and instance names, and Element Id (`TokenParser`, so `linkInstanceId@id`
  works). Parameter values are not searched; that is the Filter tab, or a
  separate Search & Replace tool.
- The control is a virtualized flat list (open question, see Follow-Up), not
  WPF `TreeView`, to get multi-select and stable scrolling.

### 5. Filter tab

One mode. No Explore mode and no Explore/Rules switch.

- **Source combo** at the top lists custom filters, then each ticked
  document's Revit filters grouped under the model name: rule-based filters
  (funnel icon) and selection filters (cursor icon); filters applied to the
  view show "in view". Next to it:
  - **+** adds a custom filter named with the next free `New filter N`, name in
    edit mode.
  - **Delete** removes the selected custom filter; disabled for Revit filters.
  - **Save** is enabled only while the custom filter has unsaved changes
    (a dot after its name).
  - Names are trimmed, never empty, and unique among custom filters ignoring
    case. An invalid name shows a red box and a one-line message and blocks
    Save and Enter; Esc or leaving the box restores the last valid name.
    Double-click or F2 renames.
  - Switching away from a filter with unsaved changes asks inline
    (Save · Discard), not in a dialog.
- **Categories:** checkbox list with counts and search, folds to a summary
  line. Checked categories are always "any of".
- **Rules:** a set that matches **All (AND)** or **Any (OR)**; sets nest
  (+ Set). Each rule is parameter, operator, value, on two lines. Operators
  follow Revit's names and the parameter's storage type (equals … does not end
  with, has a value / has no value) plus *parameter exists* / *parameter
  missing*. Lengths use project units; ElementId values (Level, Phase) pick by
  name. The value dropdown lists the real values in the checked documents with
  counts and a `[No parameter]` row. **+ Rule** and **+ Set** are small icon
  buttons (`ButtonDefault.Small`). No copy/paste of rules: Duplicate covers
  reuse inside the panel.
- **Revit filters** open read only. A rule-based filter runs in its own
  document (`GetCategories()` + `GetElementFilter()`); its rules display read
  only. A selection filter gives `GetElementIds()`; missing ids are skipped; in
  a link they become link references. A host filter is never run against a
  link document. *Duplicate* copies a Revit filter into a custom filter.
- **Result row:** match count (no per-document split: model names are too
  long for the pane), selection mode **New / Add / Remove** (narrowing an existing selection uses Scope =
  Selection), and **Live**.
- **Live** is the Filter tab's own switch, on by default. Each change of categories or rules updates the
  Revit selection after a ~300 ms debounce. The selection at the moment Live
  starts is the baseline for Add and Remove, so edits never accumulate. Live
  pauses above a size limit (about 5,000) and offers Select instead. The
  panel ignores `SelectionChanged` events it caused.

### 6. Selection sync

- Each tab owns its switch. **Tree: Sync** (two ways). **Filter: Live** (result
  to Revit only). Layers has none. Switch states live in memory per session.
- **Sync on:** selecting tree rows selects in Revit (a Type or Category row
  selects all its instances); selecting in Revit opens and highlights rows.
  Up to about 50 selected elements the tree opens each path and scrolls to the
  first; above that rows stay closed and show badges, with one hint line
  (*Too many to expand · Expand all*). Above the size limit (about 5,000) sync
  pauses for that change and Select comes back.
- **Sync is shown only with Scope = Active view or Whole model.** With Scope =
  Selection the tree is built from the selection, so syncing back would shrink
  it on every click: the switch is hidden and Select shows.
- Revit to tree uses `UIApplication.SelectionChanged` (Revit 2023+) and
  `GetReferences()` for linked elements, mapped through an index from
  `(document, UniqueId)` to tree path built with the count aggregation.
- Both directions are debounced (about 150–300 ms). A selection the panel set
  itself is recorded and the matching `SelectionChanged` is ignored; the same
  guard covers Live, the context strip and the T3 queue.
- **Events:** the panel subscribes to Revit events once and fans them out
  through an internal hub. Panel-level parts (context bar, context strip,
  temporary bar, caches, `DocumentChanged`) listen while the pane is visible.
  Tab-level handlers (Tree sync, Filter Live, Layers `ViewActivated`) listen
  only while their tab is open; a tab that comes back resyncs once. A hidden
  pane pauses everything and resyncs when shown.

### 7. Layers tab

Per category, grouped by document: Solo, Halftone, Color, Show. It always
works on the active view (Scope does not apply) and lists only ticked
documents. A host category row affects the host and every link in By Host
View; the tooltip says so. A row under a link is scoped to that link (T3).

### 8. Visibility: temporary first

Temporary mode is chosen for how it behaves in a shared model: it is not saved,
needs no borrowed view, works when a view template locks Visibility/Graphics,
and one call resets it.

- Hide, isolate, show and solo use the temporary modes:
  `HideElementsTemporary`, `IsolateElementsTemporary`,
  `HideCategoriesTemporary`, `IsolateCategoriesTemporary`, and for linked
  elements the matching temporary UI commands (T3).
- Halftone and color run in Temporary View Properties mode when the spike
  confirms that its overrides are dropped on exit; otherwise they are
  permanent view overrides, which are view state and allowed.
- **Keep** converts to permanent (`ConvertTemporaryHideIsolateToPermanent`).
  **Reset** (`DisableTemporaryViewMode`) returns the view to the state of the
  last Keep. Revit also drops temporary state when the model closes.
- **The eye** is one toggle. It acts on every selected tree row when the
  clicked row is selected, otherwise on that row. One click is one request.
  Revit has no per-element temporary unhide, so turning an eye back on resets
  temporary mode and re-applies the rest in one step.

### 9. Tiers, resolved per request

| Tier | Applies when | Mechanism |
|------|--------------|-----------|
| T0 Current document | Target is its elements | API, by id |
| T1 Link instance | Target is a whole link | API on the `RevitLinkInstance` id |
| T2 Category everywhere | Whole categories across all ticked documents, links in By Host View | `HideCategoriesTemporary` / `IsolateCategoriesTemporary` |
| T3 Inside one link | One link's category, a rule result, instances | Evaluate in the link document, `SetReferences`, `PostCommand` |

A rule result spanning documents splits: current-document matches go T0,
link matches go T3. T2 never switches a link's display mode. Rows and actions
that go through T3 carry a small marker; where T3 cannot run (nested link,
`CanPostCommand` false) the control is disabled with the reason. Select, Box
and Zoom on linked elements always use public API.

### 10. PostCommand queue

- Single flight; multi-step flows advance on `Idling` as a state machine.
- `CanPostCommand` is checked first.
- The user's selection is snapshotted, replaced by link references, and
  restored after the command runs.
- Command ids resolve in one place through `RevitCommandId` lookup (verified
  with Command Browser).

### 11. State and storage

| State | Source |
|-------|--------|
| Temporary hide/isolate, current document | Read back from the view |
| Category visibility, overrides, link instance | Read back from the view |
| Section box | `View3D.GetSectionBox` |
| T3 changes to linked elements | In-memory journal per `(Document, view)` |
| Document list, Tree Sync, Filter Live, selection mode | In memory per session |
| Custom filters | Settings (`DevTools.Settings`) |

- The T3 journal stores `(linkInstance.UniqueId, element.UniqueId, action)`.
  It clears with the temporary mode, on `DocumentClosing` and on link reload;
  Undo/Redo marks it stale. Link rows it does not know show the eye as unknown.
- A custom filter stores categories, sets and rules. A parameter is stored by
  `BuiltInParameter` name, shared parameter GUID, or name, in that order.
  Element ids are never stored.

### 12. Ownership

- Panel, resolver, filter engine and T3 queue: `RevitDevTool.Tools` (0036).
  Link references reuse `Selection/ElementSelector`.
- Dockable pane registration: `RevitDevTool.Core` `DockablePaneProvider`
  (0007).
- Shared controls (flat tree list, icon geometries): `DevTools.UI`.

## Alternatives Considered

1. **Public API only (T0–T2).** Cannot hide one link's categories or act on
   link-only data. Kept as the default wherever it is exact.
2. **PostCommand for everything.** No read-back, fragile timing, untestable.
3. **View filters to apply rule results.** Exact for host parameters, but each
   is a new project element. Rejected by §1.
4. **Extensible Storage for view state or the T3 journal.** Writes model data;
   too heavy for a view convenience.
5. **Separate Explore mode** (category → parameter → values browser). Same
   result as a rule with a value dropdown; two modes confused the task.
   Folded into the rule value dropdown.
6. **Revit filters only, no custom rules.** Links are read only and new
   Revit filters would be new elements, so ad-hoc and link-data filtering would
   be impossible.
7. **Find & Replace in the Filter tab.** Replace writes element data and cannot
   run in links. Kept out of the panel.
8. **Selection mode Intersect.** Overlaps Scope = Selection.
9. **Host versus link UI (link switch, link tags, link colors).** Replaced by
   one documents list.
10. **Inline "Save as custom filter" box.** Replaced by + / Delete / Save on
    the source combo.
11. **One global sync switch for Tree and Filter.** Tree syncs both ways and
    Filter one way; one switch hid that difference. Each tab keeps its own.
12. **Snapshot the selection for Scope = Selection** so Sync could stay on.
    Hiding Sync in that scope is simpler and has the same effect.
13. **Selection summary in the context strip, per-document counts.** Repeated
    the tree and did not fit long model names. Replaced by the tree status
    line.

## Consequences

Positive:

- One way to reach elements in any document; links need no special UI.
- Normal use leaves no trace in the saved model. Closing the model clears
  everything except what the user chose to Keep.
- Selecting by an existing Revit filter, including filters inside links, which
  Revit itself cannot do.
- Settings stay small: named custom filters only.

Tradeoffs:

- T3 relies on PostCommand: a selection change between post and run, manual
  edits, or Undo can make the journal drift within a session.
- Turning one eye back on in temporary mode resets and re-applies, which may
  flicker.
- A rule result in a link goes through T3 even when a view filter could have
  done it.
- Revit 2022 users do not get the panel. T3 does not cover nested links.
- Box is limited to existing 3D views.

## Follow-Up

- Spike on a live host with links (Revit 2023 or later):
  1. Command ids for Temporary Hide/Isolate Element, Hide/Unhide in View
     Elements, Override Graphics by Element.
  2. `SetReferences` to link references, then post each command; record what
     acts on linked elements and what `CanPostCommand` returns.
  3. `HideCategoriesTemporary` reaches link elements in By Host View.
  4. Temporary View Properties: overrides are dropped on exit, host and
     linked; arguments when the view has no template.
  5. Timing of post to `Idling` at about 1k references; `SetReferences` plus
     Temporary Hide at about 10k and 50k.
  6. Whether temporary hide/isolate adds undo entries; whether any
     per-element temporary unhide exists.
  7. Flicker of reset plus re-apply.
  8. Undo after a T3 action is visible in `DocumentChanged`.
  9. What `RevitLinkGraphicsSettings` exposes; how to tell which linked view a
     link displays (for "in view" on link filters).
- Open question: flat virtualized tree list in `DevTools.UI` versus a styled
  `TreeView`; benchmark on a model of about 100k elements with 2–3 links.
- Reading rule-based Revit filters back into rule rows needs a converter for
  `ElementLogicalFilter` / `ElementParameterFilter` / `FilterRule` types.
- After the spike, set Accepted or amend, then add
  `docs/product/model-context.md` and an architecture note.

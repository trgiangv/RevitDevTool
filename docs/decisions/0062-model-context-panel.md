# 0062 Model Context Panel

Date: 2026-11-13

## Status

**Proposed.** The live suites pass on Revit 2026 and 2024. On 2024 the only
differences are golden values taken from the 2026 sample and SB-01 rebar.
Accepted after a Revit 2023 live run.

## Context

`RevitDevTool.Tools` gets a dockable **Model Context** panel for daily model
work: browse, filter and select elements, then show, hide, isolate, halftone,
color, box or zoom the result in the active view. Elements in loaded RVT links
must work the same way as elements of the open model.

**Vocabulary.** *Current model* is the open document the user works in. *Link*
is one loaded RVT link instance. *All models* is the Explorer group that acts
on a category everywhere. The UI never says "host"; below, "host" appears only
inside Revit API terms (host view, host `ElementId`, By Host View).

Behavior: [product/models-context.md](../product/models-context.md). Structure:
[architecture/ModelsContext](../architecture/ModelsContext/README.md). The
design canvas (*Model Context Panel — UX directions*, flows 1–8) is history;
this ADR wins where they differ.

Revit limits what a host view can do to elements inside a link. *Verified*
means read from the Revit API assemblies 2023.0–2027 or run on a host;
*believed* means not yet run.

1. `HideElements`, `HideElementsTemporary`, `IsolateElementsTemporary` and
   `SetElementOverrides` take host ids. A linked element has none, so these
   reach a link only as a whole `RevitLinkInstance` (verified).
2. Host category visibility reaches link elements only when the link displays
   By Host View, and then applies to every such link at once (believed).
3. A host view filter is a new element in the project. Under By Host View it
   reaches link elements for built-in and shared parameters (verified for
   `IFC_GUID`); link-only project parameters cannot drive it.
4. `RevitLinkGraphicsSettings` exists from 2024 (display mode, `LinkedViewId`);
   2025 adds the view filter type. No version exposes Custom mode per category.
   Revit 2023 cannot read a link's display mode (verified).
5. API code reaches a linked element only as a link reference put in the
   selection (`SetReferences`), then a Revit command. Spike on Revit 2026,
   Snowdon Towers with six links (verified there):
   - `PostCommand` takes only `PostableCommand` members, and posted view
     commands fail while the panel has focus.
   - The internal `CommandHandlerService.invokeCommandHandler(string)` runs a
     command synchronously from any focus. `ID_VIEW_HIDE_ELEMENTS` hides the
     referenced linked elements (Hide in View, saved with the view);
     `ID_VIEW_UNHIDE_ELEMENTS` unhides them while Reveal Hidden is on. About
     2,000 references take about 100 ms to select.
   - Temporary hide and isolate ignore link references; isolate and element
     overrides are refused, halftone changes nothing, and no internal entry
     point exists. Revit has no temporary hide, isolate or per-element
     override for a linked element.
   - Temporary View Properties does not undo a Hide in View made under it.
6. `FilteredElementCollector(hostDoc, viewId, linkInstanceId)` returns the
   linked elements visible in a host view. It exists from 2023.1, drops Hide in
   View elements, and took 0–8 ms per link on a 38k-element link once the view
   was drawn. On a sheet it crashes Revit natively (verified, 2026).
7. Temporary state is readable only per host element. Nothing reports
   temporarily hidden categories or hidden linked elements (verified).
8. Revit can hide or override by a filter, but cannot select by one.
9. `SelectionChanged` (2023+) forbids changing the selection or the document
   inside the handler (verified).
10. `UIDocument.ShowElements` takes host ids only (verified).
11. A host view filter keyed on `IFC_GUID` reaches linked elements but is not a
    usable key: string rules ignore case so GUIDs collide (one link had 1,466
    elements and 1,458 values), it cannot tell two instances of a link apart,
    and an OR filter of 7,823 rules took 305 s to regenerate the view. A
    `SelectionFilterElement` as a view filter applies to Current model ids
    only (verified, 2026).
12. Isolate by hiding the rest works: temporary isolate of the link instance,
    then Hide in View for the rest of that link. About 730 ms for 7,818 hides,
    about 5.5 s to restore them (verified, 2026). The hides are saved with the
    view.

## Decision

1. **Revit 2023.1 and later.** The panel is built under
   `REVIT2023_OR_GREATER`; the 2022 add-in does not register it. Two hints read
   a link's display mode and need 2024+: the link names in the *All models*
   tooltip and the "in view" marker on link filters (Custom links need 2025).
   On older versions the tooltip is generic and the marker is missing. Why: 2023
   lacks only these hints, not worth dropping its users (Context 4, 6).
   Model elements exclude Materials (reported as a model category, not placed objects) and Revit links.
2. **No model writes.** The panel never writes element data and never creates
   an element: no parameters, Extensible Storage, view filters or views. It
   changes only the active view's visibility and graphics, link instance
   overrides, and the selection. Replace is not part of the panel; it can
   become a separate tool that takes the panel's result. Why: the panel works
   on shared models and in links, which are read only.
3. **Documents are one list.** The Current model and each loaded link instance
   are equal entries of one documents box; the UI never splits into host and
   link areas. Links are opt-in, the last ticked entry cannot be unticked, and
   ticks live in memory per session. With Scope = Selection the selection
   decides the documents. Why: one way to reach elements in any document.
4. **Explorer and Filter, actions in a context menu.** Two views share one
   context bar (Scope, view switch, documents) and stay alive while hidden.
   Explorer browses Model › Category › Family › Type › Instance; Filter selects
   by categories and rules. Actions live in one context menu opened on the
   target (selected rows, or the Filter result), not a fixed actions bar. An
   item that cannot run on the target shows disabled with the reason. Why: a
   bar took a row on every view and sat away from the rows it acted on.
5. **Visibility lives on tree rows, and a row acts on exactly its group.**
   - *All models* category rows act on the category itself (T2): the Current
     model and every By Host View link.
   - *Current model* rows act on that branch's elements by id (T0), so a user
     can hide Ducts here and keep the linked Ducts. Elements drawn after the
     click are not covered until the eye is clicked again.
   - A link's model row acts on the whole link instance (T1). Rows below it
     offer the eye only (T3); Isolate, Halftone and Color are disabled there
     with the reason (Context 5).
   - The eye reads back from the view (temporary state for the Current model,
     the link view collector for links), so it reflects what Revit shows.
   Why: an earlier row that also hid the category in links gave users no way
   to hide a category in the Current model only.
6. **Temporary first.** Hide and isolate use Revit's temporary modes; Halftone
   and Color run inside Temporary View Properties. Why: nothing is saved with
   the view, it works when a template locks Visibility/Graphics, and one call
   resets it. The panel counts the changes (Revit cannot). **Keep** makes hide
   and isolate permanent; **Reset** ends both modes and puts back the element
   overrides recorded before the panel's first change, because element
   overrides outlive Temporary View Properties. Revit has no per-element
   temporary unhide, so turning one eye back on resets and re-applies the rest.
7. **Tiers, resolved per request.**

   | Tier | Target | Mechanism |
   |------|--------|-----------|
   | T0 | Current model elements | Public API by element id |
   | T1 | A whole link | Public API on the `RevitLinkInstance` id |
   | T2 | A category everywhere | Category hide, isolate and overrides |
   | T3 | Elements inside one link: Hide and Show only | Hide in View through the internal command handler |

   A result that spans documents splits: Current model matches go T0, link
   matches T3. T2 never switches a link's display mode. Select, Box and Zoom on
   linked elements use public API only. Nested links are out of scope for T3.
   - **T3 is one synchronous call** (`LinkedHideShow`) on the Revit thread: no
     `PostCommand`, no `Idling`, no queue. It swaps in link references, runs
     the command in a `TransactionGroup` (one undo entry), reads back with the
     link view collector, and restores the user's selection. A request that
     changed nothing fails with one sentence and rolls back.
   - The handler is found by reflection once per session. When it is missing,
     rows below a link's model row get no visibility actions.
   - T3 hides are saved with the view at once. The panel records each one in
     an in-memory journal and unhides exactly those on Show or Reset; it never
     unhides what the user hid in Revit. **Keep** asks first when the journal
     has entries, then leaves them hidden.
   - Isolate, Halftone and Color below a link's model row stay disabled:
     Revit has no command that applies them to one linked element (Context 5,
     11).
   Why: links need per-element hide as Revit's own UI has it, and the public
   API cannot do it (Context 1, 5).
8. **Filter policy.**
   - **Group builder without NOT.** A group holds conditions and nested groups
     with one AND or OR for the whole group, so there is no precedence to
     guess. Negation lives in the operators (does not equal, empty,
     undefined). A nested group may narrow its documents (by file) and
     categories, never widen them.
   - **One identity key per parameter:** built-in by `BuiltInParameter`,
     shared by GUID, project and family parameters by name plus data type.
     Origin and binding are tags, not part of the key. The key resolves in each
     document; a document without it gives no match. Why: the same rule must
     mean the same parameter in every model.
   - **Kind comes from the parameter**, never from the user; stored rules
     carry no kind.
   - **Typed numbers parse once in the Current model's units**, and that one
     value is used in every document. A value picked from the list keeps its
     stored value. Why: parsing per document gave one typed value a different
     meaning per model.
   - **The Properties palette is the truth.** Where Revit's own view filter
     disagrees with what Properties shows, the panel follows Properties
     (decided 2026-11-19): a typed number means every stored value that
     displays as it (10 fittings show "3 mm", stored 2.54 mm, so "≥ 3 mm"
     finds them); a reference set to *None* (id -1) is empty; a parameter
     the element does not list and no native rule sees is undefined for that
     element. Details in
     [filters.md](../architecture/ModelsContext/filters.md#compiling-a-rule-filtercompiler).
   - **Case-sensitive text is two passes.** Revit string rules ignore case, so
     a widened native pass gives a superset and an exact pass runs on it.
   - **Revit filters open read only and run natively** in their own document;
     a Current model filter never runs against a link. *Duplicate* copies one
     into a custom filter.
9. **Live and Selection sync.**
   - **Filter Live** (on by default) pushes the result to the Revit selection,
     one way. Add and Remove work against the selection taken when Live
     started, so edits never pile up. A model edit or context refresh only
     recounts and never touches the selection, so an edit in Properties never
     drops the element being edited.
   - **Explorer Selection sync** is two way and shown only with Scope = Active
     view or Whole model. It is never named plain "Sync".
   - A model switch never asks: each model's session keeps its unsaved custom
     filter and shows it again, still unsaved, on switch back.
   - Each tab keeps its own switch in memory per session. Both pause above a
     limit (5,000 elements) and offer *Select in Revit* instead.
   - The `SelectionChanged` handler only records the ids; work runs later.
     The panel drops the echo of a selection it sent itself.
10. **Architecture.**
    - **Host-free core plus ports.** `DevTools.ModelsContext` holds the model,
      engine, ports and view models with no Revit API and no WPF; the Revit
      adapter implements the ports. The core decides (for example
      `VisibilityPlanner` picks the tier), the adapter executes. Why: group
      rules, tier splits, Live and lifecycle are tested on fakes, and a sandbox
      runs the panel without Revit.
    - **Plain data only** crosses a port or a thread: keys, `long` ids,
      strings, structs. Snapshots are arrays of structs and name tables, built
      in one pass; parameter values are read on demand. Why: caches never pin a
      closed model or a reloaded link.
    - **Two keys.** `SourceId` (one document file) keys snapshots and caches,
      shared by every instance of a link; `DocumentKey` (one documents box
      entry) keys ticks, selection, view actions and the journal. Keys resolve
      to Revit objects on each call; a closed or unloaded document resolves to
      nothing.
    - **Revit-thread slices.** Every Revit call runs through the executor in
      short cancellable slices awaited one at a time; aggregation runs off the
      UI thread. Nothing subscribes to `Idling`.
    - **Links follow their document version** (same version keeps the
      snapshot, another applies the changes, unknown or large rebuilds); the
      Current model follows `DocumentChanged`.
    - **Sessions and generations.** Each open Current model has a session; a
      stale job result is dropped. A failed adapter call is one line in the
      panel and never throws into Revit. Failures log at Error, timings at
      Trace.
11. **UI.**
    - **A flat virtualized list, not WPF `TreeView`**, for the Explorer. Why:
      `TreeView` costs a panel per open node, scrolls to an unrealized path
      level by level, and has no multi-select.
    - **HandyControl stock look**: stock styles and brushes, shared icon
      geometries, no color literals, an AutomationId on every control.
    - **Fork rule.** A HandyControl control that needs more than a style is
      copied into `DevTools.UI/Controls`; `libs/HandyControl` is never edited.
    - **No self `;component/` URIs.** The add-in merges `DevTools.Presentation`
      and leaves only `DevTools.UI` loose (0037), so views use only
      `/DevTools.UI;component/...` URIs. A guard test enforces it.
12. **State and storage.** View state is read back from the view. The change
    count, the T3 journal, recorded overrides, ticks and switches live in
    memory. Settings hold only named custom filters; a parameter is stored by
    its identity key and element ids are never stored. Why: no model writes,
    and settings stay small.
13. **Deferred.** The context strip (expand a selection by System, Connected,
    Type, Family, Level, Workset, Host, Room) returns as its own amendment once
    its meaning across documents is settled. The context popup at the cursor
    is not in the first release. Isolate inside a link by hiding the rest
    (Context 12) stays a candidate.
14. **Ownership.** Core `source/DevTools.ModelsContext/`; views
    `source/DevTools.Presentation/Views/ModelsContext/`; shared controls and
    icons `source/DevTools.UI/`; Revit adapter and pane
    `source/RevitDevTool.Tools/ModelsContext/` (0036), pane registration in
    `RevitDevTool.Core` (0007); tests `tests/DevTools.ModelsContext.*` and
    `tests/RevitDevTool.ModelsContext.*`
    ([test matrix](../architecture/ModelsContext/test-matrix.md)).

## Alternatives Considered

1. **Public API only (T0–T2).** Cannot hide one link's elements. Kept wherever
   it is exact.
2. **PostCommand for everything.** No read-back, fragile timing, untestable.
3. **View filters to apply rule results.** Each is a new project element.
4. **Extensible Storage for view state or the T3 journal.** Writes model data.
5. **Separate Explore mode** (category, parameter, values browser). Folded into
   the rule value list.
6. **Revit filters only, no custom rules.** Links are read only and new Revit
   filters would be new elements.
7. **Find & Replace in the Filter tab.** Replace writes element data and cannot
   run in links.
8. **Selection mode Intersect.** Overlaps Scope = Selection.
9. **Host versus link UI.** Replaced by one documents list.
10. **Inline "Save as custom filter" box.** Replaced by + / Delete / Save.
11. **One sync switch for Explorer and Filter.** Explorer syncs both ways and
    Filter one way; each keeps its own.
12. **Snapshot the selection for Scope = Selection.** Hiding Selection sync is
    simpler.
13. **A fixed actions bar.** Took a row on every view and sat away from the
    rows. Replaced by the context menu.
14. **Selection summary in a context strip, per-document counts.** Replaced by
    the Explorer status line.
15. **WPF `TreeView`.** See Decision 11.
16. **Cache `Element` objects or rebuild on demand.** Pins documents, breaks on
    link reload, rebuilds the whole tree on every refresh.
17. **`UniqueId` as the sync key.** Revit reports ids and references; storing
    UniqueIds costs memory for no gain.
18. **Revit 2024 as the minimum.** 2023 lacks only two hints.
19. **One category row per document with a hidden link side effect.** Users
    could not hide a category in the Current model only.
20. **A separate Layers tab.** Repeated the tree's Model › Category levels; its
    own parts moved into the Explorer.
21. **Cysharp ObservableCollections for rows.** The visible list is small, its
    WPF bridge rejects range changes, and the cost at scale is in snapshots and
    Revit collection. Revisit if search or sort over large lists needs it.
22. **Switch links to By Linked View to keep them visible.** Changes link
    overrides behind the user's back.
23. **No T3 visibility at all.** Safe, but leaves links without per-element
    hide. Kept as the fallback when the internal call is missing.
24. **Temporary UI commands for T3** (`ID_TEMPHIDE_*`). Not postable, and they
    ignore link references.
25. **`PostCommand` queue on `Idling`.** Built and dropped: posted view-frame
    commands fail when the panel has focus, Show waited until timeout, and a
    failed run crashed Revit.
26. **Sandbox view** (duplicate the view, hide there, delete it). Moves the
    user to another view and creates and deletes views.
27. **Undo or Temporary View Properties to restore T3 hides.** Undo works only
    while the hide is the last change; Temporary View Properties does not undo
    a Hide in View.
28. **CustomExporter or a document diff to find hidden linked elements.** The
    link view collector already drops them; the journal says which ones the
    panel hid.
29. **Halftone and Color for linked elements through an `IFC_GUID` view
    filter.** GUIDs collide, instances cannot be told apart, it slows sharply
    with the number of targets, and it adds a project element (Context 11).
30. **`ID_VIEW_OVERRIDE_ELEMENTS(_HALFTONE)` or a `SelectionFilterElement` for
    linked elements.** Revit refuses the first; the second applies to Current
    model ids only.
31. **Isolate a linked element with an inverted view filter.** Leaves the
    categories without `IFC_GUID` visible and regenerates every link.

## Consequences

Positive:

- One way to reach elements in any document; links need no special UI.
- Normal use leaves no trace in the saved model except what the user chose to
  Keep and T3 hides not yet shown again or reset.
- Selecting by an existing Revit filter works, including filters inside links.
- Settings stay small: named custom filters only.
- Caches hold plain data, so closed models and reloaded links are never pinned.
- The core and views are tested and measured without Revit.

Tradeoffs:

- T3 hides are Hide in View: saved with the view at once and one undo entry
  per Hide or Show.
- T3 relies on an internal, undocumented Revit call that may change in any
  release; each supported year needs a check, and the fallback is no
  visibility actions below a link's model row.
- Isolate, Halftone and Color cannot target single elements or categories
  inside a link.
- Turning one eye back on in temporary mode resets and re-applies, which may
  flicker.
- Revit 2022 users do not get the panel. On 2023 two link hints are generic.
- Counts can lag a large model change by one rebuild.
- Box is limited to existing 3D views.

## Follow-Up

- Revit 2023 live run, then set Accepted or amend.
- T3 checks on other years: Hide, Reveal + Unhide and the temporary checks on
  2023, 2025 and 2027.
- Open spike steps (MCP `launch_host`, then `execute_csharp_code`):
  1. Hide and Unhide on a template-locked view and on a view another user owns.
  2. Why hiding 500 references of the Architectural link hid 548 elements.
  3. `SetReferences` with elements that have no graphics (MEP systems,
     unplaced rooms).
  4. Category hide and isolate with By Host View, By Linked View and Custom
     links; category overrides on exit from Temporary View Properties.
  5. Undo entries of temporary hide, Reset and Keep; Keep on a
     template-controlled view.
  6. Link view collector as read-back (section box, phase, V/G, temporary
     hide) and its time at 10k and 50k elements.
  7. T3 Hide and Show time at about 1k, 10k and 50k references.
  8. Isolate inside a link by hiding the rest (Context 12): Reset cost, locked
     templates, link display modes, nested links, reload while isolated, and
     how it combines with Keep, Reset and the journal.
  9. Undo and Redo ids in `DocumentChanged`; if unusable, they mark the
     snapshot dirty instead.
  10. Flicker of reset plus re-apply.
  11. Document versions around a link reload, `GetChangedElements` cost and
      failure for an unknown version, Reload Latest, cloud models.
  12. Quick per-category count time on a 1M-element model.
- Benchmark on a real model of about 100k elements in Revit, and the
  first-load stall (over the 50 ms target; latest numbers in the plan). Targets:
  [architecture README](../architecture/ModelsContext/README.md#performance-targets).
- Execution plan: [2026-11-13-models-context-panel](../plans/active/2026-11-13-models-context-panel.md).

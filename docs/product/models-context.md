# Model Context Product Contract

A dockable **Model Context** panel in Revit 2023 and later. It browses, filters
and selects elements of the open model and its loaded links, and shows, hides,
isolates, halftones or colors them in the active view. Links work the same way
as the open model wherever Revit allows it.

The panel never writes element data and never creates elements (no view
filters, parameters or views). It only changes the active view's visibility and
graphics, and the selection.

Policy and the reasons behind each rule: [0062](../decisions/0062-model-context-panel.md).
How it is built: [architecture/ModelsContext](../architecture/ModelsContext/README.md).

## Words the panel uses

- **Current model**: the open model you work in. The panel never says "host".
- **Link**: one loaded RVT link instance. Two instances of one file are two
  entries.
- **All models**: the top tree group that acts on a category in every model at
  once.

## Layout

From top to bottom:

1. **Context bar**: the Scope buttons, then the Explorer / Filter switch.
2. **Documents box** on its own row (hidden when Scope is Selection).
3. **Body**: Explorer or Filter. Both stay alive while hidden, so switching keeps
   their state.
4. **Temporary bar** (yellow), only while the view is in a temporary mode.

A thin progress bar shows under the context bar while counts rebuild. A
one-line message shows when something fails; the panel never throws into Revit.
While Revit is busy (a modal command, edit mode, no open model) actions are
disabled with "Revit is busy" and nothing is queued for later.

## Scope

Three icon buttons decide which elements the panel counts and filters:

| Scope | Elements |
|-------|----------|
| Selection | What is selected in Revit, from any document, including elements of a secondary design option shown in the view they were picked in. The documents box hides: the selection decides the documents. |
| Active view (default) | What the active view shows, per ticked document, with the view's design option settings: a view that shows a secondary option counts that option and not the primary. Links count only in plan, ceiling, engineering, area, section, elevation, detail, 3D and walkthrough views; other views (sheets, schedules, legends, drafting) show no linked elements. |
| Whole model | Every model element of the ticked documents, main model and primary design options, the same set a Revit schedule lists. Secondary options never count, match or get selected here. |

Visibility actions always work on the active view, whatever the scope.

## Documents box

- Lists the Current model and each loaded link instance as equal entries. Duplicate
  file names get an instance suffix.
- Ticked entries show as tags; tags that do not fit fold into a `+N` tag. The
  drop-down has search and **All**.
- The Current model is ticked on first load. New links start unticked.
- The last ticked entry cannot be unticked (from the list, **All** or its tag), so
  the panel always has something to query.
- A refresh (link reload, view change) keeps your ticks. An unloaded link shows
  disabled and keeps its tick; **All** skips it.
- Ticks live in memory per open model, not in settings.

## Explorer

A flat, virtualized tree: **Model › Category › Family › Type › Instance**.

- **All models** sits on top and lists every category found in any ticked
  document with the merged count. Its rows act on the category everywhere.
- Then **Current model**, then one group per ticked link.
- Every row shows its count in grey. Instances load on expand in pages of 200
  ("200 of 1,402 · more").
- **Search** matches row text only: model, category, family, type and instance
  names, and Element Id (`linkInstanceId@id` works). Parameter values belong to
  the Filter view.
- **Expand all** opens down to Type rows; **Collapse all** leaves the roots.
- **Status line**: *N selected* (elements under the selected rows) and
  **Clear**. When part of the Revit selection is in unticked documents, **Show in
  Explorer** ticks them and reveals the rows.

### Selection sync

An icon toggle beside the search box (tooltip *Selection sync: tree and Revit
select together*). Shown only with Scope = Active view or Whole model.

- On: selecting rows selects their elements in Revit (a Type or Category row
  selects all its instances in scope), and selecting in Revit opens and
  highlights rows.
- Up to 50 selected elements the tree opens each path and scrolls to the first.
  Above that rows stay closed with blue badges and the hint *Too many to reveal ·
  Reveal all*.
- Above 5,000 elements sync pauses for that change and *Select in Revit* shows in
  the menu.

### Visibility on rows

The eye (**Show / Hide**) is always visible. **Isolate**, **Halftone** and
**Color** show on the hovered or selected row. A row acts on exactly the group it
sits in:

| Row | Acts on |
|-----|---------|
| All models category | The category in the Current model and in every link that follows the view (By Host View). |
| Current model, any level | That branch's elements by id. Links keep their state. |
| A link's model row | The whole link instance (all four actions). |
| Rows below a link's model row | Hide and Show only (Hide in View). Isolate, Halftone and Color are disabled with the reason: Revit has no way to apply them to one linked element. |

- The eye has three states: shown, hidden, partly hidden. Hovering it highlights
  every row the click will change.
- Rows the panel hid stay in the tree, dimmed, so they can be shown again.
- A colored row keeps a swatch; a halftoned row keeps the accent marker, until
  Reset, Keep, undo or closing the model.
- Linked-element hides are Hide in View: saved with the view at once and one
  undo entry per click. They carry an orange dot.

### Context menu

Right-click the selected rows (or Shift+F10). Items: *Select in Revit* (hidden
while sync is on), Zoom (Z), Section box (B, 3D views only), Isolate (I),
Hide / Show (H), Halftone, Color…, and *Reset view* in temporary mode. An item
that cannot run on the target is disabled with the reason as its tooltip.

### Temporary bar

Hide, isolate, halftone and color use Revit's temporary modes, so they are not
saved with the view and work under a locked view template.

- *Temporary · N changes*: the panel counts the changes.
- **Keep** makes hide and isolate permanent. When linked elements are hidden it
  first asks: *N link elements stay hidden in this view* (Keep · Cancel).
- **Reset** ends the temporary modes and shows again every linked element the
  panel hid in this view.

## Filter

One editor: categories plus rules. The result is a count of matching elements
in the ticked documents and current scope.

### Source

The source combo lists **custom filters** first, then each ticked document's
Revit filters grouped under the model name: rule-based (funnel icon) and
selection filters (cursor icon). A Revit filter applied to the active view (or to
the link's linked view) shows "in view".

- **+** adds a custom filter named `New filter N`, with its name in edit mode.
- **Rename** (or F2) edits the custom filter's name. **Delete** removes it.
  Both are disabled for Revit filters.
- **Save** is enabled only while the custom filter has unsaved changes (a dot
  after its name).
- Names are trimmed, never empty, and unique among custom filters ignoring case.
  An invalid name shows a red box with a one-line reason and blocks Save and
  Enter. Esc, or leaving the box with an invalid name, restores the last valid
  name.
- Switching away from unsaved changes asks inline: Save · Discard · Cancel.
- **Revit filters open read only.** A rule-based filter runs in its own
  document with its own categories; a selection filter uses its stored ids.
  Rules the panel cannot show appear as *Unsupported rule*. **Duplicate** copies
  a rule-based Revit filter into a new custom filter (unsupported rules dropped).
- Custom filters are saved in settings by name. A parameter is stored by
  built-in name, shared GUID, or name and data type. Element ids are never
  stored.

### Categories

A checkbox list with counts and search, folded to a summary line. An element
counts under its own category (a wall used as a curtain panel counts as Curtain
Panels). *All · None*
tick or untick the categories the search shows. Checked categories always mean
"any of". With no categories checked the filter looks at every model category.

### Conditions and groups

- A **group** holds conditions and nested groups (**+ Condition**, **+ Group**).
  One **AND** or **OR** button sits between its children. It shows the group's
  match, and a click switches the whole group. There is no NOT: use the negative
  operators (*does not equal*, *empty*, *undefined* ...).
- Each condition is parameter, operator, value. With no conditions every element
  of the checked categories matches. A custom filter with no categories and no
  conditions matches nothing.
- **Group scope**: each nested group can limit its documents (by file, so every
  instance of a link) and its categories. Empty means *Same as parent*. A scope
  only narrows its parent, and the lists offer only the parent's items.
  - A value that is no longer in the parent stays as a tag and the group shows a
    warning.
  - A saved document the model does not have marks the group *unresolved*; it
    matches nothing.
  - Example: (Arch.rvt, Walls: Fire Rating = 2h) OR (MEP.rvt, Ducts: Insulation
    undefined).
- **Order**: drag a card by its six-dot grip, or focus the grip and press
  Alt+Up / Alt+Down. A condition or a group moves within its group or into
  another group, never into itself; a line shows where it will land.
- **Collapse**: the arrow in a group header hides its children.
- **Parameter picker**: type to search; the list opens on its own once 100 or
  fewer items match (Ctrl+Space opens it any time). Rows sit under two headers,
  **Instance · N** and **Type · N** (rows shown), which stay at the top while
  scrolling and cannot be picked. A row is the parameter name, a source icon
  for Project (green), Shared (blue) and Family (orange) parameters (built-in
  shows none), and its categories on the right ("Doors, Windows +2"). A name
  that matches the typed text exactly comes first in its section.
- **Tooltip** on a row and on the chosen parameter: the name, then only the
  facts it has (BuiltInParameter, GUID, Discipline, Type of parameter, Group),
  the models that have it, and its categories (families for a family
  parameter), five each then "+ N more".
- A parameter that only some models have is not a warning: a rule on it
  matches in the models that have it.
- **The list follows the group's scope.** It lists the union of parameters
  found for any category in the condition's group scope (its documents and
  categories after narrowing). With no category in scope it lists all. Value counts
  follow the same scope. A condition whose parameter drops out of the list keeps
  it and shows its name.
- Built-in parameters Revit can filter on are listed even when Revit's filter
  dialog leaves them out (for example Reference Level on ducts).
- One parameter is one item across documents: built-in by its built-in id,
  shared by GUID, project and family parameters by name plus data type. A
  family parameter and a project parameter with the same name and data type
  are one item; the same name with another data type is another item. The
  chosen parameter box shows the source icon and "Type" for a type parameter.

### Operators and values

The value kind comes from the parameter, never from the user:

| Kind | Operators | Value editor |
|------|-----------|--------------|
| Text | equals, does not equal, greater / less (or equal), contains, begins with, ends with, and their negations | Free text with suggestions |
| Integer, Number | equals, does not equal, greater / less (or equal) | Free text with suggestions. A typed number is read in the Current model's units (the placeholder shows an example such as "300 mm") and means the same value in every model; equals matches every value that displays as the typed text there. A value picked from the list keeps the stored number behind it |
| Yes/No | equals, does not equal | Yes / No |
| Element (reference) | equals, does not equal | Pick from the values found |
| Enum (integer with named values) | equals, does not equal | Pick from the names found |

Every kind also has *not empty*, *empty*, *defined* (the element has the
parameter) and *undefined* (it does not), which need no value.

- The value drop-down lists real values in the ticked documents with counts, at
  most 500 ("more…"), and a `[No parameter] · N` line.
- **Aa** (Text only, off by default) makes the rule match case. Revit's own text
  rules ignore case; with Aa on, `= "S10"` no longer matches `s10`.
- *does not equal* and the other negative operators skip elements that do not
  have the parameter. Use *undefined* to find those.
- A parameter that is missing in a document gives no match there (except for
  *undefined*, which then matches every element of that document).
- Type parameters work like instance parameters: *empty*, *not empty*,
  *defined* and *undefined* look at the element's type.
- A Revit filter with an inverted rule reads as the negated operators with AND
  and OR swapped; a rule with no negated operator reads as *Unsupported rule*.

### Result row

- The match count.
- Selection mode **New / Add / Remove**: replace the Revit selection, add to it,
  or remove from it. Add and Remove work against the selection captured when Live
  was turned on, so repeated edits never pile up.
- **Live** (on by default): each edit of categories, rules, mode or source pushes
  the result to the Revit selection after about 300 ms. Above 5,000 matches Live
  pauses: the switch and its label *Live* stay as they are (your choice is
  kept), one short warning line under the count reads *Paused: over 5,000
  matches*, nothing is sent, and *Select in Revit* shows in the menu. The line
  follows the state: it is gone as soon as the result is 5,000 or fewer (a
  narrower filter, no categories, no filter selected) or Live is off, and Live
  sends again by itself.
- A model edit or a context refresh (scope, documents, view) only recounts. It
  never changes the Revit selection, so an edit in Properties never drops the
  element being edited.
- **⋯** opens the same context menu as Explorer for the result.

## Not in this panel

- Writing parameter values (Replace).
- Isolate, Halftone or Color on single elements inside a link.
- Expanding a selection by system, host, room and similar (deferred, 0062 §14).

## Related

- Decision: [0062](../decisions/0062-model-context-panel.md)
- Architecture: [architecture/ModelsContext](../architecture/ModelsContext/README.md)
- Diagnostics: [verification.md#model-context-diagnostics](../agents/verification.md#model-context-diagnostics)

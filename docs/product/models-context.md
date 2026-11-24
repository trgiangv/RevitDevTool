# Model Context Product Contract

A dockable **Model Context** panel in Revit 2023 and later. It browses, filters
and selects elements of the open model and its loaded links, and shows, hides,
isolates, halftones or colors them in the active view. Links work the same way
as the open model wherever Revit allows it.

The panel never writes element data and never creates parameters or views. It
changes the active view's visibility and graphics and the selection. Its one
model write is a temporary view filter for Halftone and Color inside a link
(see below), deleted on Reset, save, sync and pane close.

Policy and the reasons behind each rule: [0062](../decisions/0062-model-context-panel.md).
How it is built: [architecture/ModelsContext](../architecture/ModelsContext/README.md).

## Words the panel uses

- **Current model**: the open model you work in. The panel never says "host".
- **Link**: one loaded RVT link instance. Two instances of one file are two
  entries.
- **Categories box**: the Explorer list of categories that decides what the
  tree shows and where a category is hidden or shown.

## Layout

From top to bottom:

1. **Context bar**: the Scope buttons, then the Explorer / Filter switch.
2. **Documents box** on its own row (hidden when Scope is Selection).
3. **Body**: Explorer or Filter. Both stay alive while hidden, so switching keeps
   their state. Explorer shows the Categories box under the documents box.
4. **Why hidden strip**, only while it is open.
5. **Temporary bar** (yellow), only while the view is in a temporary mode.

A thin progress bar shows under the context bar while counts rebuild. A
one-line message shows when something fails; the panel never throws into Revit.
The message stays while the context stays and goes away when the scope, the
ticked documents, the tab, the active model or the active view changes.
With no open model, and while a Pick runs, actions are disabled with "Revit is
busy". In edit mode an action that changes the view stops with "Revit is busy."
when it runs. During a modal command a request waits until Revit is idle, then
runs. When the view switched after the panel last read it, an action, Keep or
Reset reads the active view again and runs on it; it refuses with "The active
view changed." only when the active view still differs (another model became
active).

## Scope

Three icon buttons decide which elements the panel counts and filters:

| Scope | Elements |
|-------|----------|
| Selection | What is selected in Revit, from any document, including elements of a secondary design option shown in the view they were picked in. The documents box hides: the selection decides the documents. |
| Active view (default) | What the active view shows, per ticked document, with the view's design option settings: a view that shows a secondary option counts that option and not the primary. Links count only in plan, ceiling, engineering, area, section, elevation, detail, 3D and walkthrough views; other views (sheets, schedules, legends, drafting) show no linked elements. |
| Whole model | Every model element of the ticked documents, main model and primary design options, the same set a Revit schedule lists. Secondary options never count, match or get selected here. Materials are not model elements and never count. |

Visibility actions always work on the active view, whatever the scope.

## Documents box

- Lists the Current model and each loaded link instance as equal entries. Duplicate
  file names get an instance suffix.
- Ticked entries show as tags; tags that do not fit fold into a `+N` tag. When
  even one tag does not fit, the first tag shrinks with an ellipsis. The
  drop-down has search and **All**.
- When the panel opens and on every view change (a model switch too), the ticks become
  the Current model plus each loaded link the active view draws (instance not hidden,
  RVT Links shown, not filtered out). Your own ticks then hold until the next view change.
- The last ticked entry cannot be unticked (from the list, **All** or its tag), so
  the panel always has something to query.
- A refresh (link reload, model edit) keeps your ticks. An unloaded link shows
  disabled and keeps its tick; **All** skips it.
- Ticks live in memory per open model, not in settings.

## Explorer

A flat, virtualized tree: **Model › Category › Family › Type › Instance**.

A category is a model category, or a built-in model subcategory that owns elements. A subcategory is a child of
its category in the tree (Toposolid › Sub-divisions, Railings › Top Rails), with its own count; the parent counts
its own elements plus its children. Subcategories of Site, Lines and Topography (base and survey points, sketch
lines, contours) and every Center line subcategory (ducts, duct fittings, flex ducts, pipes, conduits, cable trays)
are not listed, so Whole model is the set a Revit schedule lists.

- The tree starts with **Current model**, then one group per ticked link.
- Every row shows its count in grey. Instances load on expand in pages of 200
  ("200 of 1,402 · more").
- **Search** matches row text only: model, category, family, type and instance
  names, and Element Id (`linkInstanceId@id` works). Parameter values belong to
  the Filter view. In Active view it also finds hidden elements, dimmed.
- **Pick** (icon beside the search box): click or box elements of the ticked
  links in the view, no Tab needed. Only elements of the ticked links and
  categories can be picked. The picked set becomes the Revit selection. While
  picking the status reads *Picking · Esc to finish*; Esc ends it. Disabled
  with *Tick a link to pick in it* (no link ticked) or *Tick a category to pick*.
  Current model elements are picked in Revit as usual.
- **Expand all** opens down to Type rows; **Collapse all** leaves the roots.
- **Status line**: *N selected* (elements under the selected rows, hidden at
  0) and **Clear**. When part of the Revit selection is in unticked documents, **Show in
  Explorer** ticks them and reveals the rows.

### Categories box

Under the documents box. Lists the categories of the ticked documents with
counts, search, **All · None**. Every category starts ticked; a category that
appears later shows ticked.

- Selection is the tick. A click on a name ticks only that row; Ctrl+click adds
  or removes, Shift+click and Shift+arrows tick a range, Ctrl+A ticks every
  listed row, Space toggles the focused row. The checkbox toggles its own row and
  keeps the others. Right-click keeps the ticks. Rows are never painted as
  selected. Ticks hidden by the search stay.
- Subcategories list under their category, collapsed at first; the chevron (or
  Right / Left on the row) opens and closes them. The parent counts its own
  elements plus its children. The parent drives its children: ticking or
  selecting the parent (box, name click, Ctrl, Shift, Space) ticks all of them,
  unticking or deselecting it clears them. A child still ticks on its own; the
  parent box then shows a partial mark, reads ticked once every child is ticked
  and clear once none is. Clicking a partial box ticks everything. A search
  that finds a subcategory opens its parent while the search lasts; clearing the
  search restores what you had opened. **All · None** also reach the children
  of a closed parent. A parent with no elements in scope still shows, with 0.
- The ticks decide what the tree lists.
- The eye after the count hides or shows the category in the view, in the
  Current model and in every link that follows the view (By Host View). A
  hidden category dims. The tooltip names what the eye reaches, for example
  *Hide in view: Current model + 2 links (By Host View)*, and notes the links it
  does not reach; when the view has such a link, the panel also says *Links By
  Linked View or Custom keep their own settings* after the click. The eye acts on
  its own row only.
- Right-click a category: **Isolate**, **Halftone**, **Color** for the
  ticked rows. Right-click never changes the ticks. With no row ticked the
  items are disabled with *Select categories*.
- **Isolate** leaves only the ticked categories: in Temporary View Properties
  it hides every other category the view can hide (model, annotation,
  analytical, imports, lines, levels, grids). It reaches the Current model and
  every link By Host View, so isolating Doors leaves only doors, linked doors
  included. The link instances stay, and the few categories Revit never hides
  in a view (cameras, model groups, divisions, multistory stairs) stay drawn.
  **Parent overrides children.** Every action on a parent (Hide, Show, Isolate, Halftone, Color, select, in the
  tree or the Categories box, Current model or link) covers the parent and all its subcategories and replaces
  what a subcategory had; an action on a subcategory covers that subcategory only, and the latest action wins.
  Reset brings both back to how the model was. Revit cannot show a subcategory under a hidden parent, so its eye
  then says *Show its parent category first.* A ticked subcategory alone keeps its parent drawn on Isolate.
  Reset ends the mode and every category is back. A link By Linked View or
  Custom keeps its own settings; the panel says so: *Links By Linked View or
  Custom keep their own settings.*
- Under Scope = Selection the box stays, for Pick; the tree still shows the
  selection.

### Hidden elements (Active view)

A group row shows *+N hidden* after its count when the view hides some of its
elements. Click it to list them, dimmed; click again to stop. Group actions
(Select, Hide, Isolate) act on the visible elements only; a dimmed row acts
alone, when selected. Whole model has no hidden counts. Only elements a view of
this type can draw count: sheets, elements with no geometry, view-owned elements
of another view (all view-owned elements of a link), rooms and spaces in 3D, and
areas outside area plans never count.

### Selection sync

An icon toggle beside the search box (tooltip *Selection sync: tree and Revit
select together*). Shown only with Scope = Active view or Whole model.

- On: selecting rows selects their elements in Revit (a Type or Category row
  selects all its instances in scope), and selecting in Revit opens and
  highlights rows.
- Up to 50 selected elements the tree opens each path and scrolls to the first.
  Above that rows stay closed with blue badges and the hint *Too many to reveal ·
  Reveal all*.
- Above 5,000 elements sync pauses for that change and *Select* shows in the
  menu.

### Visibility on rows

The eye (**Show / Hide**) is always visible. **Isolate**, **Halftone** and
**Color** show on the hovered or selected row. A row acts on exactly the group it
sits in:

| Row | Acts on |
|-----|---------|
| Current model, any level | That branch's elements by id, the whole branch (not only what is listed). Links keep their state. |
| A link's model row | The whole link instance (all four actions). |
| Rows below a link's model row | Hide and Show (Hide in View). Isolate keeps the row's elements and hides the rest of that link in the active view. Halftone and Color on a category, family or type row color that family or type (see below); on an instance row they are disabled with the reason. |

**Isolate inside a link.** The link and any Current model targets are isolated
temporarily; the rest of the link that the active view shows is hidden with Hide
in View (saved with the view, orange dot). Reset or turning an eye back on shows
it again. Refused, with one line, when Reveal Hidden Elements is on, the view shows no links, or more than 10,000 linked elements
would be hidden. After a Revit crash the hidden rest stays hidden in that view;
Reveal Hidden Elements brings it back.

**Halftone and Color inside a link.** Revit cannot override one linked element,
so the panel adds a temporary view filter named *Model Context 1* (2, …): Family
Name, plus Type Name for a type row, or the Filter's own rules for a Filter
result. The tooltip says *Applies to every By Host View link with this type*: the
filter also reaches that family or type in the Current model and other links that
follow the view. Refused, with the reason, when:

- the row is an instance or the elements come without a rule (*Revit can color
  linked elements only by family, type or a rule filter.*);
- the Filter has a rule Revit cannot draw (case-sensitive, defined, undefined, a
  rule scoped to one model, or a parameter Revit cannot filter);
- the request has more than 100 rules;
- the link is By Linked View or Custom, or Revit is 2023 or older.

The filter is the panel's one model write. It runs inside Temporary View
Properties (so a template that locks Filters still draws it) and is deleted on
Reset, before save and synchronize, and when the pane is closed. Keep leaves it
until the next Reset, like any override.

- The eye has three states: shown, hidden, partly hidden. Hidden means every
  element of the row that the active view can draw is hidden; elements the view
  does not draw (another level, outside the crop) never make it partly hidden.
  Hovering it highlights every row the click will change.
- Rows the panel hid stay in the tree, dimmed, so they can be shown again.
- While a category is hidden with the Categories box eye, its rows in every
  group show it as hidden by the category and are disabled until it is shown.
- Hide, Isolate, Halftone and Color on a Current model family instance also
  reach its shared nested instances (a table takes its chairs along); Reset
  undoes the same set. A linked instance already hides with its nested ones.
- A colored row keeps a swatch; a halftoned row keeps the accent marker, until
  Reset, undo or closing the model. Keep leaves them, because Keep does not
  change a color or halftone.
- Linked-element hides are Hide in View: saved with the view at once and one
  undo entry per click. They carry an orange dot.

### Context menu

Right-click the selected rows (or Shift+F10). Items: *Select* (hidden
while sync is on), Zoom (Z), Section box (B, 3D views only), Isolate (I),
Hide / Show (H), Halftone, Color and *Why hidden?*. Reset lives only on the
Temporary bar. An item that cannot run on the target is disabled with the reason
as its tooltip. *Why hidden?* needs one element. When an item or a row tool
runs and Revit refuses, the reason shows in the panel's message line. An action
that started always finishes, even when the model changes while it runs.

### Why hidden

Select one dimmed row (or choose *Why hidden?* on one element, in Explorer or a
Filter result) and a strip opens above the Temporary bar with the first cause
in the active view, for example *Category off: Duct Fittings*. The chevron lists
the other causes; *No view setting hides it* when no check hits. The strip follows the selected
row, and closes with ✕, Esc, or when the selection has no element.

- Checks, first hit first: temporary hide or isolate, hidden in view, category,
  view filter, workset, phase, design option, not drawn (*No geometry to draw*,
  *Not drawn in this view type*), owner view, view range, crop or section box.
  The category check includes the line style under Lines. A ceiling plan reads
  *Below cut plane* or *Above view range*. A link adds its own first: link hidden, its category or workset,
  its display mode (By Linked View and Custom use the link's own settings).
- A nested family element that is not drawn and has no cause of its own takes
  its parent's causes, else reads *Hidden by its family: name* (the parent
  family's visibility settings or parameters turn it off).
- A filter that cannot be tested reads *Filter: name (possible)*.
- It only explains. **Show** appears only where the panel can undo the cause: a
  Current model element the panel hid or left out of its isolate, or a linked
  element the panel hid. A temporary hide made in Revit is a cue, with no button.
  A category off or a permanent hide is a cue, with no button.
- Active view only; a view that is not a model view says so.

### Temporary bar

Hide, isolate, halftone and color use Revit's temporary modes, so they are not
saved with the view and work under a locked view template.

- *Temporary · N changes*: the panel counts the changes.
- **Keep** (tooltip *Make hides permanent in this view*) turns Revit's temporary
  hide and isolate of elements and link instances into
  permanent hides; those changes, and the linked elements the panel hid (already
  saved with the view), leave the bar. Kept linked elements keep their eye
  (hidden) and orange dot, Reset leaves them hidden, and their eye shows them
  again. Everything Keep does not make permanent
  stays on the bar and in its count, and the next Reset still undoes it:
  a category Hide or Isolate from the Categories box (they live in Temporary View
  Properties), Halftone and Color, and the panel's view filters. Keep and Reset
  are plain buttons; neither is the default. When linked elements are hidden it
  first asks: *N link elements stay hidden in this view* (Keep · Cancel).
- **Reset** ends the temporary modes, deletes the panel's view filters, and
  shows again every linked element the panel hid in this view since the last Keep. When Revit does
  not show some of them (their category is off), Reset says so once and forgets
  them. Entries of a link that is no longer in the model are dropped.

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
  Switching to another model does not ask: each model keeps its unsaved filter,
  and switching back shows it again with its dot, ready to Save or Discard.
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
"any of". With no categories checked the filter looks at every model category,
and the summary reads *All categories*; when the filter also has no condition it
matches nothing, so the summary reads *None*.

### Conditions and groups

- A **group** holds conditions and nested groups (**+ Condition**, **+ Group**).
  One **AND** or **OR** button sits between its children. It shows the group's
  match, and a click switches the whole group. There is no NOT: use the negative
  operators (*does not equal*, *empty*, *undefined* ...).
- Each condition is parameter, operator, value. A condition without a value
  reads *Skipped · no value* and is left out. With no conditions every element
  of the checked categories matches. A custom filter with no categories and no
  conditions matches nothing.
- **Group scope**: each nested group can limit its documents (by file, so every
  instance of a link) and its categories. An empty box shows *Parent*: the group
  uses its parent's documents and categories. A scope
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
parameter) and *undefined* (it does not), which need no value. Empty means
what the Properties palette shows as blank: a reference set to *None* is
empty. A parameter Properties does not show for an element is undefined there,
even when another element of the same type has it.

- The value drop-down lists real values in the ticked documents with counts, at
  most 500 (**more…** opens the list; type to narrow), and a `[No parameter] · N`
  line.
- **Aa** (Text only, off by default) makes the rule match case. Revit's own text
  rules ignore case; with Aa on, `= "S10"` no longer matches `s10`.
- *does not equal* matches every element that has the parameter and does not
  hold the value, including an empty value (blank text, no number, a reference
  set to *None*), for every kind. It and the other negative operators skip
  elements that do not have the parameter. Use *undefined* to find those.
- A parameter that is missing in a document gives no match there (except for
  *undefined*, which then matches every element of that document).
- Type parameters work like instance parameters: *empty*, *not empty*,
  *defined* and *undefined* look at the element's type.
- A Revit filter with an inverted rule reads as the negated operators with AND
  and OR swapped; a rule with no negated operator reads as *Unsupported rule*.

### Result row

The row stays at the bottom of the Filter view while the rules scroll. It is
one line: the count on the left, then a toolbar with the mode, the selection
sync toggle and the tools. When the panel is narrow the tools move into the
toolbar's overflow (the arrow at its end), each with its icon and name. The
mode and the sync toggle always stay; at the narrowest widths the word
*matches* hides first (the count's tooltip still says it).

- The match count.
- Selection mode **New / Add / Remove**: replace the Revit selection, add to it,
  or remove from it. Add and Remove work against the selection captured when selection
  sync was turned on, so repeated edits never pile up.
- **Selection sync** (on by default): the same icon toggle as Explorer's
  selection sync. Each edit of categories, rules, mode or source pushes
  the result to the Revit selection after about 300 ms. Above 5,000 matches selection
  sync pauses: the toggle stays as it is (your choice is
  kept), one short warning line under the count reads *Paused: over 5,000
  matches*, nothing is sent, and *Select* shows in the menu. The line
  follows the state: it is gone as soon as the result is 5,000 or fewer (a
  narrower filter, no categories, no filter selected) or selection sync is off, and it
  sends again by itself.
- A model edit or a context refresh (scope, documents, view) only recounts. It
  never changes the Revit selection, so an edit in Properties never drops the
  element being edited.
- **Isolate**, **Halftone**, **Color** and the eye (**Hide** / **Show**) act on
  the result, like the row tools in Explorer; a tool that cannot run is disabled
  with the reason as its tooltip. Right-click the result row for the full
  context menu (Select, Zoom, Section box, Why hidden?).

## Not in this panel

- Writing parameter values (Replace).
- Halftone or Color on one linked instance (Revit has no per-element override
  in a link; family, type and rule filters are the closest).
- Expanding a selection by system, host, room and similar (deferred, 0062
  Decision 13).

## Related

- Decision: [0062](../decisions/0062-model-context-panel.md)
- Architecture: [architecture/ModelsContext](../architecture/ModelsContext/README.md)
- Diagnostics: [verification.md#model-context-diagnostics](../agents/verification.md#model-context-diagnostics)

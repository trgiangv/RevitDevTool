# UI Conventions

The views live in `source/DevTools.Presentation/Views/ModelsContext` and use
shared controls from `source/DevTools.UI/Controls`. Policy: [0062](../../decisions/0062-model-context-panel.md) Decision 11 and 14.

`ModelContextView` builds `FilterTabView` the first time the Filter tab is
selected. That view, with its own context menu, was about a third of the
panel's first build. The view adds it to `Body` and registers the name
`FilterView`, so `FindName("FilterView")` works once the tab has been shown.

## HandyControl stock look

- Use HandyControl stock styles and attached properties (`ButtonDefault.Small`,
  `ButtonIcon.Small`, `ComboBox.Small`, `LabelPrimary.Small`,
  `hc:IconElement.Geometry` and kin). Add a style only when no stock one fits.
- Colors come only from HandyControl brushes through `DynamicResource`
  (`PrimaryBrush`, `WarningBrush`, `RegionBrush`, `BorderBrush`, text brushes),
  so Light and Dark switch without restyling. No color literals.
- Icons are `Geometry` entries in `DevTools.UI/Theme/Styles/Icons.xaml` or HC
  geometries, never defined inside a view.
- `libs/HandyControl` is never edited. A control that needs more than a style is
  copied into `DevTools.UI/Controls` and changed there.

### Text styles in the Revit pane

Implicit (key-less) HandyControl styles do not reach a WPF tree hosted in a
Revit dockable pane the way they do in a normal window, so a bare `TextBlock`
can come out with the wrong color in Dark. Rules:

- **Labels and captions** set an explicit style: `TextBlockDefault`,
  `TextBlockDefaultSecLight`, `TextBlockDefaultWarning` and kin.
- **Row text inside lists and combo boxes** (parameter names, operator names,
  value rows, filter names) sets **no** style. It inherits the item container's
  foreground, which HandyControl switches to white on the selected row. An
  explicit style there would keep the dark text on the blue selection and make
  it unreadable.

## Parameter picker: `AutoCompleteComboBox`

`DevTools.UI.Controls.AutoCompleteComboBox` is an editable `ComboBox` with
filtering as you type (ported from DotNetKit's control).

- **Editable ComboBox, not a TextBox with a popup.** UI Automation then exposes
  the Value pattern on the box and the items as list items, so FlaUI tests and
  live checks can type a parameter name and pick an item by AutomationId.
- **Own filtered list.** When `ItemsSource` is a plain list, the control shows a
  `FilteredItems` copy of it. Every rule binds the same `Parameters` list, so a
  shared view would filter the picker of every other rule. A keystroke applies
  the filter as single removes and inserts in source order, so rows that stay in
  the list keep their containers; more than `FilteredItems.MaxEdits` changes send
  one reset instead. An `ICollectionView` source keeps its own `Filter`.
- **`MaxSuggestionCount`** (100): the drop-down opens on its own only when 100
  or fewer items match, so typing one letter into a list of 2,000 parameters
  does not render a huge popup. Ctrl+Space opens it any time.
- **Virtualized** `VirtualizingStackPanel` items panel and a fixed
  `MaxDropDownHeight`.
- **150 ms** debounce on typing (`AutoCompleteComboBoxSetting.Delay`).
- **Item text cached.** The text of each item (`TextSearch.TextPath`) is read once
  through a property lookup and kept per item until `ItemsSource` or the path
  changes, so a keystroke over 3,000 parameters filters in under a millisecond.
- The validation error template is hidden while the drop-down is open and put
  back on close, so the red box does not draw over the list.

- **Sections.** `GroupPath="Section"` groups the control's own items with a
  `PropertyGroupDescription` (groups sorted by name, so Instance before Type).
  `GroupStyle` `ParameterSections` hides empty groups, keeps a recycling
  `VirtualizingStackPanel` per group (`IsVirtualizingWhenGrouping`) and draws
  the header in a `StickyGroupHeader`: on `ScrollChanged` it moves itself down
  by how far its group has scrolled above the viewport, clamped to the group's
  bottom, so the header of the group under the top edge stays visible. Headers
  are not items, so selection and Up/Down never land on them.
  The header looks like the Categories box of the Filter tab: a 1 px
  `BorderBrush` frame with corner radius 4, a 7×9 chevron in `SecondaryTextBrush`,
  the name in SemiBold `TextBlockDefault` and the count in `SmallSecondaryText`. Its
  outer border fills with `RegionBrush` so rows that scroll under it stay hidden,
  and its UIA name joins the texts with " · " ("Instance · 5").
  A click on a header collapses or expands its section (chevron right or down;
  UIA ExpandCollapse on the header peer). The collapsed section names live on the
  combo box, so a recycled group container and the next drop-down open keep the
  user's choice. While a search filters the list, `AutoCompleteComboBox` sets the
  inherited `StickyGroupHeader.ShowAll`, so a match never hides in a collapsed
  section.
  The control applies the group description only while the drop-down is open. A
  new item source on a closed picker is not grouped. Grouping 3k parameters cost
  about 50 ms per picker, and a scope change gives every rule a new list.
  `AutoCompleteComboBoxAutomationPeer` flattens each group into its header
  (control type Header, name "Instance · 5") followed by its list items, because
  the stock `GroupItemAutomationPeer` does not expose a header.
- **Exact name first.** With typed text, `FilteredItems.Apply` moves items whose
  text equals the query (ignoring case) to the front, keeping source order
  otherwise; grouping then puts them first in their section. Such a reorder
  sends one reset.
- **Row.** Name (inherits the row foreground), a 13 px stroked source icon
  (`ProjectParameter`, `SharedParameter`, `FamilyParameter` in `Icons.xaml`,
  `SuccessBrush`, `PrimaryBrush`, `WarningBrush`; built-in has none; the icon
  takes the row foreground on the selected row). The categories show only in
  the tooltip (Categories list), not on the row. The core's `ParameterScopes` gives every item
  `CategoryNames` and `Models` once per scope; the item's UIA name is
  `SpokenName` ("Width, Shared, Type").
- **Tooltip.** An HC `ToolTipBaseStyle` tooltip (`ParameterTipStyle`) whose
  template draws `ParameterTip.Of(parameter)` (core): a title, a label/value
  grid with a shared label column (labels `TextBlockDefaultSecLight`), then
  Models and Categories or Families as bullets, five then "+ N more".
- **Chosen box.** An overlay that ignores hits sits on the editable text: a
  hidden copy of the text pushes the source icon to just after the name, and
  "Type" (`Filter.Rule.Parameter.Type`) docks right for a type parameter.

## Name validation

`FilterLibraryViewModel` derives from `ObservableValidator`. `EditName` has a
`[CustomValidation]` that calls `FilterNames.Validate` (trimmed, not empty,
unique ignoring case). The view shows the error with the stock HC error template
on an `hc:TextBox`; `NameError` and `CanSave` follow `ErrorsChanged`. Enter
commits, Esc cancels, losing focus commits a valid name or restores the old one.

## Other controls

- **Tree:** `FlatTreeList`, a `ListBox` with recycling virtualization and fixed
  28 px rows in a star-sized grid row. Expand inserts a range and collapse removes
  one, each with a single collection reset.
- **Tree keys and double-click:** `ExplorerTabView.OnRowsKeyDown` (PreviewKeyDown on
  `RowList`) runs Z / B / I / H through `ContextMenuViewModel.RunShortcutAsync`, so a key
  needs keyboard focus inside the list. In Revit that focus held only once the DevTools
  pane stopped re-showing itself on every log line ([Logging](../Logging/README.md)).
  `TreeRowContainerStyle` sets a `MouseDoubleClick` EventSetter; `OnRowDoubleClick`
  skips a double-click that starts in a button (chevron, eye, row tools) and calls
  `ExplorerTabViewModel.OpenRowCommand`: an instance row becomes the only selected row
  and `ContextMenuViewModel.SelectAndZoomAsync` runs Select then Zoom (even while sync
  hides *Select* in the menu); any other row toggles expand.
- **Small drop-downs:** `SmallComboBox`, `SmallComboBoxExtend` and `TagCheckComboBoxItem.Small` (`DevTools.UI/Theme/Styles/SmallItems.xaml`) give every item HC's small height (20) and font size 11, check boxes included. The editable value box uses `SmallComboBoxExtend` because only HC's Extend templates draw `InfoElement.Placeholder`. `SmallDropDownItemsAreSmall` and `ScopeDropDownItemsAreSmall` check height and every text size.
- **Scope boxes:** `TagCheckComboBox.Small` has `Padding` 10,0. The template pulls the tag row 4 px left (as HC's `CheckComboBox` does), so the first tag sits 6 px from the left edge, the same as above and below, with 2 px between tags. `ScopeTagsHaveEvenInsets` checks it.
- **Documents box:** `TagCheckComboBox` (tags, `+N` overflow, search, All). When
  not even one tag fits, `TagOverflowPanel` measures the first tag to the space
  left, so it shrinks with an ellipsis instead of leaving a bare `+N`. Its
  items expose Toggle and SelectionItem even while the drop-down is closed.
- **Scope and view switch:** `PanelButtonGroup`, a `ButtonGroup` whose automation
  peer exposes each radio button; the stock peer hides them from UIA.
- **Value drop-downs** load on open and use a recycling virtualized panel.

## Rule builder

The rule and group templates live in `FilterRuleTemplates.xaml` (an `x:Class`
dictionary merged by `FilterTabView`). One `RuleSetViewModel` template draws
the root and every nested group; the root hides the header, the scope row and
the border, because the Documents box and the Categories section are its scope.

- **Condition card:** `SecondaryRegionBrush` card, padding 5, a grip column on
  the left, the parameter row and the operator + value row.
- **Connector:** an AND / OR `ButtonDefault.Small` above every child but the
  first; a click runs `ToggleConnectorCommand`, which switches the whole group.
- **Group:** `PrimaryBrush` border, header (grip, "Group", collapse toggle,
  remove), scope row ("Search", documents and categories `TagCheckComboBox.Small`
  with the placeholders "Parent documents" and "Parent categories" when empty, set in `FilterRuleTemplates.xaml`), and a `BorderTipWarning` line when a scope
  value is outside the parent or unresolved. `CheckedItems.Path` keeps the
  boxes' selection and `ScopeOptionViewModel.IsChecked` in step.
- **Reorder:** the grip is a focusable `Thumb`. `DragReorder` (`DevTools.UI`,
  modeled on gong-wpf-dragdrop) keeps a `DragInfo` from the grip's
  `PreviewMouseLeftButtonDown`, starts `DoDragDrop` once the pointer passes
  `SystemParameters.MinimumHorizontalDragDistance` / `MinimumVerticalDragDistance`,
  and listens on each `FilterItemsControl` (transparent background, so gaps
  between cards are targets) through `PreviewDragEnter`, `PreviewDragOver` and
  `PreviewDrop`. Preview events matter: a `TextBox` inside a card handles the
  bubbling `DragOver` itself and refuses non-text data. The handler walks up
  from the element under the pointer to the nearest list with a drop target,
  so nested groups get their own drop. It builds a `DropInfo` (before or after
  from the pointer's half of the card), draws a `PrimaryBrush` insertion line in
  the adorner layer and scrolls near the `ScrollViewer` edges.
  `FilterDropHandler` asks `CanDropAt` (no drop into the node's own subtree, no
  drop that leaves it where it is) and calls `MoveTo`. An exception in a drag
  handler is swallowed by OLE and ends the whole drop silently; that is how a
  frozen `PrimaryBrush` pen once broke every drag, so the adorner pen is not
  frozen. Alt+Up / Alt+Down on a focused card run `MoveUp` / `MoveDown`.
- **Result row:** a fixed row under the rules `ScrollViewer`, so the count stays
  in view. One line: count and *matches* (Auto column), then a stock WPF
  `ToolBar` (`ResultTools`, `Filter.ResultTools`, HC `ToolBarBaseStyle`, grip
  hidden with `ToolBarTray.IsLocked`) holding mode (`OverflowMode="Never"`),
  the selection sync toggle (`Filter.SelectionSync`, `Never`), a separator (`Never`) and
  the result tools (`Filter.Result.Isolate`, `.Halftone`, `.Color`, `.Eye`,
  `AsNeeded`; the ToolBar overflows from the end). The sync toggle shares
  `SelectionSyncToggle` (`ModelContextResources.xaml`: stock
  `ToggleButtonPrimary`, `SelectionSync` icon 14 px, Name *Selection sync*)
  with Explorer, whose `SelectionSyncToggleStyle` only adds its visibility
  trigger. `ResultToolStyle` (based
  on `ToolBar.ButtonStyleKey`) shows the icon in the bar and icon plus
  `AutomationProperties.Name` in the overflow popup. UIA reaches an overflowed
  tool by toggling the template's `ButtonOverflow` and finding it in the popup.
  The tools run the same `ContextMenuViewModel` items as the menu:
  `FilterTabView` calls `OpenResultMenu` on hover of the result row, on focus
  and on click, then reads the item's `DisabledReason` and runs its command; an
  overflowed tool anchors the color picker on the ToolBar. Widths (100% DPI):
  mode 78, sync toggle 40, separator 5, each tool 18, overflow arrow 18. When the
  bar cannot hold the count, *matches* and those never-overflow items,
  `FilterTabView.FitResultLabel` (after each layout pass) collapses *matches*;
  the count's tooltip still reads "N matches". It uses the items' `DesiredSize`
  and the label's `FormattedText` width (a collapsed label measures 0, so
  measuring it would flip back each pass), then invalidates the ToolBar's
  `PART_ToolBarPanel` so it takes the freed width.
- **Children panel:** `FilterItemsControl` exposes its children straight to UI
  Automation; the stock `ItemsControl` peer wraps them in data items that
  can come back empty for items added later.
- **Selection sync pause:** the `Filter.SelectionSync` sync toggle never changes.
  `FilterRunner.IsSelectionSyncPaused` is derived state (sync on and the current result
  over `SelectionSyncPauseLimit`), so `Filter.Paused` (HandyControl
  `TextBlockDefaultWarning`, text *Paused: over 5,000 matches* from
  `FilterRunner.SelectionSyncPausedText`) shows only while that holds and goes away with
  any result at or under the limit, an empty or missing filter, or sync off.

### Drag feel (design)

- **Preview:** on drag start a `DragAdorner` on the topmost adorner layer paints
  the dragged card with a `VisualBrush` at 0.7 opacity, offset by the grab
  point, and the card itself dims to 0.35. The source's `QueryContinueDrag`
  moves it to the cursor (`GetCursorPos`, `PointFromScreen`), so it follows the
  pointer over the whole pane, not only over drop lists. Default OLE cursors stay.
- **Target:** one function, `DragReorder.DropAtPoint(host, drag, point)`, hit
  tests the point, takes the innermost rules list under it (or the empty-group
  placeholder through `DragReorder.DropList`), the card under it, and the zone.
  A group's header, Search row and add buttons belong to the group's card in the
  parent list, so only the rules area of a group drops into it.
- **Hysteresis:** `DropZones.Position` takes the pointer's place in the card. On
  a new card the midpoint decides; on the same card the side flips only past
  25% or 75% of its height. The last card and side live on the `DragInfo`.
- **Line:** drawn in the target list at the card's top or bottom edge (the gap,
  since connectors belong to the next card), across the list's width, so the
  indent follows the target level. No line when `CanDropAt` refuses (no-op or
  own subtree).
- **Scroll:** near the `ScrollViewer` edges the view scrolls, as before.
### How drag is tested

- `FilterDropTests` (Views tests, no mouse) builds a `DropInfo` per case and
  calls `FilterDropHandler.DragOver` and `Drop` on the view models: up, down,
  onto itself (no move), into a nested group, out of a group, across nested
  groups, a group into its own descendant (refused), and the before or after
  half of a card (`DropInfo.InsertPosition`).
- Keyboard reorder stays a UIA test (`AltUpDownReorders`).
- Zones, hysteresis, nested groups, the empty-group drop and the preview
  lifetime are Views tests too (`ZoneFollowsPointerHalf`, `SideFlipsOnlyPastBand`,
  `PointerPicksCardSide`, `SmallMoveBackKeepsLine`, `NoLineForNoOpSpot`,
  `GroupHeaderDropsBeforeGroup`, `RulesAreaDropsIntoGroup`, `EmptyGroupTakesDrop`,
  `PreviewFollowsDragLifetime`). They call `DragReorder.DropAtPoint` on the laid
  out panel; no pointer moves.
- `DevTools.ModelsContext.Sandbox --drag-shots <folder>` runs the same steps in
  process (preview, line, drop) and saves `before.png`, `during.png` and
  `after.png` of the sandbox window only. In Revit the same steps run in process
  through `execute_csharp_code`; Revit is never driven with the mouse.
- `--drag <folder>` is an opt-in a person runs on their own desktop: it moves the
  real pointer with FlaUI `Mouse`, brings the sandbox to the front, checks with
  `WindowFromPoint` that the sandbox owns the start and end points, and stops
  with one sentence if another window is under the pointer or takes focus. No
  test run or agent runs it.

## AutomationIds

Icon-only buttons also set `AutomationProperties.Name` to their tooltip text (the
row eye binds it to its own tooltip). The panel has one message line,
`ModelContext.Message`: Explorer refusals (row tools, eyes, the Categories box)
report into `ModelContextViewModel.Message`, so only one line ever shows.

`CategorySection` (both tabs) is an Extended ListBox whose selection is the
check: `CategoryRowStyle` (based on stock `ListBoxItemBaseStyle`, rows
`<Area>.Category.Row`) binds `IsSelected` to `IsChecked` two-way and keeps a
selected row unpainted (stock hover and focus). The checkbox has no content, so
a click on the name reaches the ListBox. Right-click is handled before the
ListBox so it keeps the checks; Space toggles the focused row. Virtualization is
off (about 150 rows), so every row's selection stays bound.
Subcategories stay in the same ListBox: `CategoryPickerViewModel` orders
`Items` parent-then-children (`GroupCount.ParentId`) and keeps `VisibleItems`
as one `ObservableCollection` it edits in place (roots, plus the children of
expanded roots or the search hits). Replacing the list would regenerate every
container while the Selector still holds the old selection, and the two-way
`IsSelected` binding would push stale checks back (`CheckDuringExpandHolds`). The
row's chevron (`<Area>.Category.Expand`, a stock `ToggleButtonCustom`) binds
`IsExpanded`; the checkbox binds the three-state `BoxState` (own check plus the
children's). Parent drives children: setting `BoxState` checks or clears the
parent and its children at once (a click on the partial box arrives as `false`
from the stock toggle and the setter turns it into `true`), and a child change
makes the parent follow (all checked, checked; none, clear). Row selection stays
bound to the row's own `IsChecked`, but a row change is batched per dispatcher
frame (`CategoryPickerViewModel.SettleRows`) because one Selector gesture
changes several rows: a single click on an already checked child unselects its
parent and siblings but not the child, so the parent cascade must not run on
the kept child. The batch rule: a parent turned on checks its children; a parent
turned off clears them unless a sibling changed in the same gesture; a parent
left on whose children were all cleared in a multi-row gesture keeps them
(re-selecting the parent); otherwise the parent follows its children. Search
opens parents only while it lasts (`_closedInSearch` keeps a parent the user
closed during the search). `HideUnchecked` (stock CheckBox
`<Area>.CategoryHideUnchecked`, per picker, not saved) narrows `VisibleItems`
to checked rows and roots with a checked child, and every check change rebuilds it
in place; it sits outside the `WhenLocked` parts, so a read-only filter can
still use it. Children indent 16 px. A parent missing from
the scope comes from `CategoryTree.WithParents` (count 0, name from a loaded
snapshot). In the Explorer tree, `ExplorerTree.GroupRows` puts a subgroup row in
its parent group row's `Subgroups`; they lead the parent's children, the
parent's count, hidden, selected and outside counts sum its own and its listed
subgroups, and its color mark reads its own elements only.

Every interactive control has an `AutomationProperties.AutomationId`, named
`<Area>.<Part>`: `ModelContext.Scope.ActiveView`, `ModelContext.Documents` (each
item `ModelContext.Document`, its model title in HelpText),
`Explorer.Row.Eye`, `Explorer.Row.SavedHide` (orange dot: a linked row the panel
hid with Hide in View), `Explorer.Row.HiddenCount` (plain *+N hidden* text, `WarningBrush`
on an unselected row and the item foreground on a selected one, so it never sits orange
on the primary selection), `Explorer.Row.Text` (the row name). The count and *+N hidden*
share one slot (`Counts`) with the row tools: on hover or selection the tools show and
`Counts` collapses (header rows, which have no tools, keep it), so the tools never add
width and the `*` name column keeps room for its first characters. The same selected trigger
sets `RowGrid` back to opacity 1, after the dimming triggers (`IsDimmed`, Hidden eye,
hidden by category), so a selected hidden row is not pale on the selection, `Explorer.SelectionSync`, `Filter.Source`, `Filter.SelectionSync`,
`Filter.Paused`,
`Filter.Rule.Parameter`, `Filter.Rule.CaseSensitive`, `Temporary.Reset`, and so
on. The rule builder uses `Filter.Rules`, `Filter.NoRules`, `Filter.Rule.Add`
(+ Condition), `Filter.Set.Add` (+ Group), `Filter.Rule.Parameter`,
`Filter.Rule.CaseSensitive`, `Filter.Rule.Operator`, `Filter.Rule.Value`,
`Filter.Rule.Parameter.Section` (header), `Filter.Rule.Parameter.Type`,
`Filter.Rule.Parameter.Source` (icon), `Filter.Rule.Remove`, `Filter.Rule.Unsupported`,
`Filter.Rule.Skipped` (*Skipped · no value*), `Filter.Rule.MoreValues`,
`Filter.Item.Grip`, `Filter.Set.Connector`, `Filter.Set.Collapse`,
`Filter.Set.Remove`, `Filter.Set.Scope.Documents`, `Filter.Set.Scope.Categories`
and `Filter.Set.Warning`. View tests and live checks find controls only by these ids and act through
UIA patterns (Toggle, Value, SelectionItem, Invoke, ItemContainer), never by
screen position. Live L3 selects combo items through ItemContainer with the
drop-down closed ([test-matrix.md](test-matrix.md#rules-for-adding-a-case), rule 5). Renaming an id breaks tests on purpose; update them together.

The view root `ModelContext.Panel` reports the panel's activity as UIA
`ItemStatus`: `Idle <n>` or `Busy <n>`. It is Busy while a panel job runs or a
debouncer waits, and `<n>` grows when a job ends. A test settles on Idle, acts,
then waits for Idle with a larger `<n>` before it reads a count
([test-matrix.md](test-matrix.md#readiness-signal)).

List and combo items carry the text the user sees as their UIA name: item view
models and `ValueCount` return it from `ToString` (source name, category name,
document label, value), and operator items set `AutomationProperties.Name` to
the operator's description ("does not equal").

The custom filter name box ignores focus that leaves the pane (Revit or another
app becoming active), so an external UIA client can set its Value; it commits
on Enter or when focus moves to another panel control.

## XAML in a merged assembly

The add-in ILRepacks `DevTools.Presentation` into the host assembly and leaves
only `DevTools.UI` loose (0037). A `;component/` URI to the merged assembly
itself would break after merging, so panel resources are an `x:Class`
`ResourceDictionary` merged by instance, and only `/DevTools.UI;component/...`
URIs are allowed. `XamlGuardTests.PresentationXamlUsesNoSelfUris` enforces it.

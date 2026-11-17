# Designing WPF for Automation

A control is automatable when a tool can find it by a stable id and change it
through a UIA pattern. Build that in while writing the XAML; retrofitting it
after a flaky test is slower.

## Contents

- [AutomationIds](#automationids)
- [Which control exposes which pattern](#which-control-exposes-which-pattern)
- [Names and help text](#names-and-help-text)
- [Custom AutomationPeers](#custom-automationpeers)
- [Items, templates, and virtualization](#items-templates-and-virtualization)
- [Popups, menus, tooltips](#popups-menus-tooltips)
- [ComboBox: editable or not](#combobox-editable-or-not)
- [HandyControl quirks](#handycontrol-quirks)
- [Check your work](#check-your-work)

## AutomationIds

- Put `AutomationProperties.AutomationId` on every control a user can act on
  and every element a test reads (counts, messages, empty hints).
- Name them `<Area>.<Part>` (`Filter.Rule.Add`, `Explorer.Row.Eye`). Ids are
  not localized and do not change when the label text does, which is why tests
  use them instead of Name.
- Inside an item template, the same id repeats on each row. That is fine: find
  the row first (by Name or index), then the id inside it.
- A control that builds its own parts (tags, overflow chips, clear buttons)
  should derive their ids from its own id (`Docs.Tag`, `Docs.More`) so a test
  can scope them.
- Do not bind AutomationId to data. Bind `AutomationProperties.Name` instead
  when the row needs a readable name.

## Which control exposes which pattern

| Control | ControlType | Patterns to use |
|---------|-------------|-----------------|
| `Button` | Button | Invoke |
| `ToggleButton`, `CheckBox` | Button / CheckBox | Toggle |
| `RadioButton` | RadioButton | SelectionItem |
| `TextBox` | Edit | Value (and Text) |
| `ComboBox` (not editable) | ComboBox | ExpandCollapse, Selection; items are ListItem with SelectionItem |
| `ComboBox IsEditable="True"` | ComboBox | adds Value |
| `ListBox`, `ListView` | List | Selection, Scroll, ItemContainer; items SelectionItem, ScrollItem |
| `TreeView` | Tree | items TreeItem with ExpandCollapse, SelectionItem |
| `Expander` | Group | ExpandCollapse |
| `TabControl` | Tab | items TabItem with SelectionItem |
| `Slider`, `ProgressBar` | Slider / ProgressBar | RangeValue |
| `ScrollViewer` | Pane | Scroll |
| `MenuItem` | MenuItem | Invoke, or ExpandCollapse for submenus; Toggle when `IsCheckable` |

A plain `Border`, `Grid`, or `TextBlock` with a mouse handler exposes no action
pattern. Turn it into a `Button` (restyled) instead of adding an
`AutomationPeer` for a click handler; the button also gets keyboard and focus
for free.

## Names and help text

- `AutomationProperties.Name` is what a screen reader says and what `Name`
  queries match. Icon-only buttons need one ("Remove Arch.rvt").
- `AutomationProperties.HelpText` carries tooltip-like detail that a test can
  assert without opening the tooltip.
- A `TextBlock` shows up as a Text element with its text as Name, which is how
  tests read counts and messages. Give it an id when a test reads it.

## Custom AutomationPeers

Write a peer when the stock one hides a pattern or children. Typical cases:

- A container whose stock peer reports no children (a `ButtonGroup` that hides
  its radio buttons). Return a plain `FrameworkElementAutomationPeer` so the
  children surface.
- A custom items control whose items must expose Toggle or SelectionItem even
  while the drop-down is closed: derive from `ListBoxAutomationPeer` and the
  item peer, implement `IToggleProvider`/`ISelectionItemProvider`, and raise
  property-changed events when state changes.
- A control that acts like a button but derives from something else: implement
  `IInvokeProvider` on its peer.

```csharp
protected override AutomationPeer OnCreateAutomationPeer() => new MyPeer(this);

private sealed class MyPeer(MyControl owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
    public override object GetPattern(PatternInterface p) => p == PatternInterface.Invoke ? this : base.GetPattern(p);
    public void Invoke() => ((MyControl)Owner).Activate();
}
```

Keep the peer's behavior identical to what a user click does; a test that
passes through a shortcut the user cannot take proves nothing. Examples in this
repo: `PanelButtonGroup`, `ColorEyeDropperAutomationPeer`, `TagCheckComboBox`.

## Items, templates, and virtualization

- With UI virtualization only realized rows exist in the UIA tree. A query for
  row 500 finds nothing until it is scrolled into view. Use the list's
  ItemContainer pattern (`FindItemByProperty`) and `VirtualizedItem.Realize()`,
  or `ScrollItem.ScrollIntoView()` on a realized neighbor. The script's
  `items -Realize` does this.
- With recycling, a realized container is reused for another item. Read the
  row, act on it, and re-find it after scrolling; never hold an element across
  a scroll.
- Rows that are realized but scrolled out of view report `IsOffscreen=True`.
  That is a state, not an error; assert on it only when visibility is the point.
- Give the row a meaningful Name (override `ToString` on the item, or bind
  `AutomationProperties.Name` in the container style) so tests can find a row
  by its text.

## Popups, menus, tooltips

- A WPF `Popup`, `ContextMenu`, drop-down, or tooltip is a separate HWND. In UIA
  it is a top-level window of the same process (class `Popup`), not a child of
  the control. Search the process's windows, not just the main window.
- In an in-process test, get the popup's HWND from
  `PresentationSource.FromVisual(popup.Child)` and search from there.
- A popup closes when it loses focus or activation. Opening it with a pattern
  (ExpandCollapse) and acting through patterns avoids moving focus away.
- `StaysOpen="False"` popups can close between two queries if something else
  takes focus. Keep the test window from activating other windows
  (`ShowActivated="False"` on helper windows).

## ComboBox: editable or not

- A non-editable `ComboBox` has no Value pattern. Its current choice is the
  Selection pattern's selected item. Items appear as children only after the
  drop-down has generated containers, so expand first, then select the item by
  SelectionItem, then collapse.
- An editable `ComboBox` adds Value: a test can `SetValue("Mark")` to type and
  filter, then pick a list item. Choose editable when the user types to search;
  it is more automatable than a TextBox with a hand-made popup.
- Setting Value on an editable box does not commit a selection by itself.
  Assert the bound view-model value, or select the item explicitly.

## HandyControl quirks

- HandyControl styles keep the stock WPF control types, so stock patterns
  apply. A style alone never changes what UIA sees; a template that drops the
  `ContentPresenter` or wraps content can hide children.
- `ButtonGroup` hides its children from UIA; expose them with a peer as above.
- HC `Label` tags and `Tag` chips are Text or Custom elements without actions;
  give the remove button an id and Name.
- `hc:TextBox` with the info/error template: the error text is a separate
  element; give it an id if a test reads it.
- Follow the stock look rules in `docs/architecture/<Module>/ui.md`: behavior
  changes keep HC styles. Do not restyle a control to make it testable; add an
  id or a peer.

## Check your work

Run the app and dump it:

```powershell
pwsh -File .agents/skills/ui-automation/scripts/uia.ps1 tree -ProcessId <pid> -Root <AreaRootId> -Depth 5
```

Every interactive line should have an `id=` and the pattern you expect. A
`Custom` element with only `LegacyIAccessible,SynchronizedInput` where you
expected a Button means the action is unreachable.

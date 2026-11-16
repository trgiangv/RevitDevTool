# UI Conventions

The views live in `source/DevTools.Presentation/Views/ModelsContext` and use
shared controls from `source/DevTools.UI/Controls`. Policy: 0062 §12 and §15.

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
- **Own collection view.** When `ItemsSource` is a plain list, the control wraps
  it in a new `ListCollectionView`. Every rule binds the same `Parameters` list;
  with the default view, typing in one rule would filter the picker of every
  other rule.
- **`MaxSuggestionCount`** (100): the drop-down opens on its own only when 100
  or fewer items match, so typing one letter into a list of 2,000 parameters
  does not render a huge popup. Ctrl+Space opens it any time.
- **Virtualized** `VirtualizingStackPanel` items panel and a fixed
  `MaxDropDownHeight`.
- **150 ms** debounce on typing (`AutoCompleteComboBoxSetting.Delay`).
- The validation error template is hidden while the drop-down is open and put
  back on close, so the red box does not draw over the list.

Each picker item is a name plus HC `Label` tags (origin, binding, data type,
coverage). The tag text comes from the enum `[Description]` through
`EnumDescriptionConverter`.

## Name validation

`FilterTabViewModel` derives from `ObservableValidator`. `EditName` has a
`[CustomValidation]` that calls `FilterNames.Validate` (trimmed, not empty,
unique ignoring case). The view shows the error with the stock HC error template
on an `hc:TextBox`; `NameError` and `CanSave` follow `ErrorsChanged`. Enter
commits, Esc cancels, losing focus commits a valid name or restores the old one.

## Other controls

- **Tree:** `FlatTreeList`, a `ListBox` with recycling virtualization and fixed
  28 px rows in a star-sized grid row. Expand inserts a range and collapse removes
  one, each with a single collection reset.
- **Documents box:** `TagCheckComboBox` (tags, `+N` overflow, search, All). Its
  items expose Toggle and SelectionItem even while the drop-down is closed.
- **Scope and view switch:** `PanelButtonGroup`, a `ButtonGroup` whose automation
  peer exposes each radio button; the stock peer hides them from UIA.
- **Value drop-downs** load on open and use a recycling virtualized panel.

## AutomationIds

Every interactive control has an `AutomationProperties.AutomationId`, named
`<Area>.<Part>`: `ModelContext.Scope.ActiveView`, `ModelContext.Documents`,
`Explorer.Row.Eye`, `Explorer.SelectionSync`, `Filter.Source`, `Filter.Live`,
`Filter.Rule.Parameter`, `Filter.Rule.CaseSensitive`, `Temporary.Reset`, and so
on. View tests and live checks find controls only by these ids and act through
UIA patterns (Toggle, Value, SelectionItem, Invoke, ExpandCollapse), never by
screen position. Renaming an id breaks tests on purpose; update them together.

## XAML in a merged assembly

The add-in ILRepacks `DevTools.Presentation` into the host assembly and leaves
only `DevTools.UI` loose (0037). A `;component/` URI to the merged assembly
itself would break after merging, so panel resources are an `x:Class`
`ResourceDictionary` merged by instance, and only `/DevTools.UI;component/...`
URIs are allowed. `XamlGuardTests.PresentationXamlUsesNoSelfUris` enforces it.

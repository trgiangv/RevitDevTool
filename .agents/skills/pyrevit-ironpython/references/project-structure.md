# pyRevit Project Structure

## Extension Layout

```
MyExtension.extension/
  extension.json               # metadata: name, author, description
  bundle.yaml                  # layout: [TabName]
  lib/                         # SHARED lib — on sys.path for all bundles
    MyLib/
      __init__.py
      ...
  TabName.tab/
    bundle.yaml                # layout: [Panel1, Panel2, ...]
    Panel1.panel/
      bundle.yaml              # layout: [Stack1, ToolA, ...]
      Stack1.stack/
        bundle.yaml            # layout: [ToolB, ToolC]
        ToolB.pushbutton/
          bundle.yaml
          script.py
        Dropdown.pulldown/
          ToolC.pushbutton/
            bundle.yaml
            script.py
      ToolA.pushbutton/
        bundle.yaml
        script.py
```

## Bundle Types

| Suffix | Purpose |
|--------|---------|
| `.extension` | Root container — holds tabs, lib, config |
| `.tab` | Ribbon tab |
| `.panel` | Panel within a tab |
| `.stack` | Vertical stack of 2-3 buttons |
| `.pulldown` | Dropdown menu |
| `.splitbutton` | Split button with default + dropdown |
| `.pushbutton` | Single clickable button (IronPython script) |
| `.invokebutton` | Button that invokes a .NET DLL command |

## extension.json

```json
{
    "builtin": "True",
    "default_enabled": "True",
    "type": "extension",
    "rocket_mode_compatible": "True",
    "name": "MyExtension",
    "description": "Tool description",
    "author": "TeamName",
    "templates": {
        "author": "TeamName",
        "docpath": "https://docs.example.com/"
    },
    "dependencies": []
}
```

`templates.docpath` enables `{{docpath}}` placeholder in `bundle.yaml` `help_url` fields.

## bundle.yaml

### Layout bundles (tab, panel, stack)

```yaml
layout:
  - ChildName1
  - ChildName2
```

Names must match child folder names without the suffix (e.g. `Panel1` matches `Panel1.panel/`).

### Pushbutton bundle

```yaml
title: Tool Display Name
context: doc-project
tooltip:
  en_us: |
    Version = 1.0
    Description: What this tool does.
author: "Author.Name"
engine:
  clean: true
help_url: '{{docpath}}path/to/docs'
```

**`context` values:**
- `doc-project` — requires an open project document
- `zero-doc` — works without any document open
- `selection` — requires selected elements

**`engine.clean`** — controls IronPython engine lifecycle:
- `true` (development) — fresh engine per run, modules reload on code changes
- `false` (release) — cached engine, faster startup; code must not store Revit objects at module level

### Invokebutton bundle (for .NET DLL commands)

```yaml
title: Tool Name
tooltip: Description
assembly: MyAssembly.dll
command_class: MyCommandClassName
author: "Author.Name"
```

## Shared vs Local lib

### Extension-level `lib/` (shared)

- Path: `MyExtension.extension/lib/`
- pyRevit adds this to `sys.path` automatically
- Use for: MVVM base classes, common utilities, Revit helpers, custom WPF controls
- Import: `from MyLib.WPF import ObservableBase`

### Pushbutton-level `lib/` (local)

- Path: `ToolName.pushbutton/lib/`
- Scoped to that pushbutton only
- Use for: tool-specific models, services, constants, views
- Import: `from lib.services.copy_service import CopyService`

### When to use which

| Scenario | Location |
|----------|----------|
| MVVM base classes (ObservableBase, RelayCommand, WPFWindow) | Extension `lib/` |
| Revit utility functions used by multiple tools | Extension `lib/` |
| Tool-specific business logic | Pushbutton `lib/` |
| Tool-specific models and constants | Pushbutton `lib/` |
| Tool-specific ViewModels and Views | Pushbutton `lib/` |

## Complex Tool Layout

For non-trivial tools, organize the pushbutton `lib/` into focused packages:

```
ToolName.pushbutton/
  bundle.yaml
  script.py                    # entry point — short, orchestrates flow
  lib/
    __init__.py
    constants.py               # named constants, option maps, enums
    collector.py               # element queries, validation
    models/
      __init__.py
      result.py                # data-only classes
      item.py                  # WPF-bindable items (ObservableBase)
    services/
      __init__.py
      mapping_service.py       # domain logic
      copy_service.py          # Revit API mutation
    viewmodels/
      __init__.py
      main_viewmodel.py        # dialog ViewModel
    views/
      __init__.py
      main_view.py             # WPFWindow subclass
      main_view.xaml           # XAML layout
    utils/
      __init__.py
      helpers.py               # pure functions
```

## Entry Point Pattern

```python
# script.py — keep it short
from lib.collector import collect_inputs
from lib.services.main_service import execute
from lib.viewmodels import MainViewModel
from lib.views import MainView

def main():
    # type: () -> None
    inputs = collect_inputs()
    if inputs is None:
        return

    view_model = MainViewModel(inputs)
    window = MainView(view_model)
    window.show_dialog()

    if not view_model.confirmed:
        return

    try:
        result = execute(view_model.settings)
        # show success notification
    except Exception as error:
        # show error notification
        pass

if __name__ == "__main__":
    main()
```

# Revit Python Cheatsheet

## Required Pattern

Always include explicit imports and wrap code in `def run():`. Do **not** create global variables or read document at module level.

```python
from Autodesk.Revit import DB, UI
from RevitDevTool.Core import RevitContext

def run():
    doc = RevitContext.ActiveDocument
    if doc is None:
        print("No active document")
        return
    print(f"Document: {doc.Title}")

run()
```

## Required Imports

CLR references are pre-loaded by the host setup script. You must explicitly import namespaces:

```python
from Autodesk.Revit import DB, UI
from RevitDevTool.Core import RevitContext
```

| Import | Provides |
|--------|----------|
| `DB` | `Autodesk.Revit.DB` — geometry, elements, parameters, transactions |
| `UI` | `Autodesk.Revit.UI` — UIApplication, Selection, TaskDialog |
| `RevitContext` | `.ActiveDocument`, `.ActiveUiDocument`, `.ActiveView`, `.UiApplication` |

Import specific types inside `run()` when needed:

```python
from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory
```

## PEP 723 Dependencies

Add a script metadata header for external packages (installed automatically before execution):

```python
# /// script
# dependencies = ["pandas>=2.0", "numpy"]
# ///

def run():
    import pandas as pd
    doc = RevitContext.ActiveDocument
    print(pd.__version__)

run()
```

## Transaction Pattern

Modifications require an explicit transaction. Always read `doc` inside `run()`:

```python
from Autodesk.Revit import DB
from RevitDevTool.Core import RevitContext

def run():
    doc = RevitContext.ActiveDocument
    if doc is None:
        print("No active document")
        return

    t = DB.Transaction(doc, "Update comments")
    t.Start()
    try:
        wall = doc.GetElement(DB.ElementId(12345))
        param = wall.get_Parameter(DB.BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)
        if param and not param.IsReadOnly:
            param.Set("updated")
        t.Commit()
        print("Committed")
    except Exception as ex:
        if t.HasStarted() and not t.HasEnded():
            t.RollBack()
        print(f"Rolled back: {ex}")

run()
```

## FilteredElementCollector

```python
from Autodesk.Revit import DB
from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory
from RevitDevTool.Core import RevitContext

def run():
    doc = RevitContext.ActiveDocument
    if doc is None:
        return

    walls = list(
        FilteredElementCollector(doc)
        .OfClass(DB.Wall)
        .ToElements()
    )
    doors = list(
        FilteredElementCollector(doc)
        .OfCategory(BuiltInCategory.OST_Doors)
        .WhereElementIsNotElementType()
        .ToElements()
    )
    print(f"Walls: {len(walls)}, Doors: {len(doors)}")

run()
```

## Common API Calls (Python.NET)

### Units (internal = feet)
```python
from Autodesk.Revit import DB
internal = DB.UnitUtils.ConvertToInternalUnits(3000.0, DB.UnitTypeId.Millimeters)
```

### Get type from instance
```python
wall_type = doc.GetElement(wall.GetTypeId())
```

### Create a wall
```python
line = DB.Line.CreateBound(DB.XYZ(0, 0, 0), DB.XYZ(10, 0, 0))
wall = DB.Wall.Create(doc, line, wall_type_id, level_id, height, 0, False, False)
```

### Selection (requires UI document)
```python
from RevitDevTool.Core import RevitContext
ui_doc = RevitContext.ActiveUiDocument
if ui_doc:
    refs = ui_doc.Selection.PickObjects(UI.Selection.ObjectType.Element, "Pick elements")
```

## Package Lifecycle (PEP 723)

- Packages declared in `# /// script` header are auto-installed on first use.
- First install: 5–30s (network download). Subsequent imports: instant (cached in memory).
- **Limitation**: Once a package is imported, its version is fixed for the entire host session. You cannot upgrade an already-imported package without restarting the host process.
- **Limitation**: If multiple host instances run simultaneously, concurrent package installs may conflict. Pin exact versions to avoid ambiguity.
- **Tip**: Use `pandas==2.2.0` (exact pin) rather than `pandas>=2.0` for reproducible behavior.

## WPF UI (Custom Dialogs)

Only use when creating custom windows/dialogs.

### Namespace Conflicts (Critical)

These WPF types shadow Revit API types — **never import both at module top level**:

| Conflict | WPF | Revit |
|----------|-----|-------|
| `Grid` | `System.Windows.Controls.Grid` | `DB.Grid` |
| `ComboBox` | `System.Windows.Controls.ComboBox` | `UI.ComboBox` |
| `TextBox` | `System.Windows.Controls.TextBox` | `UI.TextBox` |
| `Color` | `System.Windows.Media.Color` | `DB.Color` |
| `Line` | `System.Windows.Shapes.Line` | `DB.Line` |
| `Element` | `System.Windows.FrameworkElement` | `DB.Element` |
| `Transform` | `System.Windows.Media.Transform` | `DB.Transform` |

### Rules:
1. Import WPF via module alias: `import System.Windows.Controls as Controls`
2. Access WPF types qualified: `Controls.Grid()`, `Controls.Button()`
3. Keep host API imports with short names: `DB.Grid`, `DB.Line`
4. **Never** `from System.Windows.Controls import *`

### Minimal Window Template

```python
import clr
clr.AddReference('PresentationFramework')
clr.AddReference('PresentationCore')
clr.AddReference('WindowsBase')

import System.Windows as Wpf
import System.Windows.Controls as Controls
from Autodesk.Revit import DB
from RevitDevTool.Core import RevitContext

def run():
    doc = RevitContext.ActiveDocument
    if doc is None:
        print("No active document")
        return

    window = Wpf.Window()
    window.Title = "My Tool"
    window.Width = 400
    window.Height = 300
    window.WindowStartupLocation = Wpf.WindowStartupLocation.CenterScreen

    stack = Controls.StackPanel()
    stack.Margin = Wpf.Thickness(10)

    label = Controls.TextBlock()
    label.Text = f"Document: {doc.Title}"
    stack.Children.Add(label)

    btn = Controls.Button()
    btn.Content = "OK"
    btn.Margin = Wpf.Thickness(0, 10, 0, 0)
    btn.Click += lambda s, e: window.Close()
    stack.Children.Add(btn)

    window.Content = stack
    window.ShowDialog()
    print("Dialog closed")

run()
```

### Threading & Dialog Safety

**`ShowDialog()` blocks the MCP execution thread.** If no user closes the window, the host freezes.

**Never use `Show()`** — returns immediately, agent misses results, risks host crash on GC.

**Every dialog MUST have a self-destruct timeout** — no exceptions:

```python
import System.Windows.Threading as Threading
from System import TimeSpan

# MANDATORY: always add auto-close timer to ANY dialog
timer = Threading.DispatcherTimer()
timer.Interval = TimeSpan.FromSeconds(30)  # 30s for forms, 10s for info
def on_tick(s, e):
    timer.Stop()
    window.Close()
timer.Tick += on_tick
timer.Start()
window.ShowDialog()  # guaranteed to close even if user is absent
```

| Intent | Timeout |
|--------|---------|
| Input form (user picks/types) | 30s |
| Informational display (stats, results) | 10s |
| No UI needed (autonomous) | Skip dialog, use `print()` |

**Destructive operations**: Do NOT create confirmation dialogs in host. Agent handles user confirmation in chat before sending code. Host-side safety = transaction + undo (`navigate_history`).

## Rules

1. **Always** include explicit imports (`from Autodesk.Revit import DB, UI`).
2. **Always** use `def run(): ... run()` — never execute host API at module level.
3. **Never** assign to module-level globals (`doc = ...` outside a function).
4. Read `RevitContext.ActiveDocument` **inside** `run()`, not at import time.
5. Use `print()` for output — stdout/stderr are captured and returned.
6. Read `revit://model/context` for current model state before querying.

# AutoCAD Python Cheatsheet

## Required Pattern

Always include explicit imports and wrap code in `def run():`. Do **not** create global variables or access the database at module level.

```python
from Autodesk.AutoCAD.ApplicationServices.Core import Application
from Autodesk.AutoCAD.DatabaseServices import Transaction, OpenMode

def run():
    doc = Application.DocumentManager.MdiActiveDocument
    if doc is None:
        print("No active document")
        return
    print(f"Document: {doc.Name}")

run()
```

## Required Imports

CLR references are pre-loaded by the host setup script. You must explicitly import namespaces:

```python
from Autodesk.AutoCAD.ApplicationServices.Core import Application
from Autodesk.AutoCAD.DatabaseServices import Transaction, OpenMode, BlockTableRecord
from Autodesk.AutoCAD.Geometry import Point3d, Vector3d
```

| Import | Provides |
|--------|----------|
| `Application` | Document manager, active document |
| `DatabaseServices` | Transaction, OpenMode, Entity types, BlockTable |
| `Geometry` | Point3d, Vector3d, Matrix3d |

## PEP 723 Dependencies

Add a script metadata header for external packages (installed automatically before execution):

```python
# /// script
# dependencies = ["pandas>=2.0"]
# ///

def run():
    import pandas as pd
    print(pd.__version__)

run()
```

## Database / Transaction Pattern

Always acquire the active document inside `run()`, lock the document, and use a transaction for modifications:

```python
def run():
    from Autodesk.AutoCAD.ApplicationServices.Core import Application
    from Autodesk.AutoCAD.DatabaseServices import Transaction, OpenMode, BlockTable, BlockTableRecord

    doc = Application.DocumentManager.MdiActiveDocument
    if doc is None:
        print("No active document")
        return

    db = doc.Database
    with doc.LockDocument():
        tr = db.TransactionManager.StartTransaction()
        try:
            bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead)
            btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite)
            # create or modify entities here
            tr.Commit()
            print("Committed")
        except Exception as ex:
            print(f"Aborted: {ex}")

run()
```

**Critical**: `doc.LockDocument()` is required because MCP execution runs on a background thread. Without it, you get "The calling thread cannot access this object because a different thread owns it".

### Open ModelSpace for writing
```python
bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead)
btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite)
btr.AppendEntity(entity)
tr.AddNewlyCreatedDBObject(entity, True)
```

### Query entities in ModelSpace
```python
bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead)
btr = tr.GetObject(bt[DB.BlockTableRecord.ModelSpace], OpenMode.ForRead)
for obj_id in btr:
    entity = tr.GetObject(obj_id, OpenMode.ForRead)
    print(entity.GetType().Name)
```

### Editor output
```python
doc.Editor.WriteMessage("\nResult text")
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

These WPF types shadow AutoCAD API types — **never import both at module top level**:

| Conflict | WPF | AutoCAD |
|----------|-----|---------|
| `Color` | `System.Windows.Media.Color` | `Autodesk.AutoCAD.Colors.Color` |
| `Line` | `System.Windows.Shapes.Line` | `DatabaseServices.Line` |
| `Point` | `System.Windows.Point` | `Geometry.Point3d` |
| `Application` | `System.Windows.Application` | `ApplicationServices.Application` |

### Rules:
1. Import WPF via module alias: `import System.Windows.Controls as Controls`
2. Access WPF types qualified: `Controls.Grid()`, `Controls.Button()`
3. Keep host API imports explicit: `from Autodesk.AutoCAD.DatabaseServices import Line`
4. **Never** `from System.Windows.Controls import *`

### Minimal Window Template

```python
import clr
clr.AddReference('PresentationFramework')
clr.AddReference('PresentationCore')
clr.AddReference('WindowsBase')

import System.Windows as Wpf
import System.Windows.Controls as Controls
from Autodesk.AutoCAD.ApplicationServices.Core import Application

def run():
    doc = Application.DocumentManager.MdiActiveDocument
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
    label.Text = f"Drawing: {doc.Name}"
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

1. **Always** include explicit imports (`from Autodesk.AutoCAD.DatabaseServices import ...`).
2. **Always** use `def run(): ... run()` — never execute host API at module level.
3. **Never** assign to module-level globals (`doc = ...` outside a function).
4. Acquire `Application.DocumentManager.MdiActiveDocument` **inside** `run()`.
5. Use `print()` for output — stdout/stderr are captured and returned.
6. Wrap all database reads/writes in `db.TransactionManager.StartTransaction()`.

# AutoCAD API Cheat Sheet

## Required Pattern

```csharp
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

public class Command
{
    [CommandMethod("MYCOMMAND", CommandFlags.Session)]
    public void Execute()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        var db = doc.Database;
        var ed = doc.Editor;

        using (var docLock = doc.LockDocument())
        using (var tr = db.TransactionManager.StartTransaction())
        {
            // Your code here
            tr.Commit();
        }

        ed.WriteMessage("\nResult text");
    }
}
```

**Critical**: `CommandFlags.Session` and `doc.LockDocument()` are required because MCP execution runs on a background thread. Without them, you get "The calling thread cannot access this object because a different thread owns it".
```

## Transaction Pattern
- Always use `db.TransactionManager.StartTransaction()`
- `tr.GetObject(id, OpenMode.ForRead)` for reading
- `tr.GetObject(id, OpenMode.ForWrite)` for modifying
- Call `tr.Commit()` to persist changes
- Using block auto-aborts if not committed

## Common Patterns

### Open ModelSpace for writing
```csharp
var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
btr.AppendEntity(entity);
tr.AddNewlyCreatedDBObject(entity, true);
```

### Query all entities in ModelSpace
```csharp
var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
foreach (ObjectId id in btr)
{
    var entity = (Entity)tr.GetObject(id, OpenMode.ForRead);
    // process entity
}
```

### Create a Line
```csharp
var line = new Line(new Point3d(0, 0, 0), new Point3d(100, 0, 0));
btr.AppendEntity(line);
tr.AddNewlyCreatedDBObject(line, true);
```

### Create a Circle
```csharp
var circle = new Circle(new Point3d(50, 50, 0), Vector3d.ZAxis, 25.0);
btr.AppendEntity(circle);
tr.AddNewlyCreatedDBObject(circle, true);
```

### Modify entity properties
```csharp
var entity = (Entity)tr.GetObject(entityId, OpenMode.ForWrite);
entity.Color = Color.FromColorIndex(ColorMethod.ByAci, 1); // Red
entity.Layer = "MyLayer";
```

### Get/Set XData
```csharp
var rb = entity.GetXDataForApplication("AppName");
entity.XData = new ResultBuffer(
    new TypedValue((int)DxfCode.ExtendedDataRegAppName, "AppName"),
    new TypedValue((int)DxfCode.ExtendedDataAsciiString, "value")
);
```

### Layer operations
```csharp
var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
if (!lt.Has("NewLayer"))
{
    var ltr = new LayerTableRecord { Name = "NewLayer" };
    lt.UpgradeOpen();
    lt.Add(ltr);
    tr.AddNewlyCreatedDBObject(ltr, true);
}
```

## Units
- AutoCAD uses drawing units (configurable, typically mm or inches)
- No internal unit conversion needed (unlike Revit)
- `db.Insunits` indicates the drawing's insertion units

## Key Differences from Revit
- No `FilteredElementCollector` — iterate `BlockTableRecord` directly
- No `ElementId` — use `ObjectId`
- No built-in parameter system — use XData or dictionaries
- Transactions: `StartTransaction()` not `new Transaction()`
- Must explicitly `AppendEntity` + `AddNewlyCreatedDBObject`

## Common Namespaces
- `Autodesk.AutoCAD.DatabaseServices` — entities, transactions, database
- `Autodesk.AutoCAD.Geometry` — Point3d, Vector3d, Matrix3d
- `Autodesk.AutoCAD.ApplicationServices` — Document, Application
- `Autodesk.AutoCAD.EditorInput` — Editor, selection, prompts
- `Autodesk.AutoCAD.Colors` — Color
- `Autodesk.AutoCAD.Runtime` — CommandMethod attribute

## Selection
```csharp
var result = ed.GetSelection();
if (result.Status == PromptStatus.OK)
{
    foreach (SelectedObject so in result.Value)
    {
        var entity = (Entity)tr.GetObject(so.ObjectId, OpenMode.ForRead);
    }
}
```

## WPF UI (Custom Dialogs)

Only use when creating custom windows/dialogs. Requires `#r` directives:

```csharp
#r "PresentationFramework"
#r "PresentationCore"
#r "WindowsBase"
#r "System.Xaml"
```

### Namespace Conflicts (Critical)

These WPF types shadow AutoCAD API types — **always use fully qualified names or aliases**:

| Conflict | WPF (avoid bare import) | AutoCAD API |
|----------|------------------------|-------------|
| `Color` | `System.Windows.Media.Color` | `Autodesk.AutoCAD.Colors.Color` |
| `Line` | `System.Windows.Shapes.Line` | `Autodesk.AutoCAD.DatabaseServices.Line` |
| `Point` | `System.Windows.Point` | `Autodesk.AutoCAD.Geometry.Point3d` |
| `Matrix` | `System.Windows.Media.Matrix` | `Autodesk.AutoCAD.Geometry.Matrix3d` |
| `Application` | `System.Windows.Application` | `Autodesk.AutoCAD.ApplicationServices.Application` |

### Rules:
1. **Never** `using System.Windows.Controls;` alongside AutoCAD usings
2. Use aliases: `using WpfColor = System.Windows.Media.Color;`
3. Or fully qualify: `new System.Windows.Controls.Grid()`
4. Keep AutoCAD usings without alias

### Minimal Window Template (XAML-less)

```csharp
#r "PresentationFramework"
#r "PresentationCore"
#r "WindowsBase"
using System;
using System.Windows;
using System.Windows.Controls;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

public class Command
{
    [CommandMethod("MYDIALOG", CommandFlags.Session)]
    public void Execute()
    {
        var doc = AcApp.DocumentManager.MdiActiveDocument;

        var window = new Window
        {
            Title = "My Tool",
            Width = 400, Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        var stack = new StackPanel { Margin = new Thickness(10) };
        stack.Children.Add(new TextBlock { Text = $"Drawing: {doc.Name}" });
        stack.Children.Add(new Button { Content = "OK", Margin = new Thickness(0, 10, 0, 0) });
        ((Button)stack.Children[1]).Click += (s, e) => window.Close();

        window.Content = stack;
        window.ShowDialog();

        doc.Editor.WriteMessage("\nDialog closed");
    }
}
```

### Threading & Dialog Safety

**ShowDialog() blocks the MCP execution thread.** If no user closes the window, the host freezes.

**Never use `Show()`** — returns immediately, agent misses results, risks host crash on GC.

**Every dialog MUST have a self-destruct timeout** — no exceptions:

```csharp
// MANDATORY: always add auto-close timer to ANY dialog
var timer = new System.Windows.Threading.DispatcherTimer();
timer.Interval = TimeSpan.FromSeconds(30); // 30s for forms, 10s for info
timer.Tick += (s, e) => { timer.Stop(); window.Close(); };
timer.Start();
window.ShowDialog(); // guaranteed to close even if user is absent
```

| Intent | Timeout |
|--------|---------|
| Input form (user picks/types) | 30s |
| Informational display (stats, results) | 10s |
| No UI needed (autonomous) | Skip dialog, use `ed.WriteMessage` |

**Destructive operations**: Do NOT create confirmation dialogs in host. Agent handles user confirmation in chat before sending code. Host-side safety = transaction + undo (`navigate_history`).

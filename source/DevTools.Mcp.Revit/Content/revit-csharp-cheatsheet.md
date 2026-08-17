# Revit API Cheat Sheet

## Required Pattern (IExternalCommand)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.Manual)]
public class Command : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        // Your code here
        message = "result text"; // output to caller
        return Result.Succeeded;
    }
}
```

**Always include all 6 usings above.** `System` provides `Math`, `Func<>`, `Action<>`. `System.Collections.Generic` provides `List<T>`, `Dictionary<K,V>`.

## Transaction Modes
- `TransactionMode.Manual` — you open/commit transactions (most common)
- `TransactionMode.ReadOnly` — no modifications, no transaction needed

## Units
- Internal units are always **feet** (1 ft = 304.8 mm)
- Use `UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters)` to convert

## Common Patterns

### Query elements
```csharp
var walls = new FilteredElementCollector(doc)
    .OfClass(typeof(Wall))
    .Cast<Wall>()
    .ToList();
```

### Query by category
```csharp
var doors = new FilteredElementCollector(doc)
    .OfCategory(BuiltInCategory.OST_Doors)
    .WhereElementIsNotElementType()
    .ToElements();
```

### Modify elements (transaction required)
```csharp
using (var tx = new Transaction(doc, "Description"))
{
    tx.Start();
    element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set("value");
    tx.Commit();
}
```

### Create elements
```csharp
using (var tx = new Transaction(doc, "Create"))
{
    tx.Start();
    var line = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(10, 0, 0));
    var wall = Wall.Create(doc, line, wallTypeId, levelId, height, offset, false, false);
    tx.Commit();
}
```

### Get type from instance
```csharp
var wallType = (WallType)doc.GetElement(wall.GetTypeId());
```

### Get parameter value
```csharp
var param = element.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
double height = param.AsDouble(); // in feet
string text = param.AsString();
ElementId id = param.AsElementId();
```

## Version Pitfalls
- `ElementId.Value` (2024+) replaces `ElementId.IntegerValue` (deprecated)
- `FilteredElementCollector` requires `using System.Linq` for `.Cast<T>()`, `.FirstOrDefault()`
- `doc.Delete()` accepts `ICollection<ElementId>` — use `.ToList()` on LINQ results

## Common BuiltInCategories
`OST_Walls`, `OST_Doors`, `OST_Windows`, `OST_Floors`, `OST_Roofs`,
`OST_Rooms`, `OST_Columns`, `OST_StructuralFraming`, `OST_Furniture`,
`OST_MEPSpaces`, `OST_Ducts`, `OST_Pipes`, `OST_GenericModel`

## Common BuiltInParameters
- `ALL_MODEL_INSTANCE_COMMENTS` — Comments
- `ALL_MODEL_MARK` — Mark
- `WALL_USER_HEIGHT_PARAM` — Wall height
- `ROOM_NAME` / `ROOM_NUMBER` — Room
- `ELEM_FAMILY_AND_TYPE_PARAM` — Family and Type

## Warnings
- `doc.GetWarnings()` returns active warnings (query after modifications)
- Overlapping elements, duplicate marks, etc. are warnings (not errors)

## WPF UI (Custom Dialogs)

Only use when creating custom windows/dialogs. Requires `#r` directives:

```csharp
#r "PresentationFramework"
#r "PresentationCore"
#r "WindowsBase"
#r "System.Xaml"
```

### Namespace Conflicts (Critical)

These WPF types shadow Revit API types — **always use fully qualified names or aliases**:

| Conflict | WPF (avoid bare import) | Revit API |
|----------|------------------------|-----------|
| `Grid` | `System.Windows.Controls.Grid` | `Autodesk.Revit.DB.Grid` |
| `ComboBox` | `System.Windows.Controls.ComboBox` | `Autodesk.Revit.UI.ComboBox` |
| `TextBox` | `System.Windows.Controls.TextBox` | `Autodesk.Revit.UI.TextBox` |
| `Color` | `System.Windows.Media.Color` | `Autodesk.Revit.DB.Color` |
| `Line` | `System.Windows.Shapes.Line` | `Autodesk.Revit.DB.Line` |
| `Document` | `System.Windows.Documents.*` | `Autodesk.Revit.DB.Document` |
| `Element` | `System.Windows.FrameworkElement` | `Autodesk.Revit.DB.Element` |
| `Point` | `System.Windows.Point` | `Autodesk.Revit.DB.XYZ` |
| `Transform` | `System.Windows.Media.Transform` | `Autodesk.Revit.DB.Transform` |

### Rules:
1. **Never** `using System.Windows.Controls;` alongside `using Autodesk.Revit.DB;` or `using Autodesk.Revit.UI;`
2. Use aliases: `using WpfGrid = System.Windows.Controls.Grid;` `using WpfComboBox = System.Windows.Controls.ComboBox;` `using WpfTextBox = System.Windows.Controls.TextBox;`
3. Or fully qualify: `new System.Windows.Controls.Grid()`
4. Keep Revit usings short (`DB.Grid`, `DB.Line`)

### Minimal Window Template (XAML-less)

```csharp
#r "PresentationFramework"
#r "PresentationCore"
#r "WindowsBase"
using System;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using WpfGrid = System.Windows.Controls.Grid;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

[Transaction(TransactionMode.ReadOnly)]
public class Command : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var doc = commandData.Application.ActiveUIDocument.Document;

        var window = new Window
        {
            Title = "My Tool",
            Width = 400, Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };

        var stack = new StackPanel { Margin = new Thickness(10) };
        stack.Children.Add(new TextBlock { Text = $"Document: {doc.Title}" });
        stack.Children.Add(new Button { Content = "OK", Margin = new Thickness(0, 10, 0, 0) });
        ((Button)stack.Children[1]).Click += (s, e) => window.Close();

        window.Content = stack;
        window.ShowDialog();

        message = "Dialog shown";
        return Result.Succeeded;
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
| No UI needed (autonomous) | Skip dialog, use `message`/`print` |

**Destructive operations**: Do NOT create confirmation dialogs in host. Agent handles user confirmation in chat before sending code. Host-side safety = transaction + undo (`navigate_history`).

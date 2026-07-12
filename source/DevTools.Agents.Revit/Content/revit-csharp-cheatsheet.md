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

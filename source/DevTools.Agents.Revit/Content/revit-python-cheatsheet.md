# Revit Python Cheatsheet

## Required Pattern

Always wrap code in `def run():` and call it at the end. Do **not** create global variables or read `RevitContext` at module level.

```python
def run():
    doc = RevitContext.ActiveDocument
    if doc is None:
        print("No active document")
        return
    print(f"Document: {doc.Title}")

run()
```

## Available Builtins

Inherited from the host Python runtime scope:

| Name | Description |
|------|-------------|
| `DB` | `Autodesk.Revit.DB` namespace |
| `UI` | `Autodesk.Revit.UI` namespace |
| `RevitContext` | `RevitDevTool.Core.RevitContext` — `.ActiveDocument`, `.ActiveUiDocument`, `.ActiveView`, `.UiApplication` |
| `print()` | Captured and returned to the caller |

Import additional types inside `run()` when needed:

```python
def run():
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory
    doc = RevitContext.ActiveDocument
    # ...
run()
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
def run():
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

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
mm_value = 3000.0
internal = DB.UnitUtils.ConvertToInternalUnits(mm_value, DB.UnitTypeId.Millimeters)
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
ui_doc = RevitContext.ActiveUiDocument
if ui_doc:
    refs = ui_doc.Selection.PickObjects(DB.UI.Selection.ObjectType.Element, "Pick elements")
```

## Rules

1. **Always** use `def run(): ... run()` — never execute host API at module level.
2. **Never** assign to module-level globals (`doc = ...` outside a function).
3. Read `RevitContext.ActiveDocument` **inside** `run()`, not at import time.
4. Use `print()` for output — stdout/stderr are captured and returned.
5. Read `revit://model/context` for current model state before querying.

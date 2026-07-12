# AutoCAD Python Cheatsheet

## Required Pattern

Always wrap code in `def run():` and call it at the end. Do **not** create global variables or access the database at module level.

```python
def run():
    from Autodesk.AutoCAD.ApplicationServices.Core import Application
    doc = Application.DocumentManager.MdiActiveDocument
    if doc is None:
        print("No active document")
        return
    print(f"Document: {doc.Name}")

run()
```

## Available Builtins

Inherited from the host Python runtime scope:

| Name | Description |
|------|-------------|
| `DB` | AutoCAD database types (`Autodesk.AutoCAD.DatabaseServices`, etc.) |
| `AcadContext` | Host context helpers for active document/editor (when injected by setup) |
| `print()` | Captured and returned to the caller |

Import namespaces inside `run()` when needed:

```python
def run():
    from Autodesk.AutoCAD.ApplicationServices.Core import Application
    from Autodesk.AutoCAD.DatabaseServices import Transaction, OpenMode
    doc = Application.DocumentManager.MdiActiveDocument
    db = doc.Database
    # ...
run()
```

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

Always acquire the active document inside `run()` and use a transaction for modifications:

```python
def run():
    from Autodesk.AutoCAD.ApplicationServices.Core import Application
    from Autodesk.AutoCAD.DatabaseServices import Transaction, OpenMode

    doc = Application.DocumentManager.MdiActiveDocument
    if doc is None:
        print("No active document")
        return

    db = doc.Database
    tr = db.TransactionManager.StartTransaction()
    try:
        bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead)
        btr = tr.GetObject(bt[DB.BlockTableRecord.ModelSpace], OpenMode.ForWrite)
        # create or modify entities here
        tr.Commit()
        print("Committed")
    except Exception as ex:
        print(f"Aborted: {ex}")

run()
```

### Open ModelSpace for writing
```python
bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead)
btr = tr.GetObject(bt[DB.BlockTableRecord.ModelSpace], OpenMode.ForWrite)
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

## Rules

1. **Always** use `def run(): ... run()` — never execute host API at module level.
2. **Never** assign to module-level globals (`doc = ...` outside a function).
3. Acquire `Application.DocumentManager.MdiActiveDocument` **inside** `run()`.
4. Use `print()` for output — stdout/stderr are captured and returned.
5. Wrap all database reads/writes in `db.TransactionManager.StartTransaction()`.

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

## Package Lifecycle (PEP 723)

- Packages declared in `# /// script` header are auto-installed on first use.
- First install: 5–30s (network download). Subsequent imports: instant (cached in memory).
- **Limitation**: Once a package is imported, its version is fixed for the entire host session. You cannot upgrade an already-imported package without restarting the host process.
- **Limitation**: If multiple host instances run simultaneously, concurrent package installs may conflict. Pin exact versions to avoid ambiguity.
- **Tip**: Use `pandas==2.2.0` (exact pin) rather than `pandas>=2.0` for reproducible behavior.

## Rules

1. **Always** include explicit imports (`from Autodesk.AutoCAD.DatabaseServices import ...`).
2. **Always** use `def run(): ... run()` — never execute host API at module level.
3. **Never** assign to module-level globals (`doc = ...` outside a function).
4. Acquire `Application.DocumentManager.MdiActiveDocument` **inside** `run()`.
5. Use `print()` for output — stdout/stderr are captured and returned.
6. Wrap all database reads/writes in `db.TransactionManager.StartTransaction()`.

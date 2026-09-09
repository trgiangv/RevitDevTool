# Civil 3D Examples

AutoCAD-family illustration — not a separate RevitDevTool host product. Platform features: [AutoCAD](/docs/hosts/Hosts-AutoCAD).

Python examples for Civil 3D-specific APIs using RevitDevTool.

## Prerequisites

- Civil 3D 2022–2027 with RevitDevTool installed
- Active drawing open in Civil 3D

## List Surfaces

```python
# /// script
# dependencies = ["tabulate>=0.9"]
# ///

import clr
clr.AddReference("AeccDbMgd")

from Autodesk.AutoCAD.ApplicationServices.Core import Application
from Autodesk.AutoCAD.DatabaseServices import OpenMode
from Autodesk.Civil.DatabaseServices import CivilDocument, TinSurface
from tabulate import tabulate

doc = Application.DocumentManager.MdiActiveDocument
db = doc.Database
civil_doc = CivilDocument.GetCivilDocument(db)

tr = db.TransactionManager.StartTransaction()
try:
    surface_ids = civil_doc.GetSurfaceIds()
    rows = []
    for oid in surface_ids:
        surface = tr.GetObject(oid, OpenMode.ForRead)
        name = surface.Name
        area = surface.GetArea() if hasattr(surface, "GetArea") else "N/A"
        rows.append([name, type(surface).__name__, area])
    tr.Commit()
finally:
    tr.Dispose()

print(tabulate(rows, headers=["Surface", "Type", "Area"]))
```

## Alignment Station Report

```python
# /// script
# dependencies = ["tabulate>=0.9"]
# ///

import clr
clr.AddReference("AeccDbMgd")

from Autodesk.AutoCAD.ApplicationServices.Core import Application
from Autodesk.AutoCAD.DatabaseServices import OpenMode
from Autodesk.Civil.DatabaseServices import CivilDocument
from tabulate import tabulate

doc = Application.DocumentManager.MdiActiveDocument
db = doc.Database
civil_doc = CivilDocument.GetCivilDocument(db)

tr = db.TransactionManager.StartTransaction()
try:
    align_ids = civil_doc.GetAlignmentIds()
    rows = []
    for oid in align_ids:
        alignment = tr.GetObject(oid, OpenMode.ForRead)
        start_sta = alignment.StartingStation
        end_sta = alignment.EndingStation
        rows.append([
            alignment.Name,
            f"{alignment.Length:.2f}",
            f"{start_sta:.2f}",
            f"{end_sta:.2f}",
        ])
    tr.Commit()
finally:
    tr.Dispose()

print(tabulate(rows, headers=["Alignment", "Length", "Start Sta", "End Sta"]))
```

## Pipe Network Inventory

```python
# /// script
# dependencies = ["tabulate>=0.9"]
# ///

import clr
clr.AddReference("AeccDbMgd")

from Autodesk.AutoCAD.ApplicationServices.Core import Application
from Autodesk.AutoCAD.DatabaseServices import OpenMode
from Autodesk.Civil.DatabaseServices import CivilDocument, Network
from tabulate import tabulate

doc = Application.DocumentManager.MdiActiveDocument
db = doc.Database
civil_doc = CivilDocument.GetCivilDocument(db)

tr = db.TransactionManager.StartTransaction()
try:
    network_ids = civil_doc.GetPipeNetworkIds()
    for oid in network_ids:
        network = tr.GetObject(oid, OpenMode.ForRead)
        print(f"\nNetwork: {network.Name}")
        print(f"  Pipes: {network.GetPipeIds().Count}")
        print(f"  Structures: {network.GetStructureIds().Count}")
    tr.Commit()
finally:
    tr.Dispose()
```

## Entity Type Summary

```python
from Autodesk.AutoCAD.ApplicationServices.Core import Application
from Autodesk.AutoCAD.DatabaseServices import (
    BlockTable, BlockTableRecord, OpenMode,
)
from collections import Counter

doc = Application.DocumentManager.MdiActiveDocument
db = doc.Database

tr = db.TransactionManager.StartTransaction()
try:
    bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead)
    ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)

    types = Counter()
    for oid in ms:
        ent = tr.GetObject(oid, OpenMode.ForRead)
        types[type(ent).__name__] += 1
    tr.Commit()
finally:
    tr.Dispose()

for entity_type, count in types.most_common():
    print(f"{entity_type}: {count}")
```

## Using pytest for Validation

See [pytest](/docs/testing/pytest) for setup. From your test project:

```powershell
uv run pytest --host civil3d --host-version 2025 -v
```

Example test:

```python
def test_alignment_exists(acad_db, acad_transaction):
    import clr
    clr.AddReference("AeccDbMgd")
    from Autodesk.Civil.DatabaseServices import CivilDocument

    civil_doc = CivilDocument.GetCivilDocument(acad_db)
    align_ids = civil_doc.GetAlignmentIds()
    assert align_ids.Count > 0, "Drawing should have at least one alignment"
```

## See Also

- [Civil 3D example](/docs/hosts/Hosts-AutoCAD) — minimal API entry point
- [AutoCAD](/docs/hosts/Hosts-AutoCAD) — shared platform
- [Examples Overview](/docs/examples/Examples-Overview) — all examples

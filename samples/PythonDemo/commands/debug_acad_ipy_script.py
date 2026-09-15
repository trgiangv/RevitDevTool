"""IronPython debug sample (AutoCAD). Host listens on 127.0.0.1:4567.

Must stay named *_ipy_script.py so ScriptExecutionProvider uses IronPython.
Do not import pydevd here -- AcadDevTool already called _enable_attach.

Attach with launch.json "Attach Host: IronPython" (debugpy, port 4567).
CPython debugpy stays on 5678.

IronPython does not run SetupAcad.py; add AutoCAD CLR refs before imports.
"""

import clr

clr.AddReference("accoremgd")
clr.AddReference("acdbmgd")
clr.AddReference("acmgd")

from Autodesk.AutoCAD.ApplicationServices.Core import Application
from Autodesk.AutoCAD.DatabaseServices import (
    BlockTableRecord,
    Entity,
    LayerTableRecord,
    OpenMode,
)
from Autodesk.AutoCAD.EditorInput import PromptStatus


def main():
    doc = Application.DocumentManager.MdiActiveDocument
    if doc is None:
        print("No active document. Open a drawing, then run again.")
        return

    db = doc.Database
    ed = doc.Editor

    # Breakpoint here after VS Code is attached to 4567.
    print("document:", doc.Name)
    print("filename:", db.Filename)

    loc = doc.LockDocument()
    try:
        tr = db.TransactionManager.StartTransaction()
        try:
            _dump_selection(ed, tr)
            _dump_layers(db, tr)
            _dump_model_space(db, tr)
            tr.Commit()
        except Exception:
            tr.Abort()
            raise
        finally:
            tr.Dispose()
    finally:
        loc.Dispose()


def _dump_selection(ed, tr):
    result = ed.SelectImplied()
    if result.Status != PromptStatus.OK or result.Value is None:
        print("selection count: 0")
        return

    n = result.Value.Count
    print("selection count:", n)
    shown = 0
    for selected in result.Value:
        if selected is None:
            continue
        ent = tr.GetObject(selected.ObjectId, OpenMode.ForRead)
        if not isinstance(ent, Entity):
            continue
        print(
            " ",
            ent.GetType().Name,
            "handle=",
            str(ent.Handle),
            "layer=",
            ent.Layer,
        )
        shown += 1
        if shown >= 10:
            print("  ... truncated")
            break


def _dump_layers(db, tr):
    table = tr.GetObject(db.LayerTableId, OpenMode.ForRead)
    names = []
    for layer_id in table:
        rec = tr.GetObject(layer_id, OpenMode.ForRead)
        if isinstance(rec, LayerTableRecord):
            names.append(rec.Name)
    names.sort()
    print("layer count:", len(names))
    for name in names[:15]:
        print(" ", name)
    if len(names) > 15:
        print("  ... truncated")


def _dump_model_space(db, tr):
    bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead)
    model = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead)
    counts = {}
    total = 0
    for obj_id in model:
        ent = tr.GetObject(obj_id, OpenMode.ForRead)
        if not isinstance(ent, Entity):
            continue
        type_name = ent.GetType().Name
        counts[type_name] = counts.get(type_name, 0) + 1
        total += 1
    print("model space entities:", total)
    types = list(counts.keys())
    types.sort()
    for type_name in types:
        print(" ", type_name, counts[type_name])


if __name__ == "__main__":
    main()

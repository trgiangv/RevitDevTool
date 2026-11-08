"""Geometry tests — read entities, bounding boxes, coordinates."""

import pytest


def test_model_space_entity_count(acad_db, acad_transaction):
    """Count entities in Model Space."""
    from Autodesk.AutoCAD.DatabaseServices import (
        BlockTable,
        BlockTableRecord,
        OpenMode,
    )

    bt = acad_transaction.GetObject(acad_db.BlockTableId, OpenMode.ForRead)
    model_space = acad_transaction.GetObject(
        bt[BlockTableRecord.ModelSpace], OpenMode.ForRead
    )

    count = 0
    for _ in model_space:
        count += 1

    print(f"Model Space entities: {count}")
    assert count >= 0


def test_entity_types_summary(acad_db, acad_transaction):
    """Summarize entity types in Model Space."""
    from Autodesk.AutoCAD.DatabaseServices import (
        BlockTable,
        BlockTableRecord,
        Entity,
        OpenMode,
    )

    bt = acad_transaction.GetObject(acad_db.BlockTableId, OpenMode.ForRead)
    model_space = acad_transaction.GetObject(
        bt[BlockTableRecord.ModelSpace], OpenMode.ForRead
    )

    type_counts: dict[str, int] = {}
    for oid in model_space:
        ent = acad_transaction.GetObject(oid, OpenMode.ForRead)
        if isinstance(ent, Entity):
            type_name = ent.GetType().Name
            type_counts[type_name] = type_counts.get(type_name, 0) + 1

    print(f"Entity type summary ({sum(type_counts.values())} total):")
    for name, cnt in sorted(type_counts.items(), key=lambda x: -x[1])[:20]:
        print(f"  {name}: {cnt}")
    assert isinstance(type_counts, dict)


def test_first_entity_bounding_box(acad_db, acad_transaction):
    """Read bounding box of the first entity in Model Space."""
    from Autodesk.AutoCAD.DatabaseServices import (
        BlockTable,
        BlockTableRecord,
        Entity,
        Extents3d,
        OpenMode,
    )

    bt = acad_transaction.GetObject(acad_db.BlockTableId, OpenMode.ForRead)
    model_space = acad_transaction.GetObject(
        bt[BlockTableRecord.ModelSpace], OpenMode.ForRead
    )

    for oid in model_space:
        ent = acad_transaction.GetObject(oid, OpenMode.ForRead)
        if not isinstance(ent, Entity):
            continue
        try:
            ext = ent.GeometricExtents
        except Exception:
            continue
        if ext is None:
            continue

        dx = ext.MaxPoint.X - ext.MinPoint.X
        dy = ext.MaxPoint.Y - ext.MinPoint.Y
        dz = ext.MaxPoint.Z - ext.MinPoint.Z
        print(f"Entity: {ent.GetType().Name} (Handle={ent.Handle})")
        print(f"  Min: ({ext.MinPoint.X:.4f}, {ext.MinPoint.Y:.4f}, {ext.MinPoint.Z:.4f})")
        print(f"  Max: ({ext.MaxPoint.X:.4f}, {ext.MaxPoint.Y:.4f}, {ext.MaxPoint.Z:.4f})")
        print(f"  Size: {dx:.4f} x {dy:.4f} x {dz:.4f}")
        assert dx >= 0 and dy >= 0 and dz >= 0
        return

    pytest.skip("No entities with valid geometric extents in Model Space")


def test_database_extents(acad_db):
    """Read overall database extents (drawing limits)."""
    ext_min = acad_db.Extmin
    ext_max = acad_db.Extmax

    print(f"Database Extmin: ({ext_min.X:.4f}, {ext_min.Y:.4f}, {ext_min.Z:.4f})")
    print(f"Database Extmax: ({ext_max.X:.4f}, {ext_max.Y:.4f}, {ext_max.Z:.4f})")
    assert ext_min is not None
    assert ext_max is not None


def test_line_geometry(acad_db, acad_transaction):
    """Find the first Line entity and read start/end points."""
    from Autodesk.AutoCAD.DatabaseServices import (
        BlockTable,
        BlockTableRecord,
        Line,
        OpenMode,
    )

    bt = acad_transaction.GetObject(acad_db.BlockTableId, OpenMode.ForRead)
    model_space = acad_transaction.GetObject(
        bt[BlockTableRecord.ModelSpace], OpenMode.ForRead
    )

    for oid in model_space:
        ent = acad_transaction.GetObject(oid, OpenMode.ForRead)
        if isinstance(ent, Line):
            sp = ent.StartPoint
            ep = ent.EndPoint
            length = ent.Length
            print(f"Line: ({sp.X:.4f}, {sp.Y:.4f}) → ({ep.X:.4f}, {ep.Y:.4f})")
            print(f"  Length: {length:.4f}")
            assert length >= 0
            return

    pytest.skip("No Line entities in Model Space")

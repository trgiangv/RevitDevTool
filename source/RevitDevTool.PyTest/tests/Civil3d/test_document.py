"""Document-level tests — layers, linetypes, block table, text styles."""

import pytest


def test_layer_table(acad_db, acad_transaction):
    """Enumerate all layers in the active document."""
    from Autodesk.AutoCAD.DatabaseServices import (
        LayerTable,
        LayerTableRecord,
        OpenMode,
    )

    layer_table_id = acad_db.LayerTableId
    layer_table = acad_transaction.GetObject(layer_table_id, OpenMode.ForRead)

    layers: list[str] = []
    for layer_id in layer_table:
        layer = acad_transaction.GetObject(layer_id, OpenMode.ForRead)
        if isinstance(layer, LayerTableRecord):
            layers.append(layer.Name)

    print(f"Total layers: {len(layers)}")
    for name in sorted(layers)[:20]:
        print(f"  {name}")
    assert len(layers) > 0, "Document should have at least one layer (0)"


def test_layer_zero_exists(acad_db, acad_transaction):
    """Layer '0' must always exist."""
    from Autodesk.AutoCAD.DatabaseServices import (
        LayerTable,
        OpenMode,
    )

    layer_table = acad_transaction.GetObject(acad_db.LayerTableId, OpenMode.ForRead)
    assert layer_table.Has("0"), "Layer '0' should always exist"
    print("Layer '0' verified")


def test_linetype_table(acad_db, acad_transaction):
    """Enumerate linetypes."""
    from Autodesk.AutoCAD.DatabaseServices import (
        LinetypeTable,
        LinetypeTableRecord,
        OpenMode,
    )

    lt_table = acad_transaction.GetObject(acad_db.LinetypeTableId, OpenMode.ForRead)
    linetypes: list[str] = []
    for lt_id in lt_table:
        lt = acad_transaction.GetObject(lt_id, OpenMode.ForRead)
        if isinstance(lt, LinetypeTableRecord):
            linetypes.append(lt.Name)

    print(f"Total linetypes: {len(linetypes)}")
    for name in sorted(linetypes):
        print(f"  {name}")
    assert len(linetypes) > 0


def test_block_table(acad_db, acad_transaction):
    """Enumerate block definitions."""
    from Autodesk.AutoCAD.DatabaseServices import (
        BlockTable,
        BlockTableRecord,
        OpenMode,
    )

    bt = acad_transaction.GetObject(acad_db.BlockTableId, OpenMode.ForRead)
    blocks: list[str] = []
    for block_id in bt:
        block = acad_transaction.GetObject(block_id, OpenMode.ForRead)
        if isinstance(block, BlockTableRecord):
            blocks.append(block.Name)

    print(f"Total block definitions: {len(blocks)}")
    for name in sorted(blocks)[:20]:
        print(f"  {name}")
    assert "*Model_Space" in blocks, "Model space block should exist"


def test_text_style_table(acad_db, acad_transaction):
    """Enumerate text styles."""
    from Autodesk.AutoCAD.DatabaseServices import (
        TextStyleTable,
        TextStyleTableRecord,
        OpenMode,
    )

    ts_table = acad_transaction.GetObject(acad_db.TextStyleTableId, OpenMode.ForRead)
    styles: list[str] = []
    for ts_id in ts_table:
        ts = acad_transaction.GetObject(ts_id, OpenMode.ForRead)
        if isinstance(ts, TextStyleTableRecord):
            styles.append(ts.Name)

    print(f"Total text styles: {len(styles)}")
    for name in sorted(styles):
        print(f"  {name}")
    assert len(styles) > 0


def test_units_info(acad_db):
    """Report drawing units."""
    from Autodesk.AutoCAD.DatabaseServices import UnitsValue

    insunits = acad_db.Insunits
    print(f"Insert units: {insunits}")
    assert insunits is not None

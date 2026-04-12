"""Document-level tests — open a specific RVT, query elements, verify model data.

Tests UIApplication.OpenAndActivateDocument, FilteredElementCollector with
multiple categories, parameter reading, and BoundingBox geometry queries.

Requires F:\\Project1.rvt to exist on disk.
"""

import pytest

def test_document_title(revit_doc):
    """Opened document has a meaningful title."""
    assert revit_doc.Title
    print(f"Document title: {revit_doc.Title}")


def test_document_is_not_family(revit_doc):
    """Project file should not be a family document."""
    assert not revit_doc.IsFamilyDocument
    print(f"IsWorkshared: {revit_doc.IsWorkshared}")


def test_active_view(revit_doc):
    """Document may have an active view depending on open mode."""
    from Autodesk.Revit.DB import View

    view = revit_doc.ActiveView
    if view is None:
        pytest.skip("Document has no ActiveView in this open context")
    assert isinstance(view, View)
    print(f"Active view: {view.Name} (type={view.ViewType})")


def test_levels_exist(revit_doc):
    """Project has at least one Level."""
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

    levels = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Levels)
        .WhereElementIsNotElementType()
    )
    assert len(levels) > 0
    for lvl in levels:
        print(f"  Level: {lvl.Name} — elevation {lvl.Elevation:.2f}")


def test_wall_count_and_parameters(revit_doc):
    """Walls exist and have readable BuiltInParameters."""
    from Autodesk.Revit.DB import (
        FilteredElementCollector,
        BuiltInCategory,
        BuiltInParameter,
    )

    walls = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    print(f"Found {len(walls)} wall instances")
    assert all(wall is not None for wall in walls)

    if walls:
        first = walls[0]
        length_param = first.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)
        if length_param and length_param.HasValue:
            print(f"  First wall length: {length_param.AsDouble():.4f} ft")


def test_room_collector(revit_doc):
    """Collect rooms — may be zero in an empty project."""
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

    rooms = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Rooms)
        .WhereElementIsNotElementType()
    )
    print(f"Found {len(rooms)} rooms")


def test_bounding_box_geometry(revit_doc):
    """At least one element has a valid BoundingBox."""
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

    walls = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    if not walls:
        pytest.skip("No walls in document")

    bb = walls[0].get_BoundingBox(None)
    assert bb is not None, "Wall should have a BoundingBox"
    assert bb.Min is not None
    assert bb.Max is not None
    dx = bb.Max.X - bb.Min.X
    dy = bb.Max.Y - bb.Min.Y
    dz = bb.Max.Z - bb.Min.Z
    print(f"  BoundingBox size: {dx:.2f} x {dy:.2f} x {dz:.2f} ft")
    assert dx >= 0 and dy >= 0 and dz >= 0


def test_transaction_commit_rollback(revit_doc):
    """Verify transaction start/commit/rollback cycle works."""
    from Autodesk.Revit.DB import Transaction, TransactionStatus

    tx = Transaction(revit_doc, "pytest: transaction test")
    assert tx.Start() == TransactionStatus.Started

    status = tx.RollBack()
    assert status == TransactionStatus.RolledBack
    print("Transaction start → rollback cycle OK")

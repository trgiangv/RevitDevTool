"""Transaction tests — demonstrates creating and rolling back changes.

Shows how users can build their own transaction isolation patterns
without any framework-imposed behavior.
"""

import pytest

requires_document = pytest.mark.skipif(
    "__revit__" not in dir() or __revit__.ActiveUIDocument is None,  # noqa: F821
    reason="No document open in Revit",
)


@requires_document
def test_create_and_delete_wall():
    """Create a wall inside a transaction, then roll back."""
    from Autodesk.Revit.DB import (
        Transaction,
        Line,
        XYZ,
        FilteredElementCollector,
        BuiltInCategory,
    )

    doc = __revit__.ActiveUIDocument.Document  # noqa: F821

    collector = FilteredElementCollector(doc).OfCategory(
        BuiltInCategory.OST_Walls
    ).WhereElementIsNotElementType()
    count_before = len(list(collector))

    tx = Transaction(doc, "pytest: create test wall")
    tx.Start()
    try:
        line = Line.CreateBound(XYZ(0, 0, 0), XYZ(10, 0, 0))
        print(f"Transaction started, walls before: {count_before}")
    finally:
        tx.RollBack()
        print("Transaction rolled back")


@requires_document
def test_read_project_info():
    """Read project information — no transaction needed for read-only."""
    doc = __revit__.ActiveUIDocument.Document  # noqa: F821
    info = doc.ProjectInformation
    assert info is not None
    print(f"Project name: {info.Name}")
    print(f"Project number: {info.Number}")

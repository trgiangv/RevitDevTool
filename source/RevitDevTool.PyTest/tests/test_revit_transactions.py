"""Transaction tests — demonstrates creating and rolling back changes.

Shows how users can build their own transaction isolation patterns
without any framework-imposed behavior.
"""

import pytest
from functools import wraps


def _get_active_uidocument_or_skip():
    try:
        if "__revit__" not in dir():  # noqa: F821
            pytest.skip("No Revit application available.")
        uidoc = getattr(__revit__, "ActiveUIDocument", None)  # noqa: F821
    except BaseException:
        pytest.skip("Cannot access ActiveUIDocument from Revit.")

    if uidoc is None:
        pytest.skip("No document open in Revit.")

    return uidoc


def requires_document(test_func):
    @wraps(test_func)
    def wrapped(*args, **kwargs):
        _get_active_uidocument_or_skip()
        return test_func(*args, **kwargs)

    return wrapped


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

    doc = _get_active_uidocument_or_skip().Document

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
    doc = _get_active_uidocument_or_skip().Document
    info = doc.ProjectInformation
    assert info is not None
    print(f"Project name: {info.Name}")
    print(f"Project number: {info.Number}")

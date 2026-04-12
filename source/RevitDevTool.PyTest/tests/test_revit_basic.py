"""Basic Revit API tests — demonstrates RevitDevTool.PyTest usage.

All Revit API imports must be inside function bodies (lazy imports).
__revit__ is available globally (injected by RevitDevTool).
"""

import pytest

requires_document = pytest.mark.skipif(
    "__revit__" not in dir() or __revit__.ActiveUIDocument is None,  # noqa: F821
    reason="No document open in Revit",
)


def test_revit_version():
    """Verify Revit is running and we can read the version."""
    app = __revit__.Application  # noqa: F821
    assert app.VersionNumber is not None
    print(f"Revit version: {app.VersionName}")


@requires_document
def test_active_document_exists():
    """Verify a document is open in Revit."""
    uidoc = __revit__.ActiveUIDocument  # noqa: F821
    assert uidoc is not None
    assert uidoc.Document is not None
    print(f"Active document: {uidoc.Document.Title}")


def test_wall_collector(revit_doc):
    """Query walls from the active document using FilteredElementCollector."""
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

    collector = (
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    walls = list(collector)
    print(f"Found {len(walls)} wall instances")
    assert isinstance(walls, list)

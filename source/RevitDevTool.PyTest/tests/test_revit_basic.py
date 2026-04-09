"""Basic Revit API tests — demonstrates RevitDevTool.PyTest usage.

All Revit API imports must be inside function bodies (lazy imports).
__revit__ is available globally (injected by RevitDevTool).
"""


def test_revit_version():
    """Verify Revit is running and we can read the version."""
    app = __revit__.Application  # noqa: F821
    assert app.VersionNumber is not None
    print(f"Revit version: {app.VersionName}")


def test_active_document_exists():
    """Verify a document is open in Revit."""
    uidoc = __revit__.ActiveUIDocument  # noqa: F821
    assert uidoc is not None
    assert uidoc.Document is not None
    print(f"Active document: {uidoc.Document.Title}")


def test_wall_collector():
    """Query walls from the active document using FilteredElementCollector."""
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

    doc = __revit__.ActiveUIDocument.Document  # noqa: F821
    collector = (
        FilteredElementCollector(doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    walls = list(collector)
    print(f"Found {len(walls)} wall instances")
    assert isinstance(walls, list)

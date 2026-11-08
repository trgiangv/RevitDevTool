"""Basic Revit API tests — demonstrates RevitDevTool.PyTest usage.

All Revit API imports must be inside function bodies (lazy imports).
__revit__ is available globally (injected by RevitDevTool).
"""

def test_revit_version():
    """Verify Revit is running and we can read the version."""
    app = __revit__.Application  # noqa: F821
    assert app.VersionNumber is not None
    print(f"Revit version: {app.VersionName}")


def test_target_document_available(revit_doc):
    """Verify the configured target document is available for test execution."""
    assert revit_doc is not None
    assert revit_doc.IsValidObject
    assert revit_doc.Title
    print(f"Target document: {revit_doc.Title}")


def test_basic_wall_collection(revit_doc):
    """Query walls from the configured test document."""
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

    collector = (
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    walls = list(collector)
    print(f"Found {len(walls)} wall instances")
    assert isinstance(walls, list)

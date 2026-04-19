"""Query current Revit active view and selected elements."""


def test_active_view_info(revit_doc):
    """Report the currently active view in Revit."""

    view = revit_doc.ActiveView

    print(f"Document: {revit_doc.Title}")
    print(f"Active View: {view.Name}")
    print(f"View Type: {view.ViewType}")
    print(f"View Id: {view.Id.IntegerValue}")

    assert view.Name is not None


def test_selected_elements(revit_uiapp, revit_doc):
    """Report currently selected elements in Revit."""
    uidoc = revit_uiapp.ActiveUIDocument  # noqa: F821
    selection = uidoc.Selection.GetElementIds()
    count = selection.Count

    print(f"Selected elements: {count}")
    for eid in selection:
        elem = revit_doc.GetElement(eid)
        category = elem.Category.Name if elem.Category else "N/A"
        name = elem.Name if elem.Name else "N/A"
        print(f"  [{eid.IntegerValue}] {category} — {name}")

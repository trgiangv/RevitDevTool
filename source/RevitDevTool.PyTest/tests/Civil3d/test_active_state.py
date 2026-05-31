"""Query current active document and selection state in Civil 3D."""

import pytest


def test_active_document_info(acad_doc):
    """Report the active document name and path."""
    print(f"Document: {acad_doc.Name}")
    print(f"Path: {acad_doc.Name}")
    assert acad_doc.Name is not None


def test_active_database_info(acad_db):
    """Report basic database properties."""
    from Autodesk.AutoCAD.DatabaseServices import SymbolUtilityServices

    filename = acad_db.Filename
    print(f"Database filename: {filename}")
    print(f"Database version: {acad_db.LastSavedAsVersion}")
    assert acad_db is not None


def test_editor_available(acad_editor):
    """Verify the Editor is accessible."""
    assert acad_editor is not None
    print(f"Editor document: {acad_editor.Document.Name}")


def test_current_space(acad_db, acad_transaction):
    """Report current space (Model or Paper)."""
    from Autodesk.AutoCAD.DatabaseServices import BlockTableRecord

    current_space_id = acad_db.CurrentSpaceId
    current_space = acad_transaction.GetObject(
        current_space_id, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead
    )
    if isinstance(current_space, BlockTableRecord):
        print(f"Current space: {current_space.Name}")
    assert current_space is not None


def test_selected_objects(acad_editor):
    """Report currently selected objects (may be empty)."""
    result = acad_editor.SelectImplied()
    from Autodesk.AutoCAD.EditorInput import PromptStatus

    if result.Status != PromptStatus.OK or result.Value is None:
        print("No objects selected (implied selection empty)")
        pytest.skip("No selection active")

    ss = result.Value
    count = ss.Count
    print(f"Selected objects: {count}")
    for sel_obj in ss:
        print(f"  ObjectId: {sel_obj.ObjectId}")
    assert count >= 0

# Test Patterns

## 1. Smoke Test — Verify Connection

```python
def test_revit_application_available():
    app = __revit__.Application  # noqa: F821
    assert app.VersionNumber is not None
    print(f"Revit {app.VersionName} (build {app.VersionBuild})")
```

## 2. Element Collection Query

```python
def test_wall_collection(revit_doc):
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

    walls = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    print(f"Found {len(walls)} wall instances")
    assert isinstance(walls, list)
```

## 3. Reading Parameters

```python
def test_wall_parameters(revit_doc):
    from Autodesk.Revit.DB import (
        FilteredElementCollector, BuiltInCategory, BuiltInParameter,
    )

    walls = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    if not walls:
        import pytest
        pytest.skip("No walls in document")

    first = walls[0]
    length_param = first.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)
    if length_param and length_param.HasValue:
        print(f"First wall length: {length_param.AsDouble():.4f} ft")
```

## 4. Document Properties

```python
import pytest

def test_document_title(revit_doc):
    assert revit_doc.Title
    print(f"Document: {revit_doc.Title}")

def test_document_not_family(revit_doc):
    assert not revit_doc.IsFamilyDocument

def test_active_view(revit_doc):
    from Autodesk.Revit.DB import View

    view = revit_doc.ActiveView
    if view is None:
        pytest.skip("No ActiveView in this context")
    assert isinstance(view, View)
    print(f"Active view: {view.Name} (type={view.ViewType})")
```

## 5. Transaction Start + Rollback

```python
def test_transaction_cycle(revit_doc):
    from Autodesk.Revit.DB import Transaction, TransactionStatus

    tx = Transaction(revit_doc, "pytest: smoke")
    assert tx.Start() == TransactionStatus.Started
    status = tx.RollBack()
    assert status == TransactionStatus.RolledBack
```

## 6. Auto-Rollback Fixture (Model Modification)

```python
def test_modify_with_rollback(revit_doc, revit_auto_rollback):
    """All changes are reverted after this test."""
    from Autodesk.Revit.DB import Transaction

    tx = Transaction(revit_doc, "pytest: modify")
    tx.Start()
    info = revit_doc.ProjectInformation
    info.Name = "Test Modified Name"
    tx.Commit()
    # revit_auto_rollback reverts everything
```

## 7. BoundingBox Geometry

```python
import pytest

def test_bounding_box(revit_doc):
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

    walls = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    if not walls:
        pytest.skip("No walls")

    bb = walls[0].get_BoundingBox(None)
    assert bb is not None
    assert bb.Max.X - bb.Min.X >= 0
```

## 8. Using PEP 723 Dependencies with Revit Data

```python
def test_revit_data_with_tabulate(revit_doc):
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory
    from tabulate import tabulate

    categories = [
        ("Walls", BuiltInCategory.OST_Walls),
        ("Floors", BuiltInCategory.OST_Floors),
        ("Doors", BuiltInCategory.OST_Doors),
    ]
    rows = []
    for name, cat in categories:
        count = (
            FilteredElementCollector(revit_doc)
            .OfCategory(cat)
            .WhereElementIsNotElementType()
            .GetElementCount()
        )
        rows.append([name, count])

    table = tabulate(rows, headers=["Category", "Count"], tablefmt="simple")
    print(f"Element summary:\n{table}")
```

## 9. RevitDevTool.Core Services

```python
import base64

def test_image_export(revit_doc):
    from RevitDevTool.Core import RevitImageExporter

    result = RevitImageExporter.ExportActiveView()
    assert result is not None
    assert result.ContentType == "image/png"
    assert result.FileSizeBytes > 0

    raw = base64.b64decode(result.Base64Data)
    assert raw[:4] == b"\x89PNG"
```

## 10. Selection and UI State

```python
def test_selected_elements(revit_uiapp, revit_doc):
    uidoc = revit_uiapp.ActiveUIDocument
    selection = uidoc.Selection.GetElementIds()
    print(f"Selected: {selection.Count} elements")
    for eid in selection:
        elem = revit_doc.GetElement(eid)
        cat = elem.Category.Name if elem.Category else "N/A"
        print(f"  [{eid.IntegerValue}] {cat} — {elem.Name}")
```

## 11. pytest.skip for Missing Prerequisites

```python
import pytest

def test_requires_specific_element(revit_doc):
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

    roofs = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Roofs)
        .WhereElementIsNotElementType()
    )
    if not roofs:
        pytest.skip("No roofs in test document")

    assert roofs[0].get_BoundingBox(None) is not None
```

## 12. pytest.raises for Expected Exceptions

```python
import pytest

def test_nonexistent_view(revit_doc):
    from RevitDevTool.Core import RevitImageExporter

    with pytest.raises(Exception):
        RevitImageExporter.ExportView[str]("__nonexistent__")
```

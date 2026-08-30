# Test Patterns

Host imports inside the function. CPython uses fixtures. IronPython uses `unittest`.

## CPython query

```python
def test_wall_count(revit_doc):
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory
    walls = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    assert isinstance(walls, list)
```

## Skip when the model lacks data

```python
def test_first_wall(revit_doc):
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory
    walls = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    if not walls:
        import pytest
        pytest.skip("No walls")
    assert walls[0].get_BoundingBox(None) is not None
```

## Mutate + auto-rollback

```python
def test_rename_project(revit_doc, revit_auto_rollback):
    from Autodesk.Revit.DB import Transaction
    tx = Transaction(revit_doc, "pytest: rename")
    tx.Start()
    revit_doc.ProjectInformation.Name = "pytest"
    tx.Commit()
```

## IronPython (`test_*_ipy.py`)

No fixtures, no PEP 723, no f-strings.

```python
import unittest

class WallTests(unittest.TestCase):
    def test_collector_runs(self):
        from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory
        doc = __revit__.ActiveUIDocument.Document
        walls = list(
            FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Walls)
            .WhereElementIsNotElementType()
        )
        self.assertIsInstance(walls, list)
```

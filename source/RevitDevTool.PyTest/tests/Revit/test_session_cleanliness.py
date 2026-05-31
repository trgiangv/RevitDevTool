"""Session-cleanliness tests for pytest lane execution."""

import uuid


def test_complex_di_imports_and_dotnet_bindings(revit_doc, humanize_mod):
    """Stress fixture DI + Python deps + .NET imports together."""
    import numpy as np
    import polars as pl
    from Autodesk.Revit.DB import BuiltInCategory, FilteredElementCollector
    from System.Collections.Generic import List

    # Verify .NET generic import path still binds correctly.
    numbers = List[int]()
    numbers.Add(7)
    numbers.Add(11)
    assert numbers.Count == 2

    walls = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    assert isinstance(walls, list)

    # Verify Python package imports and basic ops in same execution lane.
    assert "MB" in humanize_mod.naturalsize(1024 * 1024)
    assert np.array([1, 2, 3]).sum() == 6
    frame = pl.DataFrame({"k": ["A", "A", "B"], "v": [1, 2, 3]})
    grouped = frame.group_by("k").agg(pl.col("v").sum()).sort("k")
    assert grouped["v"].to_list() == [3, 3]


def test_session_module_state_starts_clean():
    """Mutate module state; next pytest run must start from None again."""
    import _session_probe

    assert _session_probe.SESSION_TOKEN is None
    _session_probe.SESSION_TOKEN = f"token-{uuid.uuid4()}"


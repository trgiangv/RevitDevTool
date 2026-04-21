# Conftest & Fixtures Guide

## conftest.py Structure

```python
# /// script
# dependencies = [
#   "humanize>=4.0",
#   "tabulate>=0.9",
#   "numpy>=2.0",
# ]
# ///
"""Test suite conftest — shared fixtures and PEP 723 dependencies.

PEP 723 dependencies above are auto-resolved by RevitDevTool's
PytestDependencyService. All test files share these packages.
"""

import os
import pytest

RVT_PATH = r"C:\path\to\test_model.rvt"
```

## Core Fixtures

### revit_uiapp (session)

Provides the UIApplication object:

```python
@pytest.fixture(scope="session")
def revit_uiapp():
    return __revit__  # noqa: F821
```

### revit_app (session)

Provides the Application object:

```python
@pytest.fixture(scope="session")
def revit_app(revit_uiapp):
    return revit_uiapp.Application
```

### revit_doc (session)

Opens and returns the target document. Skips if the RVT file is not found:

```python
@pytest.fixture(scope="session")
def revit_doc(revit_uiapp):
    if not os.path.isfile(RVT_PATH):
        pytest.skip(f"{RVT_PATH} not found")

    target = os.path.normcase(os.path.abspath(RVT_PATH))
    current_uidoc = revit_uiapp.ActiveUIDocument
    current_doc = current_uidoc.Document if current_uidoc else None

    if (current_doc is not None
        and os.path.normcase(os.path.abspath(current_doc.PathName or "")) == target):
        return current_doc

    current_uidoc = revit_uiapp.OpenAndActivateDocument(RVT_PATH)
    return current_uidoc.Document
```

### revit_auto_rollback (function)

Starts undo tracking before a test and always reverts after:

```python
@pytest.fixture
def revit_transaction_service():
    from RevitDevTool.Core import RevitTransactionService
    return RevitTransactionService

@pytest.fixture
def revit_auto_rollback(revit_transaction_service):
    revit_transaction_service.StartChanges()
    try:
        yield revit_transaction_service
    finally:
        revit_transaction_service.RevertChanges()
```

## Adding Custom Fixtures

### Element-specific fixture

```python
@pytest.fixture(scope="session")
def source_schedule(revit_doc):
    from Autodesk.Revit import DB
    SCHEDULE_ID = 123456  # ElementId integer
    return revit_doc.GetElement(DB.ElementId(SCHEDULE_ID))
```

### Dependency-wrapping fixture

```python
@pytest.fixture(scope="session")
def humanize_mod():
    """Expose auto-installed humanize package."""
    import humanize
    return humanize
```

### Lazy service fixture

```python
@pytest.fixture
def image_exporter():
    """Lazily import RevitDevTool service."""
    from RevitDevTool.Core import RevitImageExporter
    return RevitImageExporter
```

## Project Configuration (pyproject.toml)

Type checker stub paths (for autocompletion in any IDE):

```toml
[tool.basedpyright]
extraPaths = ["~/AppData/Roaming/RevitDevTool/2025/Stubs"]
typeCheckingMode = "off"
```

For `[tool.pytest.ini_options]` and CLI flags, see SKILL.md.


# Conftest

PEP 723 `# /// script` is **CPython `tests/run` only**. Do not put it on `test_*_ipy.py`.

```python
# /// script
# dependencies = [
#   "numpy>=2.0",
# ]
# ///
import os
import pytest

RVT_PATH = os.environ.get("REVIT_TEST_MODEL_PATH", r"C:\path\to\test_model.rvt")
```

## Fixtures

```python
@pytest.fixture(scope="session")
def revit_uiapp():
    return __revit__  # noqa: F821

@pytest.fixture(scope="session")
def revit_app(revit_uiapp):
    return revit_uiapp.Application

@pytest.fixture(scope="session")
def revit_doc(revit_uiapp):
    if not os.path.isfile(RVT_PATH):
        pytest.skip(f"{RVT_PATH} not found")
    target = os.path.normcase(os.path.abspath(RVT_PATH))
    uidoc = revit_uiapp.ActiveUIDocument
    current = uidoc.Document if uidoc else None
    if current is not None and os.path.normcase(os.path.abspath(current.PathName or "")) == target:
        return current
    return revit_uiapp.OpenAndActivateDocument(RVT_PATH).Document

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

Host / `.NET` imports stay inside fixture or test bodies. CLI flags: SKILL.md.

```toml
[tool.basedpyright]
extraPaths = ["~/AppData/Roaming/RevitDevTool/2025/Stubs"]
typeCheckingMode = "off"
```

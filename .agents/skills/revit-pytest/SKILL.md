---
name: revit-pytest
description: >
  Create and run CAD/BIM API tests using pytest via RevitDevTool Named Pipe bridge.
  Use when writing new pytest test files for Revit/AutoCAD/Civil3D API, adding test
  fixtures for host documents/elements, running tests against a live host instance,
  setting up conftest.py with PEP 723 dependencies, or asking "how do I test Revit
  API with pytest". Always run via uv in the RevitDevTool.PyTest repo.
---

# Host API Testing with pytest

## How It Works

```
Local pytest (collect) → Named Pipe → Host (PytestRunner.py) → Results → Local pytest (report)
```

Tests are collected locally, executed inside a live host via JSON-RPC over Named Pipes.

## Run (always uv)

From the **RevitDevTool.PyTest** repo root — never search system Python, never bare `pytest`:

```powershell
cd c:\Users\truon\source\repos\RevitDevTool.PyTest
uv run pytest -v
uv run pytest tests/Revit/<file>.py::test_name -v
uv run pytest --host-version=2025 -v
uv run pytest --host autocad --host-version=2026 -v
uv run pytest --host-launch --host-version=2025 -v
```

## Configure pyproject.toml

```toml
[tool.pytest.ini_options]
host_name = "revit"
host_version = "2025"
host_launch = false
host_timeout = "60"
host_launch_timeout = "180"
```

| Option | Default | Description |
|--------|---------|-------------|
| `host_name` | `"revit"` | `revit`, `autocad`, `civil3d`, etc. |
| `host_version` | — | Required when `host_launch = true` |
| `host_launch` | `false` | Force a **new** host instance |
| `host_timeout` | `"60"` | Per-test timeout (seconds) |
| `host_pipe` | — | Explicit pipe (bypass discovery) |

## conftest.py

```python
# /// script
# dependencies = [
#   "numpy>=2.0",
# ]
# ///
"""PEP 723 deps are auto-installed by RevitDevTool."""

import pytest

@pytest.fixture(scope="session")
def revit_uiapp():
    return __revit__  # noqa: F821

@pytest.fixture(scope="session")
def revit_doc(revit_uiapp):
    return revit_uiapp.ActiveUIDocument.Document

@pytest.fixture
def revit_auto_rollback():
    from RevitDevTool.Core import RevitTransactionService
    RevitTransactionService.StartChanges()
    try:
        yield RevitTransactionService
    finally:
        RevitTransactionService.RevertChanges()
```

## Tests

**Host/.NET imports MUST be inside function bodies.**

```python
def test_active_view(revit_doc):
    view = revit_doc.ActiveView
    assert view is not None

def test_wall_count(revit_doc):
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory
    walls = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    assert len(walls) > 0
```

## Behaviors

- Pipe shape: `DevTools_{Host}_{Version}_{PID}`
- Tests run on the host main thread sequentially; `__revit__` via fixtures
- `print()` is captured; plugin uses `--capture=sys`

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Import host API at module level | Move inside the test function |
| Use `__revit__` without fixture | Use `revit_uiapp` |
| System Python / bare `pytest` | `uv run pytest` from PyTest repo |
| `host_launch` without version | Set `host_version` |

## References

- [conftest-guide.md](references/conftest-guide.md)
- [test-patterns.md](references/test-patterns.md)
- [plugin-internals.md](references/plugin-internals.md)

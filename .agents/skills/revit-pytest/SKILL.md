---
name: revit-pytest
description: >
  Create and run CAD/BIM API tests using pytest via RevitDevTool Named Pipe bridge.
  Use when writing new pytest test files for Revit/AutoCAD/Civil3D API, adding test
  fixtures for host documents/elements, running tests against a live host instance,
  setting up conftest.py with PEP 723 dependencies, or asking "how do I test Revit
  API with pytest". Covers test structure, fixtures, lazy imports, transactions,
  auto-rollback, dependency injection, and CLI options.
---

# Host API Testing with pytest

## How It Works

```
Local pytest (collect) → Named Pipe → Host (PytestRunner.py) → Results → Local pytest (report)
```

Tests are collected locally by pytest, then executed remotely inside a live host process (Revit, AutoCAD, Civil3D, etc.) via JSON-RPC over Named Pipes. Results (pass/fail/skip, stdout, tracebacks) return to the local pytest session.

## 1. Project Setup

```bash
# Create project with uv (recommended)
uv init my-revit-tests
cd my-revit-tests
uv add revitdevtool_pytest
```

Or with pixi:

```bash
pixi init --format pyproject my-revit-tests
cd my-revit-tests
pixi add --pypi revitdevtool_pytest
```

## 2. Configure pyproject.toml

Minimal config — only `host_version` is required:

```toml
[tool.pytest.ini_options]
host_version = "2025"
```

Full options:

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
| `host_name` | `"revit"` | Host application (`revit`, `autocad`, `civil3d`, `plant3d`, etc.) |
| `host_version` | — | Host version (e.g. `2025`, `8.0`). Required when `host_launch = true`. |
| `host_launch` | `false` | Force-launch a **new** host instance (ignores existing). |
| `host_timeout` | `"60"` | Per-test execution timeout (seconds). |
| `host_launch_timeout` | `"120"` | Seconds to wait for host to start. |
| `host_pipe` | — | Explicit pipe name (bypass auto-discovery). |

## 3. Write conftest.py

```python
# /// script
# dependencies = [
#   "numpy>=2.0",
# ]
# ///
"""PEP 723 dependencies above are auto-installed by RevitDevTool."""

import pytest

@pytest.fixture(scope="session")
def revit_uiapp():
    return __revit__  # noqa: F821

@pytest.fixture(scope="session")
def revit_app(revit_uiapp):
    return revit_uiapp.Application

@pytest.fixture(scope="session")
def revit_doc(revit_uiapp):
    return revit_uiapp.ActiveUIDocument.Document

@pytest.fixture
def revit_auto_rollback():
    """Start undo tracking, revert after test."""
    from RevitDevTool.Core import RevitTransactionService
    RevitTransactionService.StartChanges()
    try:
        yield RevitTransactionService
    finally:
        RevitTransactionService.RevertChanges()
```

## 4. Write Tests

**Critical rule: All host/.NET imports MUST be inside function bodies.**

```python
def test_active_view(revit_doc):
    view = revit_doc.ActiveView
    print(f"Active View: {view.Name}")
    assert view is not None

def test_wall_count(revit_doc):
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

    walls = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    assert len(walls) > 0

def test_create_and_rollback(revit_doc, revit_auto_rollback):
    from Autodesk.Revit.DB import Transaction
    with Transaction(revit_doc, "Test") as t:
        t.Start()
        # modify model...
        t.Commit()
    # revit_auto_rollback reverts all changes after test
```

## 5. Run Tests

```powershell
# Preferred: uv run
uv run pytest -v

# Or pixi:
pixi run pytest -v

# Or activate venv manually:
& ".venv\Scripts\pytest.exe" -v

# Specific test:
pytest tests/test_walls.py::test_wall_count -v

# Override version for one run:
pytest --host-version=2026 -v

# Force new host instance:
pytest --host-launch --host-version=2025 -v

# Target AutoCAD:
pytest --host autocad --host-version=2026 -v

# Target Civil 3D:
pytest --host civil3d --host-version=2026 -v
```

## Key Behaviors

### Print output
`print()` inside tests is automatically captured and displayed in terminal output for both passing and failing tests. No extra flags needed.

### Connection
- Default: plugin scans for running host matching `host_version` via Named Pipe (`DevTools_{Host}_{Version}_{PID}`, e.g. `DevTools_Revit_2025_12345`, `DevTools_Rhino_8.0_9999`)
- `host_launch = true`: spawns new host, waits for its specific PID pipe, ignores existing instances
- `host_pipe = "DevTools_Revit_2025_12345"`: connect to exact pipe (skip discovery). Any `DevTools_{Host}_{Version}_{PID}` pipe works.

### Execution context
- Tests run on the host's main thread sequentially
- `__revit__` (Revit) or equivalent builtins are injected by host setup scripts (always access via fixtures)
- `--capture=sys` is used internally (fd capture doesn't work in embedded Python.NET)

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Import host API at module level | Move to inside function body |
| Use `__revit__` directly without `noqa` | Use `revit_uiapp` fixture |
| Run bare `pytest` without venv | Use `uv run pytest` or activate venv |
| Expect `host_launch` to reuse instances | It always launches NEW (use default for reuse) |
| Missing `host_version` with `host_launch` | Set `host_version` in config or CLI |

---
name: revit-pytest
description: >
  Create and run Revit API tests using pytest via RevitDevTool Named Pipe bridge.
  Use when writing new pytest test files for Revit API, adding test fixtures for
  Revit documents/elements, running tests against a live Revit instance, setting
  up conftest.py with PEP 723 dependencies, or asking "how do I test Revit API
  with pytest". Covers test structure, fixtures, lazy imports, transactions,
  auto-rollback, dependency injection, and CLI options.
---

# Revit API Testing with pytest

## How It Works

```
Local pytest (collect) → Named Pipe → Revit (PytestRunner.py) → Results → Local pytest (report)
```

Tests are collected locally by pytest, then executed remotely inside a live Revit process via JSON-RPC over Named Pipes. Results (pass/fail/skip, stdout, tracebacks) return to the local pytest session.

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

Minimal config — only `revit_version` is required:

```toml
[tool.pytest.ini_options]
revit_version = "2025"
```

Full options:

```toml
[tool.pytest.ini_options]
revit_version = "2025"
revit_launch = false
revit_timeout = "60"
revit_launch_timeout = "180"
```

| Option | Default | Description |
|--------|---------|-------------|
| `revit_version` | — | Revit year. Required when `revit_launch = true`. |
| `revit_launch` | `false` | Force-launch a **new** Revit instance (ignores existing). |
| `revit_timeout` | `"60"` | Per-test execution timeout (seconds). |
| `revit_launch_timeout` | `"120"` | Seconds to wait for Revit to start. |
| `revit_pipe` | — | Explicit pipe name (bypass auto-discovery). |

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

**Critical rule: All Revit/.NET imports MUST be inside function bodies.**

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
pytest --revit-version=2026 -v

# Force new Revit instance:
pytest --revit-launch --revit-version=2025 -v
```

## Key Behaviors

### Print output
`print()` inside tests is automatically captured and displayed in terminal output for both passing and failing tests. No extra flags needed.

### Connection
- Default: plugin scans for running Revit matching `revit_version` via Named Pipe (`Revit_{year}_{pid}`)
- `revit_launch = true`: spawns new Revit, waits for its specific PID pipe, ignores existing instances
- `revit_pipe = "Revit_2025_12345"`: connect to exact pipe (skip discovery)

### Execution context
- Tests run on Revit's main thread sequentially
- `__revit__` is a builtin injected by RevitDevTool (always access via fixtures)
- `--capture=sys` is used internally (fd capture doesn't work in embedded Python.NET)

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Import Revit API at module level | Move to inside function body |
| Use `__revit__` directly without `noqa` | Use `revit_uiapp` fixture |
| Run bare `pytest` without venv | Use `uv run pytest` or activate venv |
| Expect `revit_launch` to reuse instances | It always launches NEW (use default for reuse) |
| Missing `revit_version` with `revit_launch` | Set `revit_version` in config or CLI |

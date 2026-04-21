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

# Revit API Testing with pytest via RevitDevTool

## How It Works

1. pytest discovers tests locally as standard `.py` files
2. `revitdevtool_pytest` plugin intercepts execution via `pytest_pyfunc_call`
3. Test source is serialized and sent over Named Pipe to a live Revit process
4. RevitDevTool add-in executes the test inside Revit's pythonnet environment
5. Results map back to pytest pass/fail/skip

## Critical Rules

### Lazy imports — all Revit API imports MUST be inside function bodies

```python
# WRONG — fails at collection time (no Revit process yet)
from Autodesk.Revit.DB import FilteredElementCollector

# CORRECT — import inside the test function
def test_walls(revit_doc):
    from Autodesk.Revit.DB import FilteredElementCollector, BuiltInCategory

    walls = list(
        FilteredElementCollector(revit_doc)
        .OfCategory(BuiltInCategory.OST_Walls)
        .WhereElementIsNotElementType()
    )
    assert len(walls) > 0
```

This also applies to .NET imports (`System.Collections.Generic`, `RevitDevTool.Core`, etc.).

### `__revit__` global

`__revit__` is injected by RevitDevTool — provides `UIApplication`:

```python
def test_version():
    app = __revit__.Application  # noqa: F821
    assert "2025" in app.VersionName
```

### Fixtures (from conftest.py)

| Fixture | Scope | Provides |
|---------|-------|----------|
| `revit_uiapp` | session | `UIApplication` (`__revit__`) |
| `revit_app` | session | `Application` |
| `revit_doc` | session | Target `Document` (opens RVT if needed) |
| `revit_transaction_service` | function | `RevitTransactionService` |
| `revit_auto_rollback` | function | Start undo tracking, always revert after test |

### PEP 723 dependencies in conftest.py

Declare Python packages at the top of `conftest.py` — RevitDevTool auto-installs them:

```python
# /// script
# dependencies = [
#   "numpy>=2.0",
#   "polars>=1.0",
# ]
# ///
```

## Running Tests

```bash
pytest --revit-launch --revit-version=2025 -v     # auto-launch Revit
pytest --revit-version=2025 -v                      # detect running Revit
pytest tests/test_smoke.py -v                       # single file
pytest -k "test_wall" -v                            # by name pattern
pytest --revit-pipe=Revit_2025_12345 -v             # explicit pipe
pytest --collect-only                               # verify discovery only
```

| CLI Option | Description |
|---|---|
| `--revit-version` | Revit version year (e.g. 2025) |
| `--revit-launch` | Auto-launch Revit if no instance found |
| `--revit-timeout` | Per-test timeout seconds (default: 60) |
| `--revit-launch-timeout` | Startup timeout seconds (default: 120) |
| `--revit-pipe` | Explicit pipe name |

Set defaults in `pyproject.toml`:

```toml
[tool.pytest.ini_options]
testpaths = ["tests"]
revit_version = "2025"
revit_timeout = "60"
revit_launch = true
revit_launch_timeout = "180"
addopts = "-v --tb=short -p no:warnings"
```

`print()` in tests is captured inside Revit and returned in `CaseResult.stdout`. Use `-s` to see it live. For details, see [Plugin Internals](./references/plugin-internals.md).

## Quick Start — New Test File

1. Create `tests/test_<feature>.py`
2. Import `pytest` at top level only — all Revit imports inside functions
3. Use fixtures: `revit_doc`, `revit_app`, `revit_uiapp`
4. Use `revit_auto_rollback` if test modifies the model
5. Use `pytest.skip()` for missing prerequisites

For full examples, see [Test Patterns](./references/test-patterns.md).

## Reference Files

| File | When to Read |
|------|-------------|
| [Test Patterns](./references/test-patterns.md) | Writing new test files — examples for queries, transactions, exports, validation |
| [Conftest & Fixtures](./references/conftest-guide.md) | Setting up conftest.py, adding fixtures, PEP 723 dependency declaration |
| [Plugin Internals](./references/plugin-internals.md) | Debugging connection issues, Named Pipe protocol, suite leasing, output capture |
| [Testing pyRevit Features](./references/test-pyrevit-patterns.md) | Dual-lib architecture for testing pyRevit IronPython code with Python 3.13 |

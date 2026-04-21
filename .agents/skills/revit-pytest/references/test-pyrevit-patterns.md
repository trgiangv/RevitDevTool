# Testing pyRevit Features with RevitDevTool.PyTest

## The Dual-Lib Problem

pyRevit bundles run on **IronPython 2.7** inside Revit.
Tests run on **Python 3.13** (CPython) via RevitDevTool's pythonnet bridge.

The two runtimes are incompatible — you cannot import pyRevit lib code directly into tests. Maintain **two independent libraries** that implement the same Revit API logic, each written for its own runtime.

```
pyRevit side (IronPython 2.7)         Test side (Python 3.13)
  pushbutton/lib/                       tests/<feature>/
    models/                               model.py
    services/                             workflow.py
    constants.py                          constants.py
    ...                                   ...
```

## Workflow

1. **Write test-side logic** in `tests/<feature>/` using Python 3.13 — plain dicts, modern syntax
2. **Write and run pytest** against live Revit data until all tests pass
3. **Port validated logic to pyRevit** — translate to IronPython 2.7 compatible syntax
4. Keep both libs in sync when requirements change

For IronPython 2.7 coding conventions, see the `pyrevit-ironpython` skill.

## What to Test

Only test **Revit API logic** — anything that reads/writes the Revit model:

- Element collection and filtering
- Parameter reading/writing
- Transaction workflows (create, modify, delete elements)
- Data extraction and transformation algorithms
- Field mapping, serialization, comparison logic

## What NOT to Test

- **WPF UI** — ViewModels, Views, XAML run on Revit's UI thread, not reachable via Named Pipe
- **pyRevit-specific APIs** — `pyrevit.HOST_APP`, `pyrevit.compat`, `AdWindows` are not available in the test runtime
- **User interaction** — dialogs, notifications, button clicks

## Key Patterns

### Separate test-side helpers from test files

Keep reusable Revit API logic in `tests/<feature>/` as a helper package. Test files (`test_*.py`) import from this package. This mirrors the pyRevit `pushbutton/lib/` structure and makes porting straightforward.

### Contract-based testing

Capture Revit state as normalized dicts (a "contract") before and after operations. Compare contracts to verify correctness without depending on pyRevit code.

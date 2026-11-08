---
name: revit-pytest
description: >
  Create and run CAD/BIM API tests using pytest via RevitDevTool Named Pipe bridge.
  Use when writing pytest for Revit/AutoCAD/Civil3D, fixtures, conftest PEP 723,
  host_name/host_version/force_launch, or "how do I test Revit API with python/pytest".
---

# Host API Testing with pytest

```mermaid
flowchart LR
    Collect[Local collect] --> Pipe["DevTools_*"]
    Pipe --> Host[Host run]
    Host --> Report[Local report]
```

In this repo the package lives in `source/RevitDevTool.PyTest`; run its live suite with `pixi run live` from that folder (settings in `tests/live.ini`). In a consumer project, `uv run pytest`. Pipe is `DevTools_{Host}_{Version}_{PID}`, not `DevToolsMcp_*`.

```powershell
cd source/RevitDevTool.PyTest
pixi run live -v
pixi run live tests/Revit/<file>.py::test_name -v
pixi run live --host-version=2025 -v
pixi run live --host autocad --host-version=2026 -v
pixi run live tests/Revit_Ipy -v --host-version=2025
```

## Config

Consumer `pyproject.toml` (this repo's live suite uses the same keys in `tests/live.ini`):

```toml
[tool.pytest.ini_options]
host_name = "revit"
host_version = "2025"
force_launch = false
per_test_timeout = "60"
launch_timeout = "180"
```

`force_launch` needs `host_version`. Pipe wait: CPython `per_test × N + launch_timeout`; IPy `per_test × N`.

## CPython (`test_*.py` → `tests/run`)

Host imports **inside** the test or fixture. `__revit__` via fixtures. PEP 723 `# /// script` on `conftest.py` is installed by the host before run.

```python
def test_active_view(revit_doc):
    view = revit_doc.ActiveView
    assert view is not None
```

## IronPython (`test_*_ipy.py` → `ipytests/run`)

`unittest.TestCase`. No pytest fixtures, no PEP 723, no f-strings, never assign to `print`. Host APIs inside methods. Filename is pytest routing only.

```python
import unittest

class ActiveViewTests(unittest.TestCase):
    def test_has_view(self):
        doc = __revit__.ActiveUIDocument.Document
        self.assertIsNotNone(doc.ActiveView)
```

## Mistakes

| Mistake | Fix |
|---------|-----|
| Host API at module top | Inside the test / method |
| `__revit__` in CPython with no fixture | `revit_uiapp` / `revit_doc` |
| System Python / bare `pytest` | `pixi run live` here, `uv run pytest` in a consumer project |
| `force_launch` without version | Set `host_version` |
| Connect `DevToolsMcp_*` | Pytest pipe is `DevTools_*` |
| PEP 723 / fixtures on `test_*_ipy.py` | CPython only |

## References

- [conftest-guide.md](references/conftest-guide.md)
- [test-patterns.md](references/test-patterns.md)
- [plugin-internals.md](references/plugin-internals.md)

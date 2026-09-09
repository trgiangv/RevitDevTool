# unittest (IronPython)

Use Python's `unittest.TestCase` style for IronPython tests. The pytest package discovers the files, while the test body runs with the IronPython engine inside the Autodesk host.

## File naming

Name IronPython tests with the routing suffix:

```text
tests/test_walls_ipy.py
```

The suffix marks the file as an IronPython test. It does not make the test a CPython test and it does not enable pytest fixtures.

| Property | IronPython unittest |
| --- | --- |
| Runner command | `uv run pytest` |
| Test style | `unittest.TestCase` |
| pytest fixtures | Not supported |
| PEP 723 dependencies | Not supported |
| Debugger attach | Not supported |

## Example

```python
import unittest

class WallTests(unittest.TestCase):
    def test_active_document(self):
        self.assertIsNotNone(__revit__.ActiveUIDocument.Document)
```

Run the test through the same bridge used by CPython:

```powershell
uv run pytest --host revit --host-version 2025 -v
```

The test client discovers `test_*_ipy.py` files separately from regular `test_*.py` files. A mixed test folder is supported, while each test keeps its own Python runtime semantics.

## Engine selection

On Revit, RevitDevTool prefers the pyRevit IronPython engine when pyRevit is installed (default IronPython 2.7.12). Without pyRevit it falls back to IronPython 3.4.2. AutoCAD-family hosts use bundled IronPython 3.4.2.

## Limitations

- Do not use pytest fixtures or PEP 723 metadata in an IronPython test.
- Do not expect `debugpy` or .NET debugger attach for IronPython tests.
- Keep host API access inside valid document, transaction, and document-lock context.
- Use [pytest](/docs/testing/pytest) or CPython when modern packages or breakpoints are required.

See [Testing Overview](/docs/testing/Testing-Overview) for the shared pipe and mixed-tree behavior.

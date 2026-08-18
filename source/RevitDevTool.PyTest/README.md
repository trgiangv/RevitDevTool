# RevitDevTool.PyTest

[![PyPI version](https://img.shields.io/pypi/v/revitdevtool_pytest)](https://pypi.org/project/revitdevtool-pytest/)
[![Python](https://img.shields.io/pypi/pyversions/revitdevtool_pytest)](https://pypi.org/project/revitdevtool-pytest/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

pytest plugin for testing CAD/BIM API code inside live host applications via the RevitDevTool Named Pipe bridge.

Write tests locally with pytest; execution happens inside the host process. **Currently supported:** **Revit** and **AutoCAD family** only. Navisworks, Rhino, Tekla, and other hosts are **in progress**.

## Installation

```bash
pip install revitdevtool_pytest
# or
uv add revitdevtool_pytest
```

| Requirement | Version |
|---|---|
| Python | >= 3.10 |
| pytest | >= 9.0 |
| pywin32 | >= 311 |
| OS | Windows (Named Pipes) |
| Host | [RevitDevTool](https://github.com/trgiangv/RevitDevTool) add-in loaded |

## Supported hosts

### Supported today

| `host_name` | Application | Pipe prefix |
|---|---|---|
| `revit` | Autodesk Revit | `Revit` |
| `autocad` | AutoCAD | `AutoCad` |
| `civil3d` | Civil 3D | `Civil3D` |
| `plant3d` | Plant 3D | `Plant3D` |
| `acadarch` | AutoCAD Architecture | `AcadArch` |
| `acadmech` | AutoCAD Mechanical | `AcadMech` |
| `acadmep` | AutoCAD MEP | `AcadMep` |
| `acadelec` | AutoCAD Electrical | `AcadElec` |
| `acadmap3d` | AutoCAD Map 3D | `AcadMap3D` |

### In progress

| `host_name` | Application | Status |
|---|---|---|
| `navisworks` | Navisworks | Not validated |
| `rhino` | Rhino | Not validated |
| `tekla` | Tekla Structures | Not validated |

The plugin registry may list additional names for future work. Treat them as experimental until documented here as **Supported today**.

### Pipe names (pytest only)

The plugin connects to the **pytest/control** pipe:

`DevTools_{Host}_{Version}_{PID}`

Examples: `DevTools_Revit_2025_12345`, `DevTools_AutoCad_2026_7890`

MCP clients use a separate pipe: `DevToolsMcp_{Host}_{Version}_{PID}`. Do not point pytest at an MCP pipe.

## Quick start (consumer project)

```bash
uv init my-host-tests && cd my-host-tests
uv add revitdevtool_pytest pytest
```

`pyproject.toml`:

```toml
[tool.pytest.ini_options]
host_name = "revit"
host_version = "2025"
force_launch = false
per_test_timeout = "60"
launch_timeout = "180"
```

`tests/conftest.py`:

```python
import pytest

@pytest.fixture(scope="session")
def revit_uiapp():
    return __revit__

@pytest.fixture(scope="session")
def revit_doc(revit_uiapp):
    return revit_uiapp.ActiveUIDocument.Document
```

`tests/test_smoke.py`:

```python
def test_active_view(revit_doc):
    assert revit_doc.ActiveView is not None
```

```bash
uv run pytest -v
```

## Configuration

| Option | Default | Description |
|---|---|---|
| `host_name` | `"revit"` | Target host — use **Supported today** names only. |
| `host_version` | — | Version string (`"2025"`, `"8.0"`, …). Required for launch. |
| `force_launch` | `false` | Force a **new** host instance (skip reuse). Requires `host_version`. |
| `per_test_timeout` | `"60"` | Per-test budget (seconds). The `tests/run` pipe wait is this × collected tests. |
| `launch_timeout` | `"180"` | Wait for host pipe after launch (seconds). |
| `host_pipe` | — | Explicit pipe name (skip discovery). |

CLI flags override INI: `--host`, `--host-version`, `--force-launch`, `--per-test-timeout`, `--launch-timeout`, `--host-pipe`.

### Connection behavior

1. **`force_launch = false` (default):** scan for `DevTools_*` pipes matching `host_name` / `host_version`, reuse a free instance (suite leasing), or reconnect to a leased PID.
2. **No matching instance:** auto-launch host when `host_version` is set (unless `--host-pipe` pins an existing pipe).
3. **`force_launch = true`:** always spawn a new process and wait for its pipe.

### Print output

Captured `print()` from in-host tests is shown for passing tests (same as `-rP`). No extra flags needed.

## Writing tests

- Put **host API imports inside functions** — collection runs on your machine, execution in the host.
- Use fixtures for `__revit__`, documents, and rollback helpers.
- Declare suite dependencies with **PEP 723** in `conftest.py`; RevitDevTool installs them before run:

```python
# /// script
# dependencies = ["numpy>=2.0", "polars>=1.0"]
# ///
```

### Revit rollback fixture

```python
@pytest.fixture
def revit_auto_rollback(revit_transaction_service):
    revit_transaction_service.StartChanges()
    try:
        yield revit_transaction_service
    finally:
        revit_transaction_service.RevertChanges()
```

## How it works

1. Local pytest collects tests.
2. Plugin connects to `DevTools_{Host}_{Version}_{PID}`.
3. Node IDs sent via JSON-RPC method `tests/run` (`BridgeMessage` length-prefixed frames).
4. Host runs `PytestRunner.py` embedded in RevitDevTool (`PytestExecutionService` on main thread).
5. Results and optional progress notifications return to the client.
6. Plugin maps `CaseResult` → standard pytest reports.

## Developing this repository

Clone and run integration tests against a live host:

```powershell
cd RevitDevTool.PyTest
uv run pytest -v                          # Revit suite (default)
uv run pytest tests/Revit/test_smoke.py -v
uv run pytest --host civil3d --host-version 2026  # after switching testpaths/host in pyproject.toml
```

### Layout

| Path | Purpose |
|---|---|
| `src/revitdevtool_pytest/` | Plugin source (published wheel) |
| `tests/Revit/` | Revit integration tests + `schedule/` helpers |
| `tests/Civil3d/` | Civil 3D / AutoCAD API tests |

Revit suite settings in `pyproject.toml`: `testpaths = ["tests/Revit"]`, `pythonpath = ["tests/Revit"]` for local `schedule` imports.

Optional env: `REVIT_TEST_MODEL_PATH` — path to `.rvt` for `revit_doc` fixture (default `F:\Project1.rvt`).

## IDE integration

**VS Code / Cursor** — `python.testing.pytestEnabled: true`. Plugin detects `vscode_pytest` / `TEST_RUN_PIPE` and disables CLI streaming to avoid duplicate tree entries.

**PyCharm** — enable pytest as test runner.

## License

[MIT](LICENSE)

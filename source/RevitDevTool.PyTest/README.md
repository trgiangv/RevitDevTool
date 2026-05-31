# RevitDevTool.PyTest

[![PyPI version](https://img.shields.io/pypi/v/RevitDevTool.PyTest)](https://pypi.org/project/RevitDevTool.PyTest/)
[![Python](https://img.shields.io/pypi/pyversions/RevitDevTool.PyTest)](https://pypi.org/project/RevitDevTool.PyTest/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

pytest plugin for testing CAD/BIM API code inside host applications via RevitDevTool Named Pipe bridge.
Supports Revit, AutoCAD-family, and any host exposing a DevToolsPipeServer pipe.
Tests run inside a live host process — write standard pytest, execute remotely.

## Installation

```bash
pip install revitdevtool_pytest
```

## Dependencies

| Package | Version |
|---|---|
| Python | >= 3.10 |
| pytest | >= 9.0 |
| pywin32 | >= 311 |

## Requirements

- Windows (Named Pipes)
- A host application with [RevitDevTool](https://github.com/trgiangv/RevitDevTool) add-in installed and loaded

## Supported Hosts

### Pre-registered

| Host Name | Application | Pipe Prefix |
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
| `navisworks` | Navisworks | `Navisworks` |
| `rhino` | Rhino | `Rhino` |
| `tekla` | Tekla Structures | `Tekla` |

### Any host

Any host name works — unknown names get a fallback config using the name as pipe prefix. Connect via auto-discovery or `--host-pipe`.

### Pipe Name Format

Pipes follow `{Host}_{Version}_{PID}` (e.g. `Revit_2025_12345`, `Rhino_8.0_9999`). Version can be any format — year, semver, etc.

## Project Setup

**Recommended:** scaffold your project with [uv](https://docs.astral.sh/uv/) or [pixi](https://pixi.sh/):

```bash
# uv
uv init my-revit-tests
cd my-revit-tests
uv add revitdevtool_pytest

# pixi (uses pyproject.toml as manifest)
pixi init --format pyproject my-revit-tests
cd my-revit-tests
pixi add --pypi revitdevtool_pytest
```

## Configuration

Add plugin settings to `[tool.pytest.ini_options]` in your `pyproject.toml`:

```toml
[tool.pytest.ini_options]
host_name = "revit"
host_version = "2025"
host_launch = false
host_launch_timeout = "180"
```

| Option | Type | Default | Description |
|---|---|---|---|
| `host_name` | string | `"revit"` | Host application name (see Supported Hosts above). |
| `host_version` | string | — | Host version (e.g. `"2025"`, `"8.0"`). Required when `host_launch = true`. |
| `host_launch` | bool | `false` | Force-launch a **new** host instance (skip reusing existing free instances). |
| `host_timeout` | string | `"60"` | Per-test execution timeout in seconds. |
| `host_launch_timeout` | string | `"120"` | Seconds to wait for host to start. |
| `host_pipe` | string | — | Explicit pipe name (bypasses auto-discovery). |

CLI flags override INI settings:

```bash
# Revit
pytest --host-launch --host-version=2025 -v

# AutoCAD
pytest --host autocad --host-version=2026 -v

# Civil 3D
pytest --host civil3d --host-version=2026 -v
```

### Connection Behavior

- **Default** (`host_launch = false`): plugin auto-discovers a running host instance matching `host_version` via Named Pipe scan. If none found, exits with error.
- **Force launch** (`host_launch = true`): always spawns a new host process, waits for its pipe to appear, then connects. Existing instances are ignored.

### Print Output

Captured `print()` output from tests running inside the host is automatically shown in the terminal for passing tests (equivalent to `-rP`). No extra flags needed.

## Usage

### Fixtures

Define fixtures in `conftest.py` that provide host API objects:

```python
import pytest

# Revit fixtures
@pytest.fixture(scope="session")
def revit_uiapp():
    return __revit__  # UIApplication injected by RevitDevTool

@pytest.fixture(scope="session")
def revit_app(revit_uiapp):
    return revit_uiapp.Application

@pytest.fixture(scope="session")
def revit_doc(revit_uiapp):
    return revit_uiapp.ActiveUIDocument.Document
```

### Writing Tests

```python
def test_active_view(revit_doc):
    view = revit_doc.ActiveView
    print(f"Active View: {view.Name}")
    assert view.Name is not None

def test_revit_version(revit_app):
    assert "2025" in revit_app.VersionName
```

### PEP 723 Dependencies

Declare test-suite dependencies in `conftest.py` using PEP 723 metadata. RevitDevTool auto-installs them before test execution:

```python
# /// script
# dependencies = [
#   "numpy>=2.0",
#   "polars>=1.0",
# ]
# ///
```

### Running Tests

```bash
# With pyproject.toml configured:
pytest

# Override version for a single run:
pytest --host-version=2026 -v

# Target a different host:
pytest --host autocad --host-version=2026 -v

# Using uv or pixi:
uv run pytest
pixi run pytest
```

## How It Works

1. pytest discovers and collects tests locally
2. Plugin intercepts `pytest_runtestloop`, connects to the host via Named Pipe
3. Test nodeids are sent to the host's `PytestRunner.py` (embedded in RevitDevTool)
4. Tests execute inside the host's Python.NET environment with full API access
5. Results (pass/fail/skip, stdout, stderr, tracebacks) are returned via pipe
6. Plugin maps results back to standard pytest reports

## IDE Integration

### VS Code / Cursor

```json
{
    "python.testing.pytestEnabled": true,
    "python.testing.pytestArgs": ["tests"]
}
```

### PyCharm

Enable pytest as the test runner. Plugin auto-detects IDE adapters and disables streaming to avoid duplicate results in the test tree.

## License

[MIT](LICENSE)

# RevitDevTool.PyTest

[![PyPI version](https://img.shields.io/pypi/v/RevitDevTool.PyTest)](https://pypi.org/project/RevitDevTool.PyTest/)
[![Python](https://img.shields.io/pypi/pyversions/RevitDevTool.PyTest)](https://pypi.org/project/RevitDevTool.PyTest/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

pytest plugin for testing Revit API code via RevitDevTool Named Pipe bridge.
Tests run inside a live Revit process — write standard pytest, execute remotely.

## Installation

```bash
pip install revitdevtool_pytest
```

```python
import revitdevtool_pytest
```

## Dependencies

| Package | Version |
|---|---|
| Python | >= 3.10 |
| pytest | >= 9.0 |
| pywin32 | >= 311 |

## Requirements

- Windows (Named Pipes)
- Revit with [RevitDevTool](https://github.com/trgiangv/RevitDevTool) add-in installed

## Project Setup

**Recommended:** scaffold your project with [uv](https://docs.astral.sh/uv/) or [pixi](https://pixi.sh/).

```bash
# uv
uv init my-revit-tests
cd my-revit-tests
uv add revitdevtool_pytest

# pixi
pixi init my-revit-tests
cd my-revit-tests
pixi add revitdevtool_pytest
```

Both tools automatically manage virtual environments, lock files, and `pyproject.toml` — no separate `pip` setup needed.

## Configuration

Add plugin settings to `[tool.pytest.ini_options]` in your `pyproject.toml`:

```toml
[tool.pytest.ini_options]
testpaths = ["tests"]
revit_version = "2025"
revit_launch = true
revit_launch_timeout = "180"
```

> With `uv` or `pixi`, run tests via `uv run pytest` or `pixi run pytest`.

All options are settable via `[tool.pytest.ini_options]` or standard INI files (`pytest.ini`, `tox.ini`, `setup.cfg`):

| Option | Type | Default | Description |
|---|---|---|---|
| `revit_version` | string | — | Revit version year (e.g. `"2025"`). Required when `revit_launch = true`. |
| `revit_launch` | bool | `false` | Auto-launch Revit if no running instance found. |
| `revit_timeout` | string | `"60"` | Per-test execution timeout in seconds. |
| `revit_launch_timeout` | string | `"120"` | Seconds to wait for Revit to start. |
| `revit_pipe` | string | — | Explicit pipe name (bypasses auto-discovery). |

CLI flags override INI settings for one-off runs:

```bash
pytest --revit-launch --revit-version=2025 -v
```

## Usage

```python
def test_revit_version():
    app = __revit__.Application
    assert "2025" in app.VersionName
```

```bash
# With pyproject.toml configured, just run:
pytest

# Or override for a single run:
pytest --revit-version=2026 -v
```

## How It Works

1. pytest discovers tests locally as usual
2. Plugin intercepts execution via `pytest_runtestloop`
3. Test source is serialized and sent over Named Pipe to Revit
4. RevitDevTool add-in executes the test inside Revit's Python (pythonnet) environment
5. Results are mapped back to pytest pass/fail/skip

## VSCode Integration

Add to `.vscode/settings.json`:

```json
{
    "python.testing.pytestEnabled": true,
    "python.testing.pytestArgs": [
        "tests"
    ]
}
```

CLI args can go in `pytestArgs` if not configured in `pyproject.toml`:

```json
{
    "python.testing.pytestArgs": [
        "--revit-launch",
        "--revit-version=2025",
        "tests"
    ]
}
```

## License

[MIT](LICENSE)

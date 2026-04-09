# RevitDevTool.PyTest

[![PyPI version](https://img.shields.io/pypi/v/RevitDevTool.PyTest)](https://pypi.org/project/RevitDevTool.PyTest/)
[![Python](https://img.shields.io/pypi/pyversions/RevitDevTool.PyTest)](https://pypi.org/project/RevitDevTool.PyTest/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

pytest plugin for testing Revit API code via RevitDevTool Named Pipe bridge.
Tests run inside a live Revit process — write standard pytest, execute remotely.

## Installation

```bash
pip install RevitDevTool.PyTest
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

## Usage

```python
def test_revit_version():
    app = __revit__.Application
    assert "2025" in app.VersionName
```

```bash
# Auto-detect running Revit 2025
pytest --revit-version=2025 -v

# Auto-launch Revit 2025 if not running
pytest --revit-launch --revit-version=2025 -v
```

## CLI Options

| Option | Description |
|---|---|
| `--revit-version` | Revit version year (e.g. 2025). Required with `--revit-launch`. |
| `--revit-launch` | Auto-launch Revit if no running instance found. |
| `--revit-timeout` | Per-test timeout in seconds (default: 60). |
| `--revit-launch-timeout` | Revit startup timeout in seconds (default: 120). |
| `--revit-pipe` | Explicit pipe name (bypasses auto-discovery). |

## How It Works

1. pytest discovers tests locally as usual
2. The plugin intercepts test execution via `pytest_pyfunc_call`
3. Test source code is serialized and sent over Named Pipe to Revit
4. RevitDevTool add-in executes the test inside Revit's Python (pythonnet) environment
5. Results are mapped back to pytest pass/fail/skip

## VSCode Integration

Add to `.vscode/settings.json`:

```json
{
    "python.testing.pytestEnabled": true,
    "python.testing.pytestArgs": [
        "--revit-launch",
        "--revit-version=2025",
        "tests"
    ]
}
```

## License

[MIT](LICENSE)

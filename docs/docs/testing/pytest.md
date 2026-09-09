# pytest

Run CPython pytest tests inside a live Revit or AutoCAD-family host through the RevitDevTool pytest bridge. The client package is `revitdevtool_pytest` `0.4.0`. Collection and reporting happen in the local Python process; Revit API code executes in the host.

## Install and configure

From the test project, add the pinned client package and run through `uv`:

```powershell
uv add "revitdevtool_pytest==0.4.0"
uv run pytest --host revit --host-version 2025 -v
```

The test client connects to the selected host automatically. Choose the host and version with the command-line options shown below.

Example configuration:

```toml
[tool.pytest.ini_options]
testpaths = ["tests"]
python_files = ["test_*.py"]
```

## Fixtures and host context

Use bridge fixtures for the host, active document, and cleanup. Tests should leave the document in a predictable state and perform writes inside valid transactions.

```python
def test_active_document(revit):
    assert revit.active_document is not None
```

Useful options include `--force-launch`, `--launch-timeout`, `--per-test-timeout`, and `--host-pipe` for connecting to a known host.

## Debugging

CPython starts `debugpy` during initialization. Attach VS Code to the port shown by RevitDevTool, usually `5678`, before running the test. See [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Host API import fails locally | The test must execute through the bridge; host API code cannot run in ordinary external Python |
| No host found | Start the host with RevitDevTool loaded or use `--force-launch` and `--host-version` |
| Fixture unavailable | Check the installed client package and fixture name in the project configuration |
| Test suite is locked | End the other pytest process for the same host, version, and workspace |

For the full wire and fixture reference, see [Testing Overview](/docs/testing/Testing-Overview).

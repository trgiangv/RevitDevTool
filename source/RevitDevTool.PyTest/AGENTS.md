# AGENTS

## Overview

This is the pytest client plugin for RevitDevTool. It bridges local pytest with a live Revit process via Named Pipe JSON-RPC.

## Architecture

```
Local pytest (collect) → Named Pipe → Revit process (PytestRunner.py) → Results → Local pytest (report)
```

### Module Map

| Module | Role |
|--------|------|
| `plugin.py` | Hook orchestrator — lifecycle, options, bridge setup |
| `connection.py` | Bridge lifecycle — discover, connect, lease, launch |
| `bridge.py` | Wire protocol — Named Pipe framing, request/response |
| `discovery.py` | Pipe scan, Revit registry lookup, process launch |
| `reporting.py` | Map remote CaseResult → pytest TestReport |
| `suite_leasing.py` | Cross-process Revit instance allocation (file-based) |
| `suite_lock.py` | Windows mutex for same-suite concurrency guard |
| `dialog_resolver.py` | Auto-dismiss Revit startup dialogs via Win32 |
| `models.py` | Wire protocol data models (mirrors C# PytestContracts) |
| `constants.py` | Shared constants, option names, defaults |

### Revit-Side (in RevitDevTool repo)

| File | Role |
|------|------|
| `PytestRunner.py` | Embedded script — runs `pytest.main()` inside Revit |
| `SetupRevit.py` | Runtime setup — API refs, I/O redirection |
| `PytestExecutionService.cs` | C# orchestrator — receives pipe request, invokes Python |
| `PytestContracts.cs` | C# wire models (mirrors `models.py`) |

## Key Design Decisions

- **Dual pytest model**: Local pytest collects tests; remote pytest executes them. This enables IDE integration (test tree, navigation) while executing in Revit's thread.
- **`--capture=sys`**: Required because fd-level capture (`os.dup2`) doesn't work in embedded Python.NET.
- **`--disable-plugin-autoload`**: Prevents third-party plugins from interfering with in-Revit execution.
- **`sys.__pytest_running__`**: Flag set by PytestRunner to prevent SetupRevit from hijacking stdout/stderr during test runs.
- **Streaming vs batch**: CLI gets real-time progress notifications; IDE adapters get one batch to avoid double-counting.
- **Suite leasing**: File-based lease store binds suite → Revit PID across processes. Mutex prevents same-suite parallel runs.
- **force_launch**: When enabled, spawns new Revit and waits for its exact PID pipe (ignores existing instances).

## Running Tests

```bash
# From project root with .venv activated:
pytest

# Or via uv / pixi:
uv run pytest
pixi run pytest

# Specific test:
pytest tests/test_active_state.py::test_active_view_info -v
```

Plugin auto-enables `-rP` (show captured stdout for passing tests).

## Configuration

All options in `pyproject.toml` under `[tool.pytest.ini_options]`:

```toml
revit_version = "2025"        # Required for launch
revit_launch = false           # true = always spawn new instance
revit_timeout = "60"           # Per-test timeout (seconds)
revit_launch_timeout = "180"   # Startup wait (seconds)
revit_pipe = ""                # Explicit pipe (bypass discovery)
```

## Fixtures Pattern

```python
# conftest.py
@pytest.fixture(scope="session")
def revit_uiapp():
    return __revit__  # UIApplication injected by host

@pytest.fixture(scope="session")
def revit_doc(revit_uiapp):
    return revit_uiapp.ActiveUIDocument.Document

@pytest.fixture
def revit_auto_rollback(revit_transaction_service):
    """Start undo tracking, revert after test."""
    revit_transaction_service.StartChanges()
    try:
        yield revit_transaction_service
    finally:
        revit_transaction_service.RevertChanges()
```

## Common Traps

- Tests execute **inside Revit**, not locally. `import` statements for Revit API (`Autodesk.Revit.DB`) only work at test runtime.
- `__revit__` is a builtin injected by SetupRevit — always access via fixtures, not direct import.
- Revit API requires main-thread access. All tests run sequentially via FIFO queue (`IHostContextExecutor`).
- Named Pipe format: `Revit_{year}_{pid}` (e.g. `Revit_2025_12345`).
- PEP 723 dependencies in `conftest.py` are auto-installed by `PytestDependencyService` before execution.
- `force_launch` requires `revit_version` to be set — otherwise exits with config error.
- Print output inside tests is captured by pytest's `--capture=sys` mechanism and returned via `CaseResult.stdout`.

## Change Rules

- Wire protocol changes must update both `models.py` (Python) and `PytestContracts.cs` (C#).
- Adding CLI options: register in both `pytest_addoption` (CLI) and `parser.addini` (INI).
- Connection logic is stateless by design — all functions receive parameters, no module-level mutable state except `plugin.py` globals.
- After modifying connection/discovery logic, manually test with both `revit_launch = true` and `false`.

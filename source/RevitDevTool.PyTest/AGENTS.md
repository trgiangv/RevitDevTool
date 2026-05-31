# AGENTS

## Overview

This is the pytest client plugin for RevitDevTool. It bridges local pytest with a live host process via Named Pipe JSON-RPC. Supports Revit, AutoCAD-family, and any host that exposes a `DevToolsPipeServer` pipe.

## Architecture

```
Local pytest (collect) → Named Pipe → Host process (PytestRunner.py) → Results → Local pytest (report)
```

### Module Map

| Module | Role |
|--------|------|
| `plugin.py` | Hook orchestrator — lifecycle, options, bridge setup |
| `connection.py` | Bridge lifecycle — discover, connect, lease, launch |
| `bridge.py` | Wire protocol — Named Pipe framing, request/response |
| `discovery.py` | Pipe scan, host registry lookup, process launch |
| `reporting.py` | Map remote CaseResult → pytest TestReport |
| `suite_leasing.py` | Cross-process host instance allocation (file-based) |
| `suite_lock.py` | Windows mutex for same-suite concurrency guard |
| `dialog_resolver.py` | Auto-dismiss host startup dialogs via Win32 |
| `models.py` | Wire protocol data models (mirrors C# PytestContracts) |
| `constants.py` | Shared constants, option names, host registry, defaults |

### Host-Side (in RevitDevTool repo)

| File | Role |
|------|------|
| `PytestRunner.py` | Embedded script — runs `pytest.main()` inside host |
| `SetupRevit.py` / `SetupAcad.py` | Runtime setup — API refs, I/O redirection |
| `PytestExecutionService.cs` | C# orchestrator — receives pipe request, invokes Python |
| `PytestContracts.cs` | C# wire models (mirrors `models.py`) |

## Multi-Host Support

The plugin supports any host that registers a DevToolsPipeServer Named Pipe.

### Pre-registered Hosts

| Host Name | Pipe Prefix | Executable | Product ID |
|-----------|------------|------------|------------|
| `revit` | `Revit` | `Revit.exe` | — |
| `autocad` | `AutoCad` | `acad.exe` | `01` |
| `civil3d` | `Civil3D` | `acad.exe` | `00` |
| `plant3d` | `Plant3D` | `acad.exe` | `17` |
| `acadarch` | `AcadArch` | `acad.exe` | `04` |
| `acadmech` | `AcadMech` | `acad.exe` | `05` |
| `acadmep` | `AcadMep` | `acad.exe` | `06` |
| `acadelec` | `AcadElec` | `acad.exe` | `07` |
| `acadmap3d` | `AcadMap3D` | `acad.exe` | `02` |
| `navisworks` | `Navisworks` | — | — |
| `rhino` | `Rhino` | — | — |
| `tekla` | `Tekla` | — | — |

Hosts without an `exe_name` (e.g. Rhino, Tekla) can only be connected via pipe auto-discovery or `--host-pipe`. Any unknown host name also gets a fallback config using the host name as pipe prefix.

### Pipe Name Format

Pipes follow `{Host}_{Version}_{PID}` — mirrors C# `InstanceManager` pattern `^\w+_[^_]+_\d+$`.

Examples:
- `Revit_2025_12345` (year-based version)
- `AutoCad_2026_7890` (year-based)
- `Rhino_8.0_9999` (semver)
- `Tekla_2024.1_1111` (dotted version)

Version is any non-underscore string — not limited to 4-digit years.

### Executable Discovery

- **Revit**: `HKLM\SOFTWARE\Autodesk\Revit\Autodesk Revit {version}` → `InstallationLocation`
- **AutoCAD family**: `HKLM\SOFTWARE\Autodesk\AutoCAD` → enumerate releases → match product ID via `ACAD-XXNN` pattern → `GlobUPILocation` / `AcadLocation`
- **Generic**: Hosts with `registry_key` + `registry_value` use registry lookup, then filesystem fallback under `Program Files\Autodesk\`
- **No exe_name**: Discovery returns `None`; connect via existing pipes only

## Key Design Decisions

- **Dual pytest model**: Local pytest collects tests; remote pytest executes them. This enables IDE integration (test tree, navigation) while executing in the host's thread.
- **`--capture=sys`**: Required because fd-level capture (`os.dup2`) doesn't work in embedded Python.NET.
- **`--disable-plugin-autoload`**: Prevents third-party plugins from interfering with in-host execution.
- **`sys.__pytest_running__`**: Flag set by PytestRunner to prevent setup scripts from hijacking stdout/stderr during test runs.
- **Streaming vs batch**: CLI gets real-time progress notifications; IDE adapters get one batch to avoid double-counting.
- **Suite leasing**: File-based lease store binds suite → host PID across processes. Mutex prevents same-suite parallel runs.
- **force_launch**: When enabled, spawns new host and waits for its exact PID pipe (ignores existing instances).
- **Open host registry**: Unknown host names get a fallback `HostConfig(pipe_prefix=host_name)` — any host exposing a DevToolsPipeServer pipe works without pre-registration.

## Running Tests

```bash
# From project root with .venv activated:
pytest

# Or via uv / pixi:
uv run pytest
pixi run pytest

# Specific test:
pytest tests/test_active_state.py::test_active_view_info -v

# Target a different host:
pytest --host autocad --host-version 2026
pytest --host rhino --host-version 8.0
```

Plugin auto-enables `-rP` (show captured stdout for passing tests).

## Configuration

All options in `pyproject.toml` under `[tool.pytest.ini_options]`:

```toml
host_name = "revit"            # Host to connect to (revit, autocad, civil3d, rhino, etc.)
host_version = "2025"          # Required for launch (accepts any string: year, semver, etc.)
host_launch = false            # true = always spawn new instance
host_timeout = "60"            # Per-test timeout (seconds)
host_launch_timeout = "180"    # Startup wait (seconds)
host_pipe = ""                 # Explicit pipe (bypass discovery)
```

CLI equivalents: `--host`, `--host-version`, `--host-timeout`, `--host-pipe`, `--host-launch`, `--host-launch-timeout`.

## Fixtures Pattern

```python
# conftest.py (Revit)
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

- Tests execute **inside the host**, not locally. `import` statements for host APIs only work at test runtime.
- `__revit__` (Revit) or equivalent builtins are injected by host setup scripts — always access via fixtures.
- Host APIs require main-thread access. All tests run sequentially via FIFO queue (`IHostContextExecutor`).
- Named Pipe format: `{Host}_{Version}_{PID}` (e.g. `Revit_2025_12345`, `Rhino_8.0_9999`). Version is any non-underscore string.
- PEP 723 dependencies in `conftest.py` are auto-installed by `PytestDependencyService` before execution.
- `--host-launch` requires `--host-version` to be set — otherwise exits with config error.
- Print output inside tests is captured by pytest's `--capture=sys` mechanism and returned via `CaseResult.stdout`.

## Change Rules

- Wire protocol changes must update both `models.py` (Python) and `PytestContracts.cs` (C#).
- Adding CLI options: register in both `pytest_addoption` (CLI) and `parser.addini` (INI).
- Connection logic is stateless by design — all functions receive parameters, no module-level mutable state except `plugin.py` globals.
- After modifying connection/discovery logic, manually test with both `host_launch = true` and `false`.
- Host registry changes: keep `HOST_REGISTRY` in `constants.py` in sync with `HostApp` enum and `AcadPathResolver.ProductIdMap` in the C# codebase.
- Any host with a DevToolsPipeServer pipe can be reached without pre-registration (fallback config).

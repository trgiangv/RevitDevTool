# Changelog

All notable changes to this project will be documented in this file.

## [0.3.1] - 2026-08-18

### Changed

- **CLI/INI options renamed** to match RevitDevTool.TestAdapter:
  `--host-launch` / `host_launch` → `--force-launch` / `force_launch`;
  `--host-timeout` / `host_timeout` → `--per-test-timeout` / `per_test_timeout`;
  `--host-launch-timeout` / `host_launch_timeout` → `--launch-timeout` / `launch_timeout`.
  `--host` and `--host-version` stay. `per_test_timeout` is the per-test budget;
  the `tests/run` pipe wait is that value times collected tests.

## [0.3.0] - 2026-07-07

### Changed

- Update pipe name with prefix **DevTools_**


## [0.3.0] - 2026-05-31

### Added

- **Multi-host support:** plugin now supports Revit, AutoCAD, Civil3D, Plant3D, Navisworks, Rhino, Tekla, and any host exposing a DevToolsPipeServer Named Pipe.
- **`--host` option:** new CLI flag and `host_name` INI key to select the target host application (default: `revit`).
- **`HostConfig` dataclass:** centralized executable discovery and launch configuration per host product, stored in `HOST_REGISTRY`.
- **AutoCAD-family executable discovery:** registry-based resolver mirrors C# `AcadPathResolver` — enumerates `HKLM\SOFTWARE\Autodesk\AutoCAD` releases, matches product IDs (`ACAD-XXNN`), resolves install path via `GlobUPILocation` / `AcadLocation`.
- **Open host registry:** `get_host_config()` returns a fallback `HostConfig(pipe_prefix=host_name)` for unknown host names — any host with a DevToolsPipeServer pipe works without pre-registration.
- **Multi-host dialog resolver:** `StartupDialogResolver` keywords expanded to cover AutoCAD, Civil, and Plant startup dialogs.

### Changed

- **CLI/INI options renamed:** all `--revit-*` / `revit_*` options replaced with `--host-*` / `host_*` equivalents (`--host-version`, `--host-timeout`, `--host-pipe`, `--host-launch`, `--host-launch-timeout`).
- **Flexible pipe pattern:** `PIPE_PATTERN` changed from `^\w+_\d{4}_\d+$` to `^\w+_[^_]+_\d+$` — version segment accepts any non-underscore string (year, semver, dotted), matching C# `InstanceManager`.
- **`HostInstance.version` type:** changed from `int` to `str` to support non-year version formats (e.g. `8.0`, `2024.1`, `v3.2.1`).
- **`--host-version` accepts string:** no longer restricted to 4-digit year integers.
- **`RevitBridge` → `HostBridge`:** bridge class renamed for host-agnostic semantics.
- **`RevitInstance` → `HostInstance`:** discovery dataclass renamed.
- **`find_revit_pipes()` → `find_host_pipes()`:** accepts optional `host_name` filter; returns all matching pipes including unregistered prefixes.
- **`find_revit_path()` → `find_host_executable()`:** dispatches to Revit registry, AutoCAD-family registry, or returns `None` for hosts without exe discovery.
- **`start_revit()` → `start_host()`:** uses `HostConfig.launch_args` per host.
- **`wait_for_revit_pipe()` → `wait_for_host_pipe()`:** host-agnostic pipe polling.
- **`HostConfig.exe_name` optional:** hosts without a known executable (Rhino, Tekla, etc.) connect via pipe auto-discovery or `--host-pipe` only.
- **`is_process_alive()`:** handles `exe_name=None` gracefully — checks PID liveness without executable name verification.

### Removed

- **`_opt_int()` helper:** version is now parsed as string, no integer conversion needed.

### Notes

- `0.3.0` is a breaking change from `0.2.x`: all CLI flags and INI keys are renamed. Existing `revit_*` / `--revit-*` options are no longer recognized.
- The pipe pattern is now aligned with the C# `InstanceManager.HostPipePattern` regex (`^\w+_[^_]+_\d+$`).
- Revit users: add `host_name = "revit"` to `pyproject.toml` (or rely on the default).

## [0.2.0] - 2026-05-31

### Added

- **Auto print capture:** plugin automatically enables `-rP` behavior — captured `print()` output is shown for both passing and failing tests without any user configuration.
- **PID-aware pipe discovery:** `wait_for_revit_pipe` can now wait for a specific process ID, ensuring `force_launch` connects to the newly spawned instance only.

### Changed

- **`revit_launch` → force_launch semantics:** renamed internal parameter from `prefer_fresh` to `force_launch`. When enabled, the plugin disconnects any existing bridge, spawns a new Revit instance, and waits for that exact PID's pipe — never reuses existing instances.
- **`_opt_bool` fix:** corrected boolean option parsing so `revit_launch = true` in `pyproject.toml` is properly respected when no CLI flag is explicitly passed.
- **Type annotations refactor:** resolved false-positive IDE warnings across `bridge.py`, `connection.py`, `suite_leasing.py`, and `dialog_resolver.py`.
- **`SetupRevit.py` (Revit-side):** I/O hijacking is now conditional on `sys.__pytest_running__` flag — prevents conflict with pytest capture during test execution.
- **`PytestRunner.py` (Revit-side):** sets `sys.__pytest_running__ = True` during execution; top-level error handling wraps entire `_run()` to always return structured JSON.

### Removed

- **`discover_tests()`** method from `bridge.py` (client-side dead code; server endpoint remains).
- **`DiscoverRequest` / `DiscoverResponse`** classes from `models.py` (unused by client).
- **`BRIDGE_METHOD_TESTS_DISCOVER`** constant (no longer referenced).
- **`resolve_existing()` / `get_suite_process_id()` / `_prune_stale()`** from `suite_leasing.py` (unused).
- **`launch_revit()`** from `discovery.py` (replaced by `start_revit` + PID tracking).
- **`addopts = "-rP"`** from `pyproject.toml` (now automatic via plugin).

### Fixed

- **`revit_launch = true` in INI ignored:** `_opt_bool` now correctly reads INI value when CLI flag is not explicitly set.

## [0.1.0] - 2026-05-03

### Added

- **Real-time streaming:** test progress notifications streamed from Revit to the pytest client during execution.
- **Suite leasing:** Windows Mutex-based exclusive access prevents multiple pytest processes from connecting to the same Revit instance.
- **Test discovery:** `PytestRunRequest` + `_BridgePlugin` integration enables `--collect-only` discovery without executing tests.
- **Reporting module:** remote `CaseResult[]` mapping back to pytest `TestReport`, with `run_remote_session` orchestrating discover + run.
- **Connection module:** centralized `ensure_bridge()` with auto-connect, lease management, and Revit auto-launch flow.
- **Schedule API tests:** comprehensive test suite for Revit schedule operations (copy, field mapping, header, template, custom workflow).
- **Image export tests:** `test_image_export.py` covering `RevitImageExporter`.
- **Document & transaction tests:** `test_revit_document.py`, `test_revit_transactions.py`.
- **Session cleanliness tests:** `test_session_cleanliness.py`, `test_active_state.py` verifying state isolation between runs.
- **Streaming tests:** `test_streaming.py` validating progress notification flow.
- **Dependencies tests:** `test_dependencies.py` verifying package resolution.
- **GitNexus integration:** AGENTS.md, CLAUDE.md, and CLI skills for graph-based code intelligence.
- **Project setup guide:** recommended `uv` and `pixi` workflows in README.

### Changed

- **Architecture rewrite:** `plugin.py` restructured — execution now intercepted via `pytest_runtestloop` instead of `pytest_pyfunc_call`. Remote session orchestrates discover + run in a single round-trip.
- **Configuration:** primary config via `[tool.pytest.ini_options]` in `pyproject.toml` — CLI flags remain as overrides.
- **`bridge.py`:** expanded with `discover_tests()`, `run_tests()`, notification dispatch, frame protocol hardening.
- **`models.py`:** new `DiscoverRequest`, `RunRequest`, `RunResponse`, `CaseResult`, `CollectionError` contracts.
- **`constants.py`:** centralized all bridge methods, option names, timeouts, pipe patterns.
- **`pyproject.toml`:** bumped `revit_launch_timeout` to 180s, added `revit_timeout` default.

### Removed

- **`serializer.py`:** deprecated — test code serialization replaced by structured discovery + run flow.
- **`.vscode/settings.json`:** removed from repo; config lives in `pyproject.toml`.

### Notes

- `0.1.0` represents a major architectural shift from single-test relay to session-oriented orchestration. The plugin now discovers tests remotely, runs them in batch, and streams progress back in real-time.
- Suite leasing ensures safe concurrent usage across multiple test suits targeting the same Revit year.

## [0.0.3] - 2026-04-10

### Fixed

- Fixed Revit auto-launch dialog handling by starting the startup dialog resolver immediately after spawning Revit, before waiting for the named pipe.
- Fixed startup dialog button selection to prefer safe allow-list actions such as `Always Load` and `Load Once`.
- Prevented accidental clicks on destructive dialog actions such as `Do Not Load`, `Cancel`, and `No`.

### Notes

- This release focuses on reliable auto-launch behavior when Revit shows unsigned add-in security dialogs during startup.

## [0.0.2] - 2026-04-10

### Fixed

- Fixed pytest plugin auto-loading via the `pytest11` entry point.
- Fixed the `pytest_unconfigure` hook signature in `revitdevtool_pytest.plugin` so Pluggy accepts the plugin during pytest startup.
- Restored the expected standalone package behavior so `RevitDevTool.PyTest` can be discovered and activated by a normal Python/pytest environment before connecting to Revit.

### Changed

- Aligned the published package metadata name with the recommended normalized package name `revitdevtool_pytest`.

### Notes

- This release is intended as a bugfix release for the `0.0.1` package.
- No user-facing CLI options are changed in this release.
- This name alignment is intended to remain compatible with existing PyPI installs because Python package indexes and installers normalize `.`, `-`, and `_` in distribution names.

## [0.0.1] - 2026-04-09

### Added

- Initial standalone `pytest` plugin package for running Revit API tests through the RevitDevTool Named Pipe bridge.
- Automatic discovery of running Revit instances through named pipes.
- Optional Revit auto-launch flow via `--revit-launch` and `--revit-version`.
- Remote test execution by intercepting `pytest_pyfunc_call` and forwarding serialized test code to Revit.
- Support for explicit pipe targeting with `--revit-pipe`.
- Per-test timeout and launch timeout configuration.
- Revit startup dialog resolver integration.
- Example test suite and VS Code integration guidance in the project documentation.

### Notes

- `0.0.1` introduced the core test bridge workflow, but the published package contains a plugin loading issue fixed in `0.0.2`.

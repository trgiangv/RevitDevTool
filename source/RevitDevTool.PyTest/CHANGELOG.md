# Changelog

All notable changes to this project will be documented in this file.

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

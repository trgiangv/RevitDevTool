# Changelog

All notable changes to this project will be documented in this file.

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

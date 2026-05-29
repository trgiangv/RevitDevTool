# Known Test Gaps

These are known harness gaps. Do not paper over them by changing unrelated code.

## Coverage Depth

- Existing test projects are currently shallow in several areas and often function as smoke/contract tests rather than realistic end-to-end validation.
- Host integration, threading, package installation, MCP dispatch through a live pipe, startup behavior, and visualization are not deeply covered.
- Passing tests do not remove the need to reason through host boundaries, target frameworks, and manual verification constraints.
- For meaningful code changes, agents should fill the smallest useful test gap they can, or state why the correct verification needs a live host or prepared environment.
- Avoid overbuilding tests. The goal is risk-based coverage, not making the suite look more serious than it is.

## Stale Paths

- Some tests still search upward for `RevitDevTool.sln` even though the current solution is `RevitDevTool.slnx`.
- `ParserIntegrationTests` expects `Samples/McpToolsetDemo/bin/Debug/net8.0/McpToolsetDemo.dll`, but helper code still constructs a `source/Samples/...` path.
- Python parser tests can still look for `source/RevitDevTool/Resources/scripts/ToolParser.py`; current embedded scripts live under `source/DevTools.Execution/Resources/scripts/`.

## Environment Dependencies

- `PythonInProcessParserTests` and parser integration tests expect `%APPDATA%\RevitDevTool\pixi-env\.pixi\envs\default`.
- Server and bridge tests may require built sample assets, prepared Python packages, and a running or launchable host process.

## Reporting Rule

If a known gap blocks verification, state the exact test name and missing path/environment. Do not broaden the implementation just to make an unrelated harness gap disappear.

## GitNexus Indexing

- `npx gitnexus status` reported the repo index stale on 2026-05-29.
- Root `npx gitnexus analyze` failed in `scopeResolution` with `Cannot add property 1, object is not extensible`.
- Vendor code under `libs/` triggered parser warnings before `.gitnexusignore` was added; `libs/` is now ignored for future runs.
- A retry after `npx gitnexus clean --force` still failed with the same `scopeResolution` error, so docs were updated by direct source inspection instead of GitNexus graph queries.
- Treat GitNexus as unavailable until this analyzer issue is fixed upstream or the problematic source pattern is isolated.

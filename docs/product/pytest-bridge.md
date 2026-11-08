# PyTest Bridge

Local pytest collects. The host executes over `DevTools_{Host}_{Version}_{PID}` (`BridgeMessage`). Not the MCP pipe `DevToolsMcp_*`.

- Client: `source/RevitDevTool.PyTest` (`revitdevtool_pytest` on PyPI). In this repo, `pixi run live` from that folder; in a consumer project, `uv run pytest`.
- CPython `test_*.py` → `tests/run`. IronPython `test_*_ipy.py` → `ipytests/run` (unittest, no PEP 723).
- Options: `--host` / `--host-version` / `--host-pipe` / `--force-launch` / `--per-test-timeout` / `--launch-timeout`.
- Wire: Python `models.py` ↔ C# `PytestContracts.cs`.

Architecture: [Execution/pytest-bridge.md](../architecture/Execution/pytest-bridge.md). Write tests: `.agents/skills/revit-pytest/SKILL.md`.

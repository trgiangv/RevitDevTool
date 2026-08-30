# PyTest Bridge

Local pytest collects. The host executes over `DevTools_{Host}_{Version}_{PID}` (`BridgeMessage`). Not the MCP pipe `DevToolsMcp_*`.

- Client: sibling `RevitDevTool.PyTest`. Always `uv run pytest` from that repo.
- CPython `test_*.py` → `tests/run`. IronPython `test_*_ipy.py` → `ipytests/run` (unittest, no PEP 723).
- Options: `--host` / `--host-version` / `--host-pipe` / `--force-launch` / `--per-test-timeout` / `--launch-timeout`.
- Wire: Python `models.py` ↔ C# `PytestContracts.cs`.

Architecture: [Execution/pytest-bridge.md](../architecture/Execution/pytest-bridge.md). Write tests: `.agents/skills/revit-pytest/SKILL.md`.

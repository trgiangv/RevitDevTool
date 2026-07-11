---
name: pytest-bridge-change
description: Review checklist for pytest bridge changes (contracts, runner, pipe routes, dependency preparation, test discovery/run). Use when editing source/DevTools.Execution/External/Testing/ or PytestContracts/PytestRunner.
---

# PyTest Bridge Change

Use when editing pytest bridge contracts, runner, named pipe test routes, dependency preparation, or test discovery/run behavior.

## Checklist

- Read `docs/agents/mcp-pytest-bridge.md`, `docs/agents/known-test-gaps.md`, and `docs/PyTest/README.md`.
- Treat `tests/discover` and `tests/run` as separate flows.
- Preserve framed named-pipe request/response contracts.
- Keep dependency setup separate from test execution.
- Do not hide known stale path gaps by broad unrelated edits.
- Treat existing tests as shallow contract/smoke coverage unless they exercise the changed bridge path directly.
- Add a focused contract or runner test when changing request/response shape, discovery, run, or progress reporting logic.
- Update `docs/PyTest/README.md` or `docs/agents/mcp-pytest-bridge.md` when changing pytest bridge architecture, named-pipe protocol, runner lifecycle, or verification workflow.
- Run focused .NET tests or document host/Pixi/named-pipe blockers.

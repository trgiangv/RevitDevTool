# Agent Harness Index

Use this directory as deterministic agent memory. It does not replace the architecture docs under `docs/`; it routes agents to the right context, boundaries, and verification commands.

## Task Routing

| Task | Read | Projects | Skill |
|------|------|----------|-------|
| Build/compile host add-in | `build-matrix.md`, `verification.md` | `build/`, `scripts/agent/` | `.agents/skills/revit-build/SKILL.md` |
| Package/release/installer | `build-matrix.md`, `verification.md` | `build/`, `scripts/agent/` | `.agents/skills/packaging-release-review/SKILL.md` |
| Shared library change | `host-boundaries.md`, `build-matrix.md` | `source/DevTools.*/` | `.agents/skills/host-boundary-review/SKILL.md`, `.agents/skills/net48-compat-review/SKILL.md` |
| Execution provider/strategy/orchestrator | `execution-system.md`, `verification.md` | `source/DevTools.Execution/`, `source/DevTools.Execution.Abstractions/` | `.agents/skills/execution-system-change/SKILL.md` |
| MCP registry/server/dispatch | `mcp-pytest-bridge.md` | `source/DevTools.Mcp/`, `source/DevTools.Ipc/` | `.agents/skills/mcp-bridge-change/SKILL.md` |
| Daemon (auth, gateway, tray, control pipe) | `docs/MCP/README.md` | `source/DevTools.Daemon/` | `.agents/skills/mcp-bridge-change/SKILL.md` |
| pytest bridge/test runtime (server-side) | `mcp-pytest-bridge.md`, `known-test-gaps.md` | `source/DevTools.Execution/External/Testing/` | `.agents/skills/pytest-bridge-change/SKILL.md` |
| Write pytest tests (client-side) | `RevitDevTool.PyTest/AGENTS.md` | `RevitDevTool.PyTest/` | `.agents/skills/revit-pytest/SKILL.md` |
| Revit API documentation lookup | — | `.agents/mcp/revit-api-docs/` | `.agents/skills/revit-api-docs/SKILL.md` |
| pyRevit IronPython tools | — | External pyRevit extensions | `.agents/skills/pyrevit-ironpython/SKILL.md` |
| Logging or geometry visualization | `host-boundaries.md` | `source/DevTools.Logging/`, host `Visualization/` | `.agents/skills/logging-visualization-review/SKILL.md` |
| Startup, lazy loading, host boot | `startup-performance.md`, `host-boundaries.md` | Host projects (`RevitDevTool/`, `AcadDevTool/`) | No separate skill |

## Operating Loop

1. Classify the task and read the files above.
2. Identify host-specific vs shared code before editing.
3. Choose the smallest verification command from `verification.md`.
4. Decide whether existing tests are meaningful for this change; add a focused test when the current suite only gives smoke coverage.
5. Run the command or explain the environmental blocker.
6. Update architecture docs when the change affects an important feature, boundary, flow, or long-term decision.
7. Summarize changed files, verification, known risks, and follow-up work.

## Documentation Rule

Architecture docs are part of the engineering source of truth, not optional prose. If a change teaches future agents how the system should evolve, record it in the right layer:

- `docs/*/README.md` for durable module architecture.
- `docs/agents/*.md` for agent workflow, traps, verification, and decision context.
- `.agents/skills/*/SKILL.md` for short task checklists.

Do not update every layer by default. Update the layer that future work will actually consult.

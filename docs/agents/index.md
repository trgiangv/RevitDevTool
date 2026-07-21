# Agent Harness Index

Use this directory as deterministic agent memory. It routes agents to context and verification. Routine compile feedback comes from `.cursor/hooks/` — do not open a build skill for that.

## Task Routing

| Task | Read | Projects |
|------|------|----------|
| MCP integration testing | `mcp-integration-test.md`, `verification.md` | Host + daemon |
| Compile / deploy host | `build-matrix.md`, `verification.md` | `build/`, `scripts/` |
| Package / release | `build-matrix.md`, `verification.md` | `build/`, `scripts/` |
| Shared / host-boundary / execution / MCP / pytest-bridge / logging | Matching `docs/agents/*.md` + module README | `source/DevTools.*`, hosts |
| Write pytest tests (client) | `RevitDevTool.PyTest/AGENTS.md` | Sibling `RevitDevTool.PyTest/` |
| Revit API explore + live execute | `docs/MCP/workflows.md` | MCP + `rvtdocs-mcp` |
| Startup / lazy load | `startup-performance.md`, `host-boundaries.md` | Host projects |

When a task matches a packaged workflow, read the relevant `.agents/skills/*/SKILL.md`. The skill set may grow over time — do not hardcode or invent skill names in docs.

## Operating Loop

1. Classify the task; read the digest above (not a build skill).
2. Edit; let the stop-hook compile (Revit API → 2022/2025/2027; shared → Debug/Release).
3. Fix hook-reported errors; use `scripts/` for deploy, tests, pack.
4. Client pytest: `uv run pytest` in `RevitDevTool.PyTest` only.
5. Update architecture docs when boundaries/flows change; summarize risks.

## Documentation Rule

- `docs/*/README.md` — durable module architecture.
- `docs/agents/*.md` — agent workflow, traps, verification.
- `.agents/skills/*/SKILL.md` — domain workflows; read the matching skill when the task fits.
- `.cursor/hooks/` — automatic compile verify (replaces build-skill roundtrips).

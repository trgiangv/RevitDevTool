# Agent Harness Index

Use this directory as deterministic agent memory. It routes agents to context
and verification. Process shape comes from `docs/WORKFLOW.md`. Routine compile
feedback comes from `.cursor/hooks/` — do not open a build skill for that.

## Task Routing

| Task | Read | Projects |
|------|------|----------|
| MCP integration testing | `mcp-integration-test.md`, `verification.md` | Host + daemon |
| Compile / deploy host | `build-matrix.md`, `verification.md` | `build/`, `scripts/` |
| Package / release | `build-matrix.md`, `verification.md` | `build/`, `scripts/` |
| Shared / host-boundary / execution / MCP / pytest-bridge / logging | Matching `docs/agents/*.md` + `docs/architecture/<Module>/README.md` + `docs/product/<domain>.md` | `source/DevTools.*`, hosts |
| Write pytest tests (client) | `RevitDevTool.PyTest/AGENTS.md` | Sibling `RevitDevTool.PyTest/` |
| Revit API explore + live execute | `docs/architecture/MCP/workflows.md` | MCP + `rvtdocs-mcp` |
| Startup / lazy load | `startup-performance.md`, `host-boundaries.md` | Host projects |
| Multi-session / coordination | `docs/plans/active/`, `docs/WORKFLOW.md` | — |
| Lasting policy lookup | `docs/decisions/` | — |

When a task matches a packaged workflow, read the relevant `.agents/skills/*/SKILL.md`.
The skill set may grow over time — do not hardcode or invent skill names in docs.

## Operating Loop

1. Classify work shape via `docs/WORKFLOW.md` (read-only / bounded / durable / pause).
2. Read product + architecture + this digest as needed (not a build skill).
3. Edit; let the stop-hook compile (Revit API → 2022/2025/2027; shared → Debug/Release).
4. Fix hook-reported errors; use `scripts/` for deploy, tests, pack.
5. Client pytest: `uv run pytest` in `RevitDevTool.PyTest` only.
6. Update the matching doc layer when behavior/boundaries/workflow change.
7. Summarize risks and proof.

## Documentation Rule

- `docs/product/` — current behavior contracts.
- `docs/architecture/*/README.md` — durable module architecture.
- `docs/agents/*.md` — agent workflow, traps, verification.
- `docs/decisions/` — lasting choices (not `decision-log.md`).
- `docs/plans/` — multi-session working memory.
- `.agents/skills/*/SKILL.md` — domain workflows; read the matching skill when the task fits.
- `.cursor/hooks/` — automatic compile verify (replaces build-skill roundtrips).

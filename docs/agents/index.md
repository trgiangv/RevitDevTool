# Agent Harness Index

Task router and verification pointers. Work shape: `docs/WORKFLOW.md`.
Build/deploy traps: `.agents/skills/build/SKILL.md`.

## Task routing

| Task | Read | Projects |
|------|------|----------|
| Compile / deploy / verify | `build` skill, `verification.md`, `build-matrix.md`, **`test-matrix.md` (Current gaps first)** | `source/`, `scripts/`, `tests/` |
| MCP integration testing | `mcp-integration-test.md` | Host + daemon |
| MTP host testing | `revit-nunit` skill, `host-testing.md`, `docs/product/host-testing.md`, `docs/architecture/Testing/` | `DevTools.Testing.*`, `DevTools.NUnit.*`, `DevTools.TUnit.*`, `DevTools.TestAdapter`, `DevTools.TestRunner*` |
| TUnit provider | `docs/product/tunit-host-testing.md`, `host-testing.md` | `DevTools.TUnit.*` |
| MCP agent efficiency | `docs/plans/completed/2026-07-26-mcp-agent-efficiency.md` | `DevTools.Mcp.*`, daemon |
| Execution / MCP / host pipe / logging | Matching `docs/agents/*.md` + `docs/architecture/<Module>/` + `docs/product/` | `DevTools.*`, hosts |
| Revit API + live execute | `revit-developer` skill, `architecture/MCP/workflows.md` | MCP + rvtdocs-mcp |
| Multi-session work | `docs/plans/active/` | — |
| Policy | `docs/decisions/` | — |

Domain skills: `.agents/skills/*/SKILL.md` — read the one that matches.

## Operating loop

1. Classify work (`WORKFLOW.md`): read-only / bounded / plan / pause.
2. Read product + architecture + one agent digest as needed. **Before new tests, Coverlet, or Execution/MCP/Ipc/Daemon refactor:** `test-matrix.md` Current gaps + untestable limits.
3. Edit; **compile** per build skill; run **in-repo** tests when contracts change.
4. Live MCP checklist when wire/surface changed and host available.
5. Update one doc layer if behavior changed.
6. Report proof and blockers (`test-matrix.md` Current gaps / Skip / live). Do not spawn a second Coverlet on a live testhost.

## Documentation layers

| Layer | Holds |
|-------|--------|
| `docs/product/` | Observable behavior |
| `docs/architecture/` | Module structure |
| `docs/agents/` | Traps, verify, integration runbooks |
| `docs/decisions/` | Lasting choices |
| `docs/plans/` | Multi-session execution |
| `.agents/skills/` | Repeatable workflows (build, platform, Revit) |

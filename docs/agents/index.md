# Agent Task Router

Pick the row that matches the task and read only what it lists. The entry point
is [`AGENTS.md`](../../AGENTS.md); work shape is [`WORKFLOW.md`](../WORKFLOW.md).

| Task | Read | Projects |
|------|------|----------|
| Compile, deploy, verify | `build` skill, [verification.md](verification.md), [build-matrix.md](build-matrix.md), [test-matrix.md](test-matrix.md) | `source/`, `scripts/`, `tests/` |
| Add tests or measure coverage | [test-matrix.md](test-matrix.md) | `tests/` |
| MCP integration testing | [mcp-integration-test.md](mcp-integration-test.md) | Host and Daemon |
| MTP host testing | `revit-test` skill, [host-testing.md](host-testing.md), `docs/product/host-testing.md`, `docs/architecture/Testing/` | `DevTools.Testing.*`, `DevTools.NUnit.*`, `DevTools.TUnit.*`, `DevTools.MSTest.*`, `DevTools.TestAdapter`, `DevTools.TestRunner*` |
| TUnit provider | `docs/product/tunit-host-testing.md`, [host-testing.md](host-testing.md) | `DevTools.TUnit.*` |
| MSTest provider | `docs/decisions/0038-mstest-host-provider.md`, [host-testing.md](host-testing.md), `source/DevTools.TestAdapter/README.md` | `DevTools.MSTest.*` |
| MCP agent efficiency | `docs/plans/completed/2026-07-26-mcp-agent-efficiency.md` | `DevTools.Mcp.*`, Daemon |
| Execution | [execution-system.md](execution-system.md), `docs/architecture/Execution/` | `DevTools.Execution*` |
| MCP or host pipe | [mcp-pytest-bridge.md](mcp-pytest-bridge.md), `docs/architecture/MCP/` | `DevTools.Ipc`, `DevTools.Mcp.*`, `DevTools.Daemon` |
| Shared vs host code | [host-boundaries.md](host-boundaries.md) | `DevTools.*`, hosts |
| Revit API and live execute | `revit-developer` skill, `docs/architecture/MCP/workflows.md` | MCP, rvtdocs-mcp |
| Platform, IPC, packaging | `platform-change` skill | `source/DevTools.*`, `build/` |
| Formatting and lint | [formatting-linting.md](formatting-linting.md) | `.editorconfig`, `scripts/format-lint.ps1` |
| Change CI or debug a CI run | `ci-change` skill, [ci.md](ci.md) | `.github/`, `build/Products/`, `build/Cli/` |
| Release a product | [release.md](release.md) | `build/`, `.github/workflows/release.yml` |
| Multi-session work | `docs/plans/active/` | |
| Policy | `docs/decisions/` | |

Skills live in `.agents/skills/<name>/SKILL.md`.

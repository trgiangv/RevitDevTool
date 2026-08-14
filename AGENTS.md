# Agent Instructions

Entry point for agents working in this repo. Behavior truth lives in code and
`docs/product/`; structure in `docs/architecture/`; how to prove work in
`.agents/skills/build/SKILL.md` and `docs/agents/`.

## Start here

| Intent | Go to |
|--------|--------|
| What should the product do? | `docs/product/<domain>.md` |
| How is it wired? | `docs/ARCHITECTURE.md` → `docs/architecture/<Module>/` |
| Which digest for this task? | `docs/agents/index.md` |
| Multi-session / risky work | `docs/WORKFLOW.md` → `docs/plans/active/` |
| Lasting policy | `docs/decisions/` |

Read the **minimum** layer for the task. Do not duplicate docs into chat.

## Problems you will hit

| Problem | Fast fix |
|---------|----------|
| Don't know where code belongs | `docs/agents/host-boundaries.md` — shared `DevTools.*` vs `RevitDevTool` / `AcadDevTool` |
| Build fails / unsure what to run | `.agents/skills/build/SKILL.md` |
| Test path looks wrong | `docs/agents/known-test-gaps.md` |
| MCP live behavior | `docs/agents/mcp-integration-test.md` |
| Need logs to diagnose host/Daemon | `docs/agents/verification.md` → Diagnostic logs |
| Host pytest/control pipe (in-repo) | `docs/agents/mcp-pytest-bridge.md` |
| Revit API + execute in host | `.agents/skills/revit-developer/SKILL.md` |
| NUnit host tests | `.agents/skills/revit-nunit/SKILL.md` |
| pytest host tests | `.agents/skills/revit-pytest/SKILL.md` |
| Platform / IPC / packaging edit | `.agents/skills/platform-change/SKILL.md` |

## Verify before done

1. After `.cs` / `.csproj` / `.xaml` edits → run compile from **build skill** (touched csproj minimum).
2. Contract or dispatch change → add/run focused test (`scripts/test-dotnet.ps1 -Project …`).
3. Daemon or host MCP surface → compile + `mcp-integration-test.md` checklist when host available.
4. Report **evidence** (command + pass/fail). If blocked, state exact missing env (host PID, pixi, file lock).

Do not claim completion from diff alone.

## Repo anchors

- Solution: `RevitDevTool.slnx` (no root `.sln`).
- Shared platform: `source/DevTools.*` · Revit host: `source/RevitDevTool/` · AutoCAD: `source/AcadDevTool/`.
- Scripts: `scripts/` · Samples: `samples/` (not `source/samples/`).

## Doc update rule

When observable behavior or boundaries change, update **one** layer only:
`docs/product/`, `docs/architecture/<Module>/`, `docs/agents/`, or `docs/decisions/`.
Link across layers; do not copy the same truth twice.

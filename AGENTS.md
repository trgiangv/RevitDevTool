# Agent Instructions

Entry point for agents working in this repo. Behavior lives in code and
`docs/product/`, structure in `docs/architecture/`, and proof in the build skill
and `docs/agents/`. Read only the layer the task needs.

## Start here

| Intent | Go to |
|--------|-------|
| What should the product do? | `docs/product/<domain>.md` |
| How is it wired? | `docs/ARCHITECTURE.md`, then `docs/architecture/<Module>/` |
| Which digest or skill fits this task? | `docs/agents/index.md` |
| How to prove work | `.agents/skills/build/SKILL.md` |
| Known test gaps and expected Skips | `docs/agents/test-matrix.md` (Current gaps) |
| Multi-session or risky work | `docs/WORKFLOW.md`, then `docs/plans/active/` |
| Lasting policy | `docs/decisions/` |

## Common problems

| Problem | Go to |
|---------|-------|
| Unsure where code belongs (shared `DevTools.*` or a host project) | `docs/agents/host-boundaries.md` |
| Build fails, or unsure what to run | `.agents/skills/build/SKILL.md` |
| A test Skips, or coverage looks low | `docs/agents/test-matrix.md`, before adding tests |
| Live MCP behavior | `docs/agents/mcp-integration-test.md` |
| Host or Daemon logs (`crash_*` when the add-in failed to load) | `docs/agents/verification.md#diagnostic-logs` |
| Host pytest and control pipe | `docs/agents/mcp-pytest-bridge.md` |
| Revit API and execute in a live host | `.agents/skills/revit-developer/SKILL.md` |
| MTP host tests | `.agents/skills/revit-test/SKILL.md`, `docs/agents/host-testing.md` |
| pytest host tests | `.agents/skills/revit-pytest/SKILL.md` |
| Platform, IPC, or packaging edit | `.agents/skills/platform-change/SKILL.md` |
| Formatting and lint | `docs/agents/formatting-linting.md` |
| Release a product | `docs/agents/release.md` |

## Verify before done

1. After `.cs`, `.csproj`, or `.xaml` edits, compile the touched projects with the build skill.
2. After a contract or dispatch change, add or run a focused test
   (`dotnet run --project tests/<proj>/<proj>.csproj`).
3. After a Daemon or host MCP change, also run the `mcp-integration-test.md` checklist when a host is available.
4. Once the task's edits are finished, run `pwsh -File scripts/format-lint.ps1` one time. It formats the changed `.cs` and `.xaml` files and lists what it cannot fix.
5. Report the command and its pass or fail. If blocked, name what is missing (host PID, pixi, file lock).

A diff alone is not proof.

## Repo anchors

- Solution: `RevitDevTool.slnx` (no root `.sln`).
- Shared platform: `source/DevTools.*`. Revit host: `source/RevitDevTool/`. AutoCAD host: `source/AcadDevTool/`.
- Scripts: `scripts/`. Samples: `samples/`.
- pytest client: `source/RevitDevTool.PyTest/`, its own pixi workspace (`pixi run lint`, `test`, `build`, `live`).

## Doc update rule

When observable behavior or boundaries change, update the one matching layer
(`docs/product/`, `docs/architecture/<Module>/`, `docs/agents/`, or
`docs/decisions/`) and link from the others.

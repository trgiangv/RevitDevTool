# Execution Plans

Execution plans are Git-native working memory for complex tasks. They preserve
enough context for another agent or human to resume work without reconstructing
intent from chat history or a partial diff.

## When To Create A Plan

Use an ephemeral plan for bounded, single-session work.

Create one durable plan when work spans sessions, coordinates contributors, has
meaningful dependencies or ordering, requires recovery steps, or would be unsafe
to resume from the diff alone.

Use `docs/templates/exec-plan.md` and place the file under `active/`.

## Lifecycle

```text
docs/plans/active/<slug>.md
  -> update progress and decisions during implementation
  -> record final validation and result
  -> move to docs/plans/completed/<slug>.md
```

The plan is the primary task artifact. Promote a lasting product or architecture
decision into `docs/decisions/`; keep task-local choices in the plan.

## Active Plans

| Plan | Status |
|------|--------|
| [2026-08-17-p0-xunit4-repository-mtp-baseline.md](active/2026-08-17-p0-xunit4-repository-mtp-baseline.md) | Active — CLI complete; IDE smoke open |
| [2026-08-17-p1-framework-neutral-testing-core.md](active/2026-08-17-p1-framework-neutral-testing-core.md) | Active — Tasks 1-3 landed |
| [2026-08-17-p2-xunit4-host-provider.md](active/2026-08-17-p2-xunit4-host-provider.md) | Blocked by P1 |
| [2026-08-02-mrtr-implementation.md](active/2026-08-02-mrtr-implementation.md) | Active — G1 closed; G3/G4 open |

## Recently Completed

| Plan | Completed |
|------|-----------|
| [2026-08-15-host-identity-ui-free-infrastructure.md](completed/2026-08-15-host-identity-ui-free-infrastructure.md) | 2026-08-17 |
| [2026-08-06-pixi-skip-if-listed.md](completed/2026-08-06-pixi-skip-if-listed.md) | 2026-08-06 |
| [2026-07-25-mcp-call-observability.md](completed/2026-07-25-mcp-call-observability.md) | 2026-07-25 |

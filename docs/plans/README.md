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
| [2026-08-22-testing-core-open-closed.md](active/2026-08-22-testing-core-open-closed.md) | Active — Tasks 0–4 landed; Opus 5 gate Accept |
| [2026-08-21-tunit-revit-testhost.md](active/2026-08-21-tunit-revit-testhost.md) | Spike — not production-ready |
| [2026-08-17-p1-framework-neutral-testing-core.md](active/2026-08-17-p1-framework-neutral-testing-core.md) | Active — Tasks 1-4 landed |
| [2026-08-17-p2-testing-kernel-extraction.md](active/2026-08-17-p2-testing-kernel-extraction.md) | Active |

## Recently Completed

| Plan | Completed |
|------|-----------|
| [2026-08-31-mcp-sdk-2-2-host-wire.md](completed/2026-08-31-mcp-sdk-2-2-host-wire.md) | 2026-08-31 |
| [2026-08-02-mrtr-implementation.md](completed/2026-08-02-mrtr-implementation.md) | 2026-08-31 (G1 done; G3/G4 not product — [0029](../decisions/0029-mcp-use-case-limits-not-full-protocol.md)) |
| [2026-08-15-nunit-visual-studio-debug.md](completed/2026-08-15-nunit-visual-studio-debug.md) | 2026-08-27 |
| [2026-08-12-nunit-native-runtime-mtp.md](completed/2026-08-12-nunit-native-runtime-mtp.md) | 2026-08-27 (superseded leftover gates withdrawn) |
| [2026-08-10-nunit-host-testing.md](completed/2026-08-10-nunit-host-testing.md) | 2026-08-12 (superseded; archived 2026-08-27) |
| [2026-08-18-nunit-boundary-cleanup.md](completed/2026-08-18-nunit-boundary-cleanup.md) | 2026-08-18 |
| [2026-08-18-assembly-isolation-kernel.md](completed/2026-08-18-assembly-isolation-kernel.md) | 2026-08-18 |
| [2026-08-18-nunit-mtp-only.md](completed/2026-08-18-nunit-mtp-only.md) | 2026-08-18 |
| [2026-08-15-host-identity-ui-free-infrastructure.md](completed/2026-08-15-host-identity-ui-free-infrastructure.md) | 2026-08-17 |
| [2026-08-06-pixi-skip-if-listed.md](completed/2026-08-06-pixi-skip-if-listed.md) | 2026-08-06 |
| [2026-07-25-mcp-call-observability.md](completed/2026-07-25-mcp-call-observability.md) | 2026-07-25 |

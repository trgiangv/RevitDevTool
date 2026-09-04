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

Do not merge completed plans that record different migrations. Cross-link them.
Rename an active plan when its ADR number or workstream id changed.

## Active Plans

None.

## Recently Completed

| Plan | Completed |
|------|-----------|
| [2026-08-21-tunit-revit-testhost.md](completed/2026-08-21-tunit-revit-testhost.md) | 2026-09-04 (spike closed; not a production track) |
| [2026-09-03-stj-facade-0028.md](completed/2026-09-03-stj-facade-0028.md) | 2026-09-04 (P0 landed; `object?` is 0031 AOT follow-up) |
| [2026-09-03-mcp-layer-identity-s5.md](completed/2026-09-03-mcp-layer-identity-s5.md) | 2026-09-03 (S5 landed; S1/S2 + SDK-free contracts are follow-on) |
| [2026-09-03-daemon-aot-spike.md](completed/2026-09-03-daemon-aot-spike.md) | 2026-09-03 (rolled back — [0032](../decisions/0032-daemon-mewui-and-aot.md)) |
| [2026-08-31-mcp-sdk-2-2-host-wire.md](completed/2026-08-31-mcp-sdk-2-2-host-wire.md) | 2026-08-31 |
| [2026-08-22-testing-core-open-closed.md](completed/2026-08-22-testing-core-open-closed.md) | 2026-08-22 ([0024](../decisions/0024-testing-core-open-closed-providers.md)) |
| [2026-08-17-p2-testing-kernel-extraction.md](completed/2026-08-17-p2-testing-kernel-extraction.md) | 2026-08-17 ([0021](../decisions/0021-testing-kernel-and-provider-owned-framework-runtime.md)) |
| [2026-08-17-p1-framework-neutral-testing-core.md](completed/2026-08-17-p1-framework-neutral-testing-core.md) | 2026-08-17 |
| [2026-08-02-mrtr-implementation.md](completed/2026-08-02-mrtr-implementation.md) | 2026-08-31 (G1 done; elicitation/progress not product — [0027](../decisions/0027-mcp-product-surface.md)) |
| [2026-08-02-host-mcp-spec-engine.md](completed/2026-08-02-host-mcp-spec-engine.md) | 2026-08-02 (0012; 0027 later withdrew SDK-strip) |
| [2026-08-15-nunit-visual-studio-debug.md](completed/2026-08-15-nunit-visual-studio-debug.md) | 2026-08-27 |
| [2026-08-12-nunit-native-runtime-mtp.md](completed/2026-08-12-nunit-native-runtime-mtp.md) | 2026-08-27 (superseded leftover gates withdrawn) |
| [2026-08-10-nunit-host-testing.md](completed/2026-08-10-nunit-host-testing.md) | 2026-08-12 (superseded; archived 2026-08-27) |
| [2026-08-18-nunit-boundary-cleanup.md](completed/2026-08-18-nunit-boundary-cleanup.md) | 2026-08-18 |
| [2026-08-18-assembly-isolation-kernel.md](completed/2026-08-18-assembly-isolation-kernel.md) | 2026-08-18 |
| [2026-08-18-nunit-mtp-only.md](completed/2026-08-18-nunit-mtp-only.md) | 2026-08-18 |
| [2026-08-15-host-identity-ui-free-infrastructure.md](completed/2026-08-15-host-identity-ui-free-infrastructure.md) | 2026-08-17 |
| [2026-08-06-pixi-skip-if-listed.md](completed/2026-08-06-pixi-skip-if-listed.md) | 2026-08-06 |
| [2026-07-25-mcp-call-observability.md](completed/2026-07-25-mcp-call-observability.md) | 2026-07-25 |

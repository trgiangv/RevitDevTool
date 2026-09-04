# Active Execution Plans

Use one evolving plan per coherent workstream when work needs durable memory.
Use `docs/templates/exec-plan.md`, keep progress and validation current, avoid
parallel duplicate plans for the same workstream, and move a plan to
`../completed/` only after the result is verified.

## Daemon

Production publish is framework-dependent single-file. AOT spike:
[completed](../completed/2026-09-03-daemon-aot-spike.md) (rolled back). UI/AOT:
[0032](../../decisions/0032-daemon-mewui-and-aot.md). JSON facades:
[0031](../../decisions/0031-daemon-json-source-gen.md) —
[plan completed](../completed/2026-09-03-stj-facade-0028.md). Remaining
`object?` on invoke/batch DTOs is 0031 follow-up, not an active plan.

## Test gaps (closed)

Headless-automatable gaps closed:
[2026-09-04-known-test-gaps](../completed/2026-09-04-known-test-gaps.md).
Remaining live-host / year-matrix / pixi-opt-in stay in `test-matrix.md`.

## MCP tests (active)

Split god project `DevTools.Mcp.Tests` into module-scoped projects:
[2026-09-04-mcp-test-project-split](2026-09-04-mcp-test-project-split.md).

## MCP (closed)

- Product: [0027](../../decisions/0027-mcp-product-surface.md). Host pipe:
  [0012](../../decisions/0012-host-mcp-spec-engine.md).
- Host-wire 2.2: [2026-08-31](../completed/2026-08-31-mcp-sdk-2-2-host-wire.md).
- Spec engine: [2026-08-02](../completed/2026-08-02-host-mcp-spec-engine.md).
- Layer identity S5: [2026-09-03](../completed/2026-09-03-mcp-layer-identity-s5.md)
  — landed. **Open follow-on (no plan yet):** S1 vs S2 packaging gate; SDK-free
  host contracts.
- MRTR: [2026-08-02](../completed/2026-08-02-mrtr-implementation.md) — G1
  landed; elicitation/progress not product (0027).

## MTP Testing

| Track | Decision | Plan | Status |
|-------|----------|------|--------|
| P1 testing core | [0020](../../decisions/0020-framework-neutral-mtp-host-testing.md) | [completed](../completed/2026-08-17-p1-framework-neutral-testing-core.md) | Landed |
| Kernel extract | [0021](../../decisions/0021-testing-kernel-and-provider-owned-framework-runtime.md) | [completed](../completed/2026-08-17-p2-testing-kernel-extraction.md) | Landed — PolySharp not in plan |
| Open-closed providers | [0024](../../decisions/0024-testing-core-open-closed-providers.md) | [completed](../completed/2026-08-22-testing-core-open-closed.md) | Landed |
| TUnit spike | — | [completed](../completed/2026-08-21-tunit-revit-testhost.md) | Closed 2026-09-04 — not a production track |

NUnit MTP-only ([0022](../../decisions/0022-nunit-mtp-only-testing-stack.md)) is
[completed](../completed/2026-08-18-nunit-mtp-only.md).

No active execution plans.

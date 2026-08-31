# Active Execution Plans

Use one evolving plan per coherent workstream when work needs durable memory.
Use `docs/templates/exec-plan.md`, keep progress and validation current, avoid
parallel duplicate plans for the same workstream, and move a plan to
`../completed/` only after the result is verified.

## MCP (closed)

Host-wire adoption: [2026-08-31 MCP SDK 2.2 host wire](../completed/2026-08-31-mcp-sdk-2-2-host-wire.md)
— Phases 0–4 + live Revit 2024/2025. Policy:
[0027](../../decisions/0027-mcp-sdk-host-wire-adoption.md) /
[0028](../../decisions/0028-host-alc-progress-notifications.md) /
[0029](../../decisions/0029-mcp-use-case-limits-not-full-protocol.md).

MRTR session: [2026-08-02-mrtr-implementation.md](../completed/2026-08-02-mrtr-implementation.md)
— G1 landed; G2=B; G3/G4 not product.

## MTP Testing Program

| Track | Decision | Plan | Status |
|-------|----------|------|--------|
| P1 testing core | [0020](../../decisions/0020-framework-neutral-mtp-host-testing.md) | [Framework-neutral testing core](2026-08-17-p1-framework-neutral-testing-core.md) | Active — Tasks 1-4 landed; remaining cutover open |
| Kernel extract | [0021](../../decisions/0021-testing-kernel-and-provider-owned-framework-runtime.md) | [Testing kernel extraction](2026-08-17-p2-testing-kernel-extraction.md) | Active |
| Open-closed providers | [0024](../../decisions/0024-testing-core-open-closed-providers.md) | [Testing core open-closed](2026-08-22-testing-core-open-closed.md) | Active — Tasks 0–4 landed; Opus 5 gate Accept |
| TUnit spike | — | [TUnit Revit testhost](2026-08-21-tunit-revit-testhost.md) | Spike — not production-ready |

NUnit MTP-only ([0022](../../decisions/0022-nunit-mtp-only-testing-stack.md)) is already
[completed](../completed/2026-08-18-nunit-mtp-only.md). There is no separate
`p0-xunit4-repository-mtp-baseline` or `p2-xunit4-host-provider` plan file.

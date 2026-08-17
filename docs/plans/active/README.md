# Active Execution Plans

Use one evolving plan per coherent workstream when work needs durable memory.
Use `docs/templates/exec-plan.md`, keep progress and validation current, avoid
parallel duplicate plans for the same workstream, and move a plan to
`../completed/` only after the result is verified.

## MTP Testing Program Order

| Priority | Decision | Plan | Gate |
|---|---|---|---|
| P0 | [0022](../../decisions/0022-repository-tests-use-xunit4-native-mtp.md) | [xUnit 4 repository baseline](2026-08-17-p0-xunit4-repository-mtp-baseline.md) | CLI complete; IDE provider smoke still open |
| P1 | [0020](../../decisions/0020-framework-neutral-mtp-host-testing.md) | [Framework-neutral testing core](2026-08-17-p1-framework-neutral-testing-core.md) | Tasks 1-8 landed; stop for review before P2 |
| P2 | [0021](../../decisions/0021-xunit4-host-testing-provider.md) | [xUnit 4 host provider](2026-08-17-p2-xunit4-host-provider.md) | Blocked by P1 NUnit parity |

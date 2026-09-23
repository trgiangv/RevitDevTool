# Active Execution Plans

Use one evolving plan per coherent workstream when work needs durable memory.
Use `docs/templates/exec-plan.md`, keep progress and validation current, avoid
parallel duplicate plans for the same workstream, and move a plan to
`../completed/` only after the result is verified.

## Daemon

Production publish is framework-dependent single-file. AOT spike:
[completed](../completed/2026-09-03-daemon-aot-spike.md) (rolled back).
Shipped UI is still MewUI
([0032](../../decisions/0032-daemon-mewui-and-aot.md)).
[0043](../../decisions/0043-daemon-wpf-fluent.md) proposes plain WPF on the
.NET 10 Fluent theme and drops Native AOT. JSON facades:
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

## RevitDevTool.Tools (closed under 0036)

Select/search + Element Finder + Command Browser extract landed under
[0036](../../decisions/0036-revit-monitor-link-element-tokens.md). GeoViz /
Inspect pick-to-overlay abandoned (no plan).

## Revit monitor link-element tokens (closed)

Click-to-select for elements in RVT links (pair token, `SetReferences`,
host-space zoom) + later Tools / Element Finder ownership:
[completed](../completed/2026-09-17-revit-link-element-monitor-tokens.md)
([0036](../../decisions/0036-revit-monitor-link-element-tokens.md)).

## Execution / MSTest.Sdk tests (closed)

- Execution god project split: [completed](../completed/2026-09-13-execution-test-project-split.md)
  ([0034](../../decisions/0034-execution-mstest-sdk-scoped-tests.md)).
- Remaining testhosts: [completed](../completed/2026-09-13-mstest-sdk-repo-migration.md)
  ([0035](../../decisions/0035-mstest-sdk-repo-tests.md)).

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
| Contract narrow | — | [completed](../completed/2026-09-10-testing-contracts-narrow.md) | Landed 2026-09-10 — `machine-run` rename deferred |
| TUnit spike | — | [completed](../completed/2026-08-21-tunit-revit-testhost.md) | Closed 2026-09-04 — not a production track |

NUnit MTP-only ([0022](../../decisions/0022-nunit-mtp-only-testing-stack.md)) is
[completed](../completed/2026-08-18-nunit-mtp-only.md).

## WebView2 product shell (active)

Replace host add-in chrome with one bare window plus a browser. Daemon is
not this shell. Decisions
[0037](../../decisions/0037-webview2-bare-window-shell.md)–[0042](../../decisions/0042-wpf-ui-migration-slices.md)
(Proposed). Plan: [2026-09-23-webview2-shell](2026-09-23-webview2-shell.md).
Daemon: [0043](../../decisions/0043-daemon-wpf-fluent.md).

Active plans: MCP test split (landed; file still under `active/`); WebView2
shell.

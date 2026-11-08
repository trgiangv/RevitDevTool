# Execution Plan: Test flow cleanup and agent doc trim

Date: 2026-11-08

## Status

Completed

## Outcome

- Every headless test project that CI runs (`dotnet run --project build -- ci <product>`
  for `daemon`, `addin`, `nettest`, `pytest`) passes, with no `[Ignore]` left for
  flakiness.
- The two flaky `HostConnectionsIntegrationTests` pass 10 runs out of 10 because the
  cause is fixed, not because a wait got longer.
- `tests/` holds only test projects and fixtures the repo still uses.
- Agent docs and skills state each rule once, in plain sentences, with the reason,
  and drop stale snapshots.

## Context

- Test truth: [`test-matrix.md`](../../agents/test-matrix.md) (Current gaps rows for
  `HostBrokerIntegrationTests` and the HandyControl desktop tests).
- CI test selection: `build/Products/ProjectGraph.cs` (`tests/X/X.csproj` ending in
  `.Tests.csproj` that reference the product closure) and
  `build/Modules/Test/TestProductModule.cs`.
- Flaky code: `source/DevTools.Daemon/Mcp/Processes/HostConnections.cs`,
  `tests/DevTools.Mcp.Client.Tests/Harness/FakeMcpHostPipe.cs`,
  `tests/DevTools.Mcp.Client.Tests/HostBrokerIntegrationTests.cs`.

## Scope

In scope:

- A. Root cause and fix for the first-connect miss in `HostConnections` or the fake pipe;
  remove both `[Ignore]`.
- B. Test inventory cleanup from the baseline run: failing tests, dead folders,
  solution entries.
- C. Rewrite of `AGENTS.md`, `docs/agents/*.md`, `.agents/skills/**` in the current
  prompting style.

Out of scope:

- Renaming the legacy `tests/DevTools.Mcp.*.Tests` projects.
- New coverage work.
- `docs/product/`, `docs/architecture/`, `docs/decisions/` content (links only).

## Approach

1. Baseline: run `ci` for all four products; record pass/fail per project.
2. A (Opus, own worktree): add a logger to the two tests, reproduce in a loop, fix the
   cause, remove `[Ignore]`, prove 10/10.
3. C (Opus, docs only): rewrite agent docs and skills by the style rules below.
4. B (main session): act on the baseline; delete or fix what is dead or failing.
5. Re-run `ci` for all four products; update the Current gaps rows in `test-matrix.md`.

Style rules for C:

- Say a rule once, where it applies, with one clause of why. No bold for emphasis,
  no repeated "do not" lists; keep the few real prohibitions (stopping every Revit
  process, second testhost on a live `bin/`).
- Prefer the positive instruction ("run X") over a list of things to avoid.
- Remove dated snapshots and numbers a run can reproduce; keep how to measure.
- One home per fact; other files link to it.
- Skill `description`: what the skill does and when to use it, short. Body: the
  steps; detail goes to `references/`.

## Risks And Recovery

- A changes daemon connect timing that the live host relies on: run the
  `mcp-integration-test.md` connect scenario when a host is available, else say so.
- C drops a rule that mattered: every cut is visible in `git diff`; restore from it.

## Progress

- [x] Baseline run of `ci` for all four products (daemon failed on a live test that found a
  test's fake pipe; pytest failed because nested worktrees broke Sourcy).
- [x] Every `tests/*.Tests` project run once (2026-11-09): all pass. Fixed on the way:
  live test picks only a real `Revit` process; `PipEnvironmentTests` became
  `ParserScriptTests` (320 s to 23 s); one adapter pack per class (128 s to 67 s); tests
  use `REVITDEVTOOL_DATA_DIR` instead of the real `%AppData%\RevitDevTool`; pydevd
  installs once instead of order-dependent Skips; stale prompt parser test removed;
  Daemon tests run class-parallel (22 s to 9 s).
- [x] Format hook removed; `scripts/format-lint.ps1` runs once at task end.
- [x] Tests that waited out timeouts now wait for the event (tunnel status, a dialog on a
  pumping thread, `RejectingServer`): Daemon 22 s to 6 s, Hosting up to 24 s to 1.5 s.
- [x] 69 tests without an assert triaged: 22 kept (helper or harness asserts), 13 kept as
  no-throw contracts, 22 given a real assert, 12 deleted as duplicates or coverage-only.
  A reported `DevToolsPipeServer` leak did not reproduce, so no product change; the test
  now asserts that no client connects after Stop.
- [x] A: cause was the fake pipe serving one client while live Daemons on the
  machine took its only listener. `FakeMcpHostPipe` now serves many clients like
  `McpPipeServer`; `[Ignore]` removed; 41/41 for 10 runs.
- [x] C: docs and skills rewritten; Daemon docs and samples moved from
  `search_dynamic` / `invoke_dynamic` to `code_mode`.
- [x] D: ColorPicker tests find parts by AutomationId and act through FlaUI UIA3
  patterns; the eye dropper asserts the popup stays open, with no sampling seam.
- [x] B: cleanup applied (stale `tests/` folders removed, slnx path casing fixed).
- [ ] CI wiring is a separate session; this plan closes after the commit.

## Decisions

- 2026-11-08: Legacy MCP test project names stay; renaming is churn without a
  behavior change.
- 2026-11-08: UI interaction tests find elements by AutomationId and act through
  UI Automation patterns with FlaUI.UIA3; no screen clicks or synthetic mouse
  events (owner).

## Validation

- Focused proof: `dotnet run --project tests/DevTools.Mcp.Client.Tests/DevTools.Mcp.Client.Tests.csproj`
  ten times, 0 failures, 0 skipped.
- Repository-required checks: `dotnet run --project build -- ci <product>` for
  `daemon`, `addin`, `nettest`, `pytest`.

## Result

Every `tests/*.Tests` project, `pixi run test`, and `scripts/test-pyserver.ps1` pass
locally (2026-11-09). Product seams with production defaults let tests skip fixed
timings, live network, and real stdin: `HostConnections` interval, `PackageVersionChecker`
`HttpClient`, `StdioHostedService` input, `SentryTelemetry` `ITransport`. Remaining limits
are in [test-matrix.md Current gaps](../../agents/test-matrix.md#current-gaps). CI wiring
is a separate session.

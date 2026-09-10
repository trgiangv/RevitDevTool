# Execution Plan: Narrow testing contracts

Date: 2026-09-10

## Status

Completed 2026-09-10 — contracts closed; follow-up Host merge is
`DevTools.Testing.Host` (NUnit/TUnit providers). `machine-run` rename
deferred.

## Outcome

Cross-process run uses one `TestingRunRequest` (one `RunId`) from adapter through TestRunner to the host. Selection is a closed union. Testhost registration is one locked slot. Cancel, Hello, timeouts, and machine-run events match that flow.

## Context

Architecture: [`docs/architecture/Testing/README.md`](../../architecture/Testing/README.md).
Product: [`docs/product/host-testing.md`](../../product/host-testing.md).

Local discovery stays host-free. `testing/discover` stays absent.

## Scope

In scope:

- JSON `machine-run` stdin (preserve RunId and run DTO).
- Atomic `HostTestDiscovery.Register` (no production PassThrough).
- `TestingSelection` as All / TestIds / FrameworkFilter / Names.
- Delete dead shared discovery/run fields.
- Real `testing/cancel` + cooperative runner signal.
- Machine-run NDJSON events; Hello validation; RequestTimeout vs PerTestTimeout.

## Progress

- [x] P0 JSON machine-run + atomic registration.
- [x] P0 TestingSelection discriminated union.
- [x] P1 delete dead shared discovery/run fields.
- [x] Cancel, events, Hello, timeout rename, stdin/Dispose.
- [x] Follow-ups: TUnit Names/pending-cancel, PublishRunAsync behavior tests,
      dead CLI constants, `Clear()` internal, injected machine-run stdin,
      repo `NuGet.config` source mapping.
- [x] Split NUnit testhost discoverer vs run-mapper (no `partial`).
- [x] CS0433: TestAdapter.Tests builds adapter with `ILRepackable=false`.
- [x] MTP cancel registration; unconstrained run error node; NUnit
      `ArgumentException` → `testing/invalid_request`; session Cancel swallows
      `ObjectDisposedException`; NUnit split asserted by loaded types.

## Validation

- Focused: Transport golden invocation round-trip; ProcessTestRunnerClient stdin; Runner MachineRun preserves RunId; Adapter architecture + HostTestSession.
- Human `run` CLI still works (ComposedRunCommandTests).
- TestRunner.Tests builds with default `dotnet run` (`SelfContained=false`).

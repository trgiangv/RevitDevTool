# 0017 NUnit Host Test Output Routing

Date: 2026-08-14

## Status

Accepted

## Context

During `nunit/run`, NUnit replaces `Console.Out` so it can fill
`ITestResult.Output`. Process `Trace` / `Debug` still fan out through
`Trace.Listeners` (host `LoggerTraceListener` when tracing is on). A Host-side
`NUnitLoggingTraceListener` plus an `ILogger` dump of `CaseResult.Output` echoed
the same Trace bytes back into the pane, often with `DefaultTraceListener`
headers (`Revit.exe Error: 0 :`) and no extra grouping value.

NUnit does not put `Trace.WriteLine` / `Debug.WriteLine` into `ITestResult.Output`.
IDE Test Explorer / MTP stdout reads `CaseResult.Output` only.

## Decision

1. **Pane is the process Trace pipeline.** `Trace` / `Debug` during a host test
   reach the DevTools monitor only via existing `Trace.Listeners`. Do not add an
   NUnit-specific log listener or dump `CaseResult.Output` through
   `ILogger<NUnitRuntimeManager>`.

2. **IDE stdout is `CaseResult.Output`.** Runtime `NUnitRunTraceScope` is a
   silent per-case buffer of `Trace` / `Debug`. On `TestFinished` it merges that
   buffer with NUnit Console/`TestContext` text. MTP maps the field to
   `StandardOutputProperty`; VSTest maps it to `StandardOut`.

3. **Console to the pane is a one-shot write-through.** After the IDE buffer is
   taken, Runtime forwards trimmed `ITestResult.Output` with `Trace.Write` while
   capture is suspended, so the pane sees Console without duplicating IDE
   stdout. Trailing CR/LF is stripped so the monitor does not show a blank line.

4. **net48 and modern hosts share this split.** Collectible ALC (net8+) does not
   isolate `Trace.Listeners`. Revit 2022 net48 is one AppDomain. The extra
   listener exists for IDE capture, not because ALC hid Trace from the pane.

## Alternatives Considered

1. **Host `NUnitLoggingTraceListener` + `[NUnit:{TestName}]` ILogger dump.**
   Rejected: duplicates process Trace, loses Console unless dumped, and the dump
   reintroduces DefaultTraceListener noise.
2. **Re-enable `ConsoleRedirector` for the whole NUnit run.** Rejected: NUnit
   still replaces `Console.Out`; wrapping it into Trace would also refill the
   IDE Trace buffer and double Console in Test Explorer.
3. **Leave Console off the pane.** Rejected: testers expect `Console.WriteLine`
   in the host monitor; write-through is enough without grouping or extra
   format.

## Consequences

Positive:

- Pane and IDE each have one owner for each API (`Trace`/`Debug` vs Console).
- Host logging code stays out of the NUnit run path.

Tradeoffs:

- Pane lines are not prefixed with NUnit test name; grouping is the IDE test
  node. Console appears on the pane at case finish, not streamed live.
- `LogLevelDetector` may still tag messages that contain `ERR` / `error`.

## Follow-Up

- Observable contract: [`docs/product/nunit-host-testing.md`](../product/nunit-host-testing.md)
- ConsoleRedirector exception: [`docs/architecture/Logging/output.md`](../architecture/Logging/output.md)

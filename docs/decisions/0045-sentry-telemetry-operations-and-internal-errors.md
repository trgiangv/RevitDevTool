# 0045 Sentry Telemetry: Operations, Sessions, Internal Errors Only

Date: 2026-10-24

## Status

**Accepted 2026-10-24. Implemented 2026-10-24.**

Keeps the [0018](0018-host-identity-and-out-of-process-infrastructure.md)
boundary: `DevTools.Telemetry` references only `DevTools.Hosting` and Sentry.
User-facing data rules live in
[Privacy & Telemetry](../docs/getting-started/Privacy-And-Telemetry.md).

## Context

The old `SentryTelemetryService` had these defects:

| Defect | Effect |
|--------|--------|
| `ExecutionOrchestrator` sent every `ExecutionResult.Exception` | Sentry issues were mostly script errors written by users, not RevitDevTool errors |
| `TelemetryPathScrubber` ran on tags only | Exception messages and stack traces could carry user paths and document names |
| `ConfigureScope` set `telemetry.feature` / `provider` on the shared scope | Tags leaked into later events |
| Usage counters (execution, MCP, geometry type, log line level) went into one Info event at `Flush` | Sentry cannot aggregate extras; the event used error quota; the data was lost on a crash |
| SDK default hooks (AppDomain unhandled, unobserved task, DiagnosticSource) | The process is shared with Revit/AutoCAD and other add-ins, so their failures were reported as ours |
| `AppDomain.UnhandledException` rarely fires in a host | Revit/AutoCAD catch command, external-event, and dispatcher exceptions themselves |
| Test runs (pytest, IronPython unittest, NUnit, MSTest, TUnit) and visualization errors | Not measured at all |

## Decision

### Use Sentry primitives, not custom aggregation

- One **transaction** per operation. The transaction name is `{operation}.{kind}`. `kind` has low cardinality (provider or framework). Script names, tool names, and document names are never sent.
- **Release health** (`AutoSessionTracking`) gives the number of sessions and the crash-free rate per host and release.
- **Errors** only for internal failures.
- The SDK background queue sends data as it occurs. There is no aggregation in RAM and no summary event at shutdown. `Dispose` closes the SDK, which flushes the queue (`ShutdownTimeout` is 2 s).
- `TracesSampleRate = 1.0`. Each operation is one span. The free plan has 5M spans and 5k errors per month, so the error quota is the limit to protect, not spans.

| Operation | Where | `kind` |
|-----------|-------|--------|
| `devtool.execution` | `ExecutionOrchestrator.ExecuteAsync` | Provider (`Python`, `CSharp`, …) |
| `devtool.mcp` | `McpCallFilter` | `BuiltIn` or the toolset `SourceKind` |
| `devtool.test` | `PytestRequestHandler`, `IpyTestRequestHandler`, `TestingRequestHandler.RunProvider` | `pytest`, `ipytest`, `NUnit`, `MSTest`, `TUnit` |
| `devtool.visualization` | `VisualizationServer.ReportError` (error only, no transaction) | Server type name |

### Small facade

```text
ITelemetry           StartOperation(operation, kind) / ReportError(ex, operation, kind)
ITelemetryOperation  Complete() / Cancel() / Fail(ex = null)
```

- Callers state what happened. `SentryTelemetry` selects the span status and decides whether to send an error.
- `Fail()` without an exception: the operation failed because of user code, user input, or the environment. Status `invalid_argument`. No error event.
- `Fail(ex)`: an error event is sent only when `TelemetryReporter.ShouldReport(ex)` is true. Status `internal_error`. A cancellation exception becomes `Cancel()`.
- A failing test is a result, not a failure: the run is `Complete()`. A poisoned test session is `Fail()`.
- No outcome enum, no `Flush` on the interface. `NoOpTelemetry` is used when telemetry is disabled or there is no DSN.

### Internal vs external

`TelemetryReporter.ShouldReport(ex)` is true only when all three conditions are true:

1. The exception is not a cancellation (`OperationCanceledException`, `TimeoutException` in the inner chain).
2. It is not marked external. `MarkExternal(ex)` puts a key in `Exception.Data`. The mark travels with the same exception object through rethrows.
3. It is internal: the stack of the exception or of an inner exception has a frame from an assembly whose name starts with `DevTools.`, `RevitDevTool`, or `AcadDevTool`. The compare ignores case, because the AutoCAD assembly is `ACadDevTool` and ILRepack merges `DevTools.*` into it.

External code is marked where DevTools calls it:

- Revit `IExternalCommand.Execute`, and AutoCAD `[CommandMethod]` invoke.
- IronPython compiled script execution.
- CPython `scope.Exec` of the user script.
- MCP tools that are not built-in.

### One filter in `BeforeSend`

`BeforeSend` drops every event whose exception fails `ShouldReport`. This
covers explicit reports and the SDK's own capture.

`SentryEventScrubber` then runs on the events that remain:

- Removes `ServerName`.
- Redacts absolute paths in the message, the exception values, and the mechanism data.
- Removes `AbsolutePath` from stack frames and keeps only the file name.
- `SendDefaultPii = false`. `User.Id` is the anonymous user identity. No separate `installation_id` tag.

### User identity: one user on one machine

- `UserIdentity.Get() = HMAC-SHA256(key = "RevitDevTool.Telemetry.UserIdentity.v1", data = MachineGuid + "|" + account SID)`, first 16 bytes as 32 hex characters.
- `MachineGuid` is read from `HKLM\SOFTWARE\Microsoft\Cryptography` (64-bit registry view). The SID is used, not the user name: it does not change on rename and holds no personal name.
- Nothing is stored on disk. The old `installation_id.txt` is no longer read. Each existing install is counted once as a new user (accepted).
- If either value cannot be read, a random ID is used for that process only.

### SDK hooks

- **On:** the SDK's `AppDomain.UnhandledException` integration. It marks the exception as unhandled and the session as crashed. The `BeforeSend` filter drops exceptions from Revit, AutoCAD, and other add-ins. The hosts have no unhandled-exception handler of their own.
- **Off:**
  - `DisableUnobservedTaskExceptionCapture`: these come from GC timing and have low value.
  - `DisableAppDomainProcessExitFlush`: `Dispose` closes the SDK.
  - On .NET (Core) only: `DisableDiagnosticSourceIntegration` and `DisableSystemDiagnosticsMetricsIntegration`, so that spans and metrics from other add-ins do not join our transactions.

### Visualization errors are deduplicated

Render and buffer errors can repeat each frame. `ReportError` sends only the first error for each (operation, kind, exception type) in one session.

### Boundary

- The DSN is a private constant in `SentryTelemetry` (an internal constructor takes a test DSN so tests never send to the production project). `AddTelemetryService` is called from each host's `AddApplicationServices` (next to `IHostAppInfo`) and takes only the enable switch from settings. There is no `SENTRY_DSN` override. This replaces the 0018 rule that the composition root passes the DSN.
- `DevTools.Testing.Host` references `DevTools.Telemetry`, so `RunProvider` owns the test operation. This was chosen over a host-side decorator that parses `testing/run` responses.
- The test handlers take `ITelemetry?` as an optional argument and use `NoOpTelemetry` when it is null.
- `DevTools.Logging` does not reference Telemetry.

## Alternatives Considered

1. **Keep RAM counters and one usage event at shutdown.** Rejected. The data cannot be queried, the event uses error quota, and the data is lost on a crash.
2. **Call `SentrySdk` directly from each caller, with no facade.** Rejected. The internal/external rule would be copied to each caller, and orchestrator tests could not check the calls.
3. **Classify failures with an outcome enum (`TelemetryOutcome`).** Rejected. Callers would repeat the classification. `Complete` / `Cancel` / `Fail` is shorter.
4. **Sample MCP calls (25 %).** Rejected. The span quota is large, and sampling makes the counts less exact.
5. **Send log lines (Sentry Logs) or ILogger errors automatically.** Rejected. Log text can carry user content, and many `LogError` calls log script failures.
6. **Offline cache (`CacheDirectoryPath`).** Deferred. The behavior when several host processes share one directory is not documented, and the privacy page says no telemetry is stored on disk.
7. **A random GUID in `installation_id.txt`.** Rejected. Deleting the file or reinstalling counts the same user again.
8. **Send the Windows user name and machine ID.** Rejected. Both are personal data, and the privacy page promises that the user name is never sent.
9. **Keep a host `AppDomain.UnhandledException` handler that calls `ReportCrash`.** Rejected. The SDK integration does the same work once the filter is in `BeforeSend`.
10. **A private Sentry hub.** Not possible. `Hub` is internal in Sentry 6.12.1, so the static `SentrySdk` is the only entry point.

## Consequences

Positive:

- Sentry issues show RevitDevTool defects, not user script errors or errors from other add-ins.
- Counts, failure rate, and duration per provider and framework come from the transactions, with no custom aggregation code.
- The facade is small. Test handlers and visualization use the same rules.

Tradeoffs:

- **Static `SentrySdk`.** Another add-in in the same process that uses the same Sentry assembly can replace the hub.
- **Queue limits.** The SDK queue holds 30 items. Bursts of MCP calls on a slow network can drop transactions. Items still in the queue are lost if the host is killed.
- **Stack-based detection.** An exception that was never thrown has no frames, so it is not reported. An error in a user test assembly that is raised by DevTools loading code (for example a bad image) is reported as internal.
- **Crash sessions from other add-ins.** `BeforeSend` drops the crash event when the exception is not internal. It is not verified whether the session is still marked crashed in that case.
- **User identity limits.** A Windows reinstall gives a new ID. Machines cloned from an image without sysprep share `MachineGuid`; the SID reduces collisions, but the same domain account on cloned machines can still collide. The hash is pseudonymous data, not anonymous data, under GDPR.
- **Path scrubbing redacts too much.** Path segments can contain spaces, so the text after a path on the same line is redacted too.

## Follow-Up

- In the Sentry project, turn on spike protection and "Prevent storing IP addresses". Both are project settings, not code.
- Do a live check on one Revit year: see transactions and sessions in Sentry, and make sure a script error does not create an issue.
- Optional: add child spans for execution (resolve dependencies / compile / run).
- Optional: add `ReportError` to the catch blocks on the critical path, for example `ExternalEvent` handlers and bridge listeners.
- Remove the unused `LoggerTraceListener` `onLogLineDelivered` argument when its tests are changed.

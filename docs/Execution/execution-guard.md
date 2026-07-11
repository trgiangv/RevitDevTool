# Execution Guard: Dialog & Failure Suppression

## Purpose

Prevents AI-driven execution (MCP tools, pytest, scripts) from hanging on Revit dialogs or transaction failure UI. When a human cannot interact with the UI, the guard auto-dismisses dialogs and auto-resolves/rollbacks failures.

---

## Architecture

```
Caller sets ExecutionGuardContext.Mode = Suppress
    → RevitHostContextExecutor reads mode from AsyncLocal
        → ExecutionGuard.Begin(Suppress) returns IDisposable
            → DialogSuppressionScope subscribes to UIApplication.DialogBoxShowing
            → FailureSuppressionScope subscribes to Application.FailuresProcessing
        → On scope dispose: unsubscribes if outermost (reference-counted)
    → Only unresolvable rollbacks published to ExecutionGuardContext.RollbackSummary
    → All other suppressions logged at Debug level (internal)
```

---

## Source Map

| File | Role |
|------|------|
| `source/DevTools.Execution.Abstractions/ExecutionGuardMode.cs` | Enum: `Passthrough`, `Suppress` |
| `source/DevTools.Execution.Abstractions/ExecutionGuardContext.cs` | AsyncLocal ambient: mode + rollback summary |
| `source/RevitDevTool.Core/Execution/IExecutionGuard.cs` | Interface (no logging deps) |
| `source/RevitDevTool.Core/Execution/ExecutionGuard.cs` | Factory combining scopes |
| `source/RevitDevTool.Core/Execution/DialogSuppressionScope.cs` | `DialogBoxShowing` handler |
| `source/RevitDevTool.Core/Execution/FailureSuppressionScope.cs` | `FailuresProcessing` handler |
| `source/RevitDevTool.Core/Execution/ExecutionGuardFeedback.cs` | Internal tracking counters |

---

## Mode Selection

| Caller | Mode | Rationale |
|--------|------|-----------|
| MCP tool/prompt/resource | `Suppress` | AI cannot click dialog buttons |
| Pytest session | `Suppress` | Tests must be deterministic |
| UI command tree / debugger | `Passthrough` (default) | User expects normal dialog behavior |

---

## Dialog Suppression

Subscribes to `UIApplication.DialogBoxShowing` and calls `OverrideResult()`:

| Dialog ID | Override | Reason |
|-----------|---------|--------|
| `TaskDialog_Save_File` | No | Don't save unexpected changes |
| `TaskDialog_Save_Family` | No | Don't save family edits |
| `TaskDialog_Really_Print_Or_Export_Temp_View_Modes` | No | Skip export warnings |
| `TaskDialog_Unresolved_References` | No | Skip reference warnings |
| All others | 1 (OK/dismiss) | Generic dismiss |

**What is NOT affected:**
- `PickObject` / `PickElementsByRectangle` — modal selection, not a `DialogBoxShowing` event
- Modeless dockable panes — not events-based

---

## Failure Suppression

Subscribes to `Application.FailuresProcessing` (adapted from Nice3point RevitToolkit):

| Severity | Action | Result |
|----------|--------|--------|
| Warning | `DeleteWarning()` | Transaction commits normally |
| Error with resolution | `ResolveFailure()` | Transaction commits normally |
| Error without resolution | `ProceedWithRollBack` + `SetClearAfterRollback` | Transaction ROLLED BACK |

---

## Feedback Philosophy

**Only rollbacks are surfaced to AI.** Everything else is noise:

- Warnings dismissed → operation succeeded, AI doesn't need to know
- Errors auto-resolved → Revit fixed it, operation succeeded
- Dialogs dismissed → expected behavior during automation
- **Unresolvable failure → ROLLBACK → AI MUST KNOW** (operation failed)

Warnings remain queryable via `Document.GetWarnings()` if the agent needs quality checks after execution.

---

## Reference Counting

Both scopes use a shared static `_refCount`:
- First scope entering → subscribes to event
- Nested scopes → increment count, share subscription
- Last scope disposing → unsubscribes from event

This is safe for the MCP FIFO queue where multiple tool calls may be awaiting execution.

---

## Design Principles

- **RevitDevTool.Core has zero logging dependency** — guard is pure Revit API code
- **Logging lives in host executor** (`RevitHostContextExecutor`) — the host project owns all cross-cutting concerns
- **AsyncLocal propagation** — avoids changing `IHostContextExecutor` interface; each async flow is isolated
- **net48 compatible** — uses `Polyfill` package for `Lock` type; no .NET 9+ APIs

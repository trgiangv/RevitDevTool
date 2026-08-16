# Execution System Digest

Deep source: `docs/architecture/Execution/README.md` and sub-pages (`code-execution.md`, `execution-guard.md`, `pytest-bridge.md`, `mcp-dispatch.md`).

## Core Shape

- Orchestrator: `source/DevTools.Execution/Services/ExecutionOrchestrator.cs`.
- DI: `source/DevTools.Execution/ExecutionExtensions.cs`.
- Providers discover roots and nodes. Strategies execute selected nodes.
- `AssemblyExecutionProvider` handles `.dll`.
- `ScriptExecutionProvider` handles directories with `*script.py`, `*script.fsx`, and `*script.csx`.
- `ContainerMode` (`Script`, `Assembly`) describes how code is organised; `ExecutionMode` (`Python`, `IronPython`, `FSharp`, `CSharp`, `Dotnet`, `Unsupported`) describes the execution backend.

## Host Integration

Shared execution does not own host APIs. It calls abstractions defined in `source/DevTools.Execution.Abstractions/`:

- `IHostContextExecutor` — run code on host main thread
- `ICommandDiscovery` — discover executable nodes
- `ICommandRunner` — invoke a discovered command
- `IDocumentBridge` — open/close documents (Revit/AutoCAD)
- `ICompiledScriptBridge` — compiled script caching
- `ExecutionGuardMode` — enum: `Passthrough`, `Suppress`
- `ExecutionGuardContext` — ambient `AsyncLocal` propagating mode + feedback summary

Additional interfaces in `source/DevTools.Execution/Interfaces/`:

- `IPythonBridge` — Python.NET runtime
- `IIronPythonBridge` — IronPython runtime
- `IDebuggerBridge` (in `DevTools.Presentation.Interfaces`) — attach/detach debugger

Host projects register implementations via `AddExecutionServices()`:

- Revit: `source/RevitDevTool/Composition/RevitServiceRegistration.cs`
- AutoCAD: `source/AcadDevTool/Composition/AcadServiceRegistration.cs`

The `AddExecutionServices()` call is the central DI hub — it registers orchestrator, MCP in-host pipe server (`DevToolsPipeServer`), pytest handler, and all strategy factories.

## Execution Guard (Dialog & Failure Suppression)

Revit-specific guard that auto-dismisses dialogs and auto-resolves/rollbacks transaction failures during AI-driven execution. Architecture:

```
Caller sets ExecutionGuardContext.Mode → RevitHostContextExecutor reads mode
    → ExecutionGuard.Begin(mode)
        → DialogSuppressionScope (UIApplication.DialogBoxShowing)
        → FailureSuppressionScope (Application.FailuresProcessing)
    → Only rollback info published to ExecutionGuardContext.RollbackSummary
    → Warnings/dialogs suppressed silently (logged at Debug level)
```

### Files

| File | Role |
|------|------|
| `source/DevTools.Execution.Abstractions/ExecutionGuardMode.cs` | Mode enum: `Passthrough`, `Suppress` |
| `source/DevTools.Execution.Abstractions/ExecutionGuardContext.cs` | AsyncLocal: mode + rollback summary |
| `source/RevitDevTool.Core/Execution/IExecutionGuard.cs` | Guard factory interface (no logging deps) |
| `source/RevitDevTool.Core/Execution/ExecutionGuard.cs` | Factory combining scopes |
| `source/RevitDevTool.Core/Execution/DialogSuppressionScope.cs` | DialogBoxShowing handler |
| `source/RevitDevTool.Core/Execution/FailureSuppressionScope.cs` | FailuresProcessing handler |
| `source/RevitDevTool.Core/Execution/ExecutionGuardFeedback.cs` | Internal tracking (not public) |

### Mode Selection by Caller

| Caller | Mode | Rationale |
|--------|------|-----------|
| MCP tool/prompt/resource | `Suppress` | AI cannot interact with UI |
| Pytest session | `Suppress` | Tests must be deterministic |
| UI command tree / debugger | `Passthrough` | User may want dialogs |

### Feedback Philosophy

Only **unresolvable failure rollbacks** are surfaced to AI callers via `ExecutionGuardContext.RollbackSummary`. All other suppressions (warnings dismissed, errors auto-resolved, dialogs auto-dismissed) are silent — the operation succeeded and AI doesn't need to know. Warnings remain queryable via `Document.GetWarnings()` if the agent needs quality checks.

### Key Design Decisions

- **RevitDevTool.Core has no logging dependency**: guard is pure Revit API; logging lives in host executor.
- **Reference-counted scopes**: nested MCP calls share one event subscription; safe for FIFO queue.
- **AsyncLocal propagation**: avoids changing `IHostContextExecutor` interface; each async flow isolated.
- **Only rollback feedback**: AI only needs to know when operations fail, not when warnings are dismissed.
- **net48 compatible**: uses `Polyfill` package for `Lock` type; no .NET 9+ APIs.
- **PickObject not affected**: modal selection is not a `DialogBoxShowing` event.
- **Save prompts → No**: `TaskDialog_Save_File` overrides with `TaskDialogResult.No`.

## Script Modes

- Python: PEP 723 dependencies through `Parser.py`, Pixi preferred, pip/pyRevit fallback. Startup runs `pixi --version` after install check; non-zero exit → pip backend.
- IronPython: Python files ending `_ipy_script.py`.
- F#: `.fsx`, NuGet resolution under `%APPDATA%\RevitDevTool\nuget`, 30 second compile timeout.
- C#: `.csx`, Roslyn compilation cache, 30 second compile timeout.

## Change Checklist

- Confirm whether the change is provider, strategy, orchestrator, node model, file watcher, package service, or host adapter.
- Keep host-thread rules in host adapters.
- If changing execution guard: verify both `Suppress` and `Passthrough` paths work; test reference counting with nested calls.
- Run the narrowest host build and related unit tests from `verification.md`.
- Remember current tests are shallow; add focused tests for shared pure logic when behavior changes.

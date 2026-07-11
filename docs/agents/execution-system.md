# Execution System Digest

Deep source: `docs/Execution/README.md`.

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

Additional interfaces in `source/DevTools.Execution/Interfaces/`:

- `IPythonBridge` — Python.NET runtime
- `IIronPythonBridge` — IronPython runtime
- `IDebuggerBridge` (in `DevTools.Presentation.Interfaces`) — attach/detach debugger

Host projects register implementations via `AddExecutionServices()`:

- Revit: `source/RevitDevTool/Hosting/RevitHostingExtensions.cs`
- AutoCAD: `source/AcadDevTool/Hosting/AcadHostingExtensions.cs`

The `AddExecutionServices()` call is the central DI hub — it registers orchestrator, MCP in-host pipe server (`DevToolsPipeServer`), pytest handler, and all strategy factories.

## Script Modes

- Python: PEP 723 dependencies through `Parser.py`, Pixi preferred, pip/pyRevit fallback.
- IronPython: Python files ending `_ipy_script.py`.
- F#: `.fsx`, NuGet resolution under `%APPDATA%\RevitDevTool\nuget`, 30 second compile timeout.
- C#: `.csx`, Roslyn compilation cache, 30 second compile timeout.

## Change Checklist

- Confirm whether the change is provider, strategy, orchestrator, node model, file watcher, package service, or host adapter.
- Keep host-thread rules in host adapters.
- Run the narrowest host build and related unit tests from `verification.md`.
- Remember current tests are shallow; add focused tests for shared pure logic when behavior changes.

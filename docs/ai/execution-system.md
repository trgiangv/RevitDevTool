# Execution System Digest

Deep source: `docs/Execution/README.md`.

## Core Shape

- Orchestrator: `source/DevTools.Execution/Services/ExecutionOrchestrator.cs`.
- DI: `source/DevTools.Execution/ExecutionExtensions.cs`.
- Providers discover roots and nodes. Strategies execute selected nodes.
- `AssemblyExecutionProvider` handles `.dll`.
- `ScriptExecutionProvider` handles directories with `*script.py`, `*script.fsx`, and `*script.csx`.
- `ExecutionMode` currently includes `Script`, `Assembly`, `Python`, `IronPython`, `FSharp`, and `CSharp`.

## Host Integration

Shared execution does not own host APIs. It calls abstractions:

- `IHostContextExecutor`
- `ICommandDiscovery`
- `ICommandRunner`
- `ICompiledScriptBridge`
- `IPythonBridge`
- `IIronPythonBridge`
- `IDebuggerBridge`

Host projects register implementations:

- Revit: `source/RevitDevTool/Hosting/RevitHostingExtensions.cs`
- AutoCAD: `source/AcadDevTool/Hosting/AcadHostingExtensions.cs`

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

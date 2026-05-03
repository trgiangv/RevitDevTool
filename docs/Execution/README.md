# Execution System Architecture

The Execution system is the core engine of RevitDevTool — a multi-language code execution framework that discovers, loads, and runs code inside the Revit process. It supports three execution modes: .NET assemblies, Python scripts, and F# scripts, all unified through a tree-view UI.

**Source:** `source/DevTools.Execution/`

---

## High-Level Architecture

```mermaid
flowchart TB
    subgraph UI["Revit UI — Dockable Panel"]
        TreeView["TreeView\n(ObservableCollection&lt;ExecutionNodeBase&gt;)"]
    end

    subgraph Orchestrator["ExecutionOrchestrator"]
        LoadPath["LoadFromPathAsync()"]
        Reload["ReloadAsync()"]
        Execute["ExecuteAsync()"]
        Events["TreeChanged / ExecutionProgressChanged"]
    end

    subgraph Providers["IExecutionProvider × 2"]
        direction LR
        Assembly["AssemblyExecutionProvider\n(.dll → IExternalCommand)"]
        Script["ScriptExecutionProvider\n(*script.py / *script.fsx)"]
    end

    subgraph Strategies["IExecutionStrategy × 3"]
        direction LR
        DotNetStrat["AssemblyExecutionStrategy\n(HostContext → ICommandRunner)"]
        PythonStrat["PythonExecutionStrategy\n(PEP 723 → UV/pip → exec)"]
        FSharpStrat["FSharpExecutionStrategy\n(FSI compile → RunCommand)"]
    end

    subgraph Services["Services"]
        FileWatch["FileWatcherService\n(3-layer: FileContent / DirStructure / RootLifecycle)"]
        TreeState["TreeStateManager\n(Capture / Restore)"]
        PackageSvc["PackageService\n(Pixi / pip / NuGet)"]
    end

    TreeView -->|user action| Orchestrator
    Orchestrator --> Providers
    Providers --> Strategies
    Orchestrator --> Services
    FileWatch -->|FileChanged| Orchestrator
    Orchestrator -->|TreeChanged| TreeView
```

---

## Core Interfaces

### `IExecutionProvider` — Detection & Discovery

```csharp
public interface IExecutionProvider
{
    string Name { get; }                        // "DotNet", "Script"
    int Priority { get; }                       // Higher = checked first
    bool CanHandle(string path);                // .dll → true, folder → true
    Task<IEnumerable<ExecutionNodeBase>> DiscoverAsync(string path, ...);
    IEnumerable<string> GetWatchPatterns();     // "*.dll", "*.py", "*.fsx"
    bool ValidatePath(string path);
}
```

| Provider | `Name` | `Priority` | `CanHandle` | Discover |
|----------|--------|-----------|-------------|----------|
| `AssemblyExecutionProvider` | `"DotNet"` | `100` | `.dll` files | Parses `IExternalCommand` from assembly |
| `ScriptExecutionProvider` | `"Script"` | `-100` | Directories | Recurses folder tree, finds `*script.py` and `*script.fsx` |

### `IExecutionStrategy` — Execution

```csharp
public interface IExecutionStrategy
{
    Task<ExecutionResult> ExecuteAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
```

Each node in the tree carries its own `IExecutionStrategy` instance. Execution is dispatched via `IHostContextExecutor` which marshals the call onto the Revit API thread (via `ExternalEvent`).

---

## Node Tree Model

```mermaid
classDiagram
    class TreeNodeBase {
        +string Name
        +bool IsExpanded
        +bool IsSelected
        +bool IsVisible
    }
    class ExecutionNodeBase {
        +string Id
        +ObservableCollection~ExecutionNodeBase~ Children
        +NodeType NodeType
        +IExecutionStrategy? ExecutionStrategy
        +bool IsExecutable
        +ExecuteAsync() Task~ExecutionResult~
    }
    class ExecutionNodeRoot {
        +string RootPath
        +ExecutionMode ProviderType
    }
    class ExecutionNodeIntermediate {
        +string FullPath
    }
    class ExecutionNode {
        +string ExecutablePath
        +ExecutionMode ProviderType
        +string? SourceFilePath
    }

    TreeNodeBase <|-- ExecutionNodeBase
    ExecutionNodeBase <|-- ExecutionNodeRoot
    ExecutionNodeBase <|-- ExecutionNodeIntermediate
    ExecutionNodeBase <|-- ExecutionNode

    note for ExecutionNodeRoot "Assembly (.dll) or Root Folder"
    note for ExecutionNodeIntermediate "Namespace or SubFolder"
    note for ExecutionNode "IExternalCommand / Python Script / F# Script"
```

### `ExecutionMode` Enum

```csharp
public enum ExecutionMode
{
    Assembly,   // .dll assemblies
    Script,     // Directory-based (Python + F#)
    Python,     // Individual Python scripts
    FSharp      // Individual F# scripts
}
```

### Node ID Scheme

| Type | Pattern | Example |
|------|---------|---------|
| .NET Assembly | `dotnet://{path}` | `dotnet://C:/Plugins/Tools.dll` |
| .NET Namespace | `dotnet://{path}\|{ns}` | `dotnet://C:/Plugins/Tools.dll\|MyCompany.Commands` |
| .NET Command | `dotnet://{path}\|{class}` | `dotnet://C:/Plugins/Tools.dll\|MyCompany.Commands.PurgeCommand` |
| Python Script | `python://{path}` | `python://C:/Scripts/data_analysis_script.py` |
| F# Script | `fsharp://{path}` | `fsharp://C:/Scripts/excel_export_script.fsx` |

---

## Execution Flows

### 1. .NET Assembly Execution (`ExecutionMode.Assembly`)

```mermaid
sequenceDiagram
    participant User as User
    participant Orch as Orchestrator
    participant Provider as AssemblyExecutionProvider
    participant Disc as ICommandDiscovery
    participant Strat as AssemblyExecutionStrategy
    participant Host as IHostContextExecutor
    participant Runner as ICommandRunner

    User->>Orch: LoadFromPathAsync("Tools.dll")
    Orch->>Provider: CanHandle(path) → true
    Provider->>Disc: ParseCommands(assemblyPath)
    Disc-->>Provider: List<CommandItem>
    Provider->>Provider: BuildAssemblyNode()
    Note over Provider: RootNode → NamespaceNode → CommandNode
    Provider-->>Orch: ExecutionNodeRoot (with children)

    User->>Orch: ExecuteAsync(commandNode)
    Orch->>Strat: ExecuteAsync(progress, ct)
    Strat->>Host: ExecuteAsync(() => runner.RunCommand(item))
    Host-->>Strat: ExecutionResult
    Strat-->>Orch: result
    Orch-->>User: UI update
```

**Key points:**
- `.dll` files discovered via `ICommandDiscovery.ParseCommands()` (host-specific: Revit scans `IExternalCommand`, AutoCAD scans `CommandMethodAttribute`)
- Namespace grouping: commands organized by namespace → UI subtree
- Execution: `IHostContextExecutor` dispatches to API thread, `ICommandRunner.RunCommand()` invokes the command

### 2. Python Script Execution (`ExecutionMode.Script`)

```mermaid
sequenceDiagram
    participant User as User
    participant Orch as Orchestrator
    participant Provider as ScriptExecutionProvider
    participant Strat as PythonExecutionStrategy
    participant Init as PythonInitializer
    participant Deps as PythonDepsManager
    participant Exec as PythonExecutor
    participant Host as IHostContextExecutor

    User->>Orch: LoadFromPathAsync("C:/Scripts/")
    Orch->>Provider: CanHandle(path) → true
    Provider->>Provider: BuildFolderTree(root, root)
    loop Folder recursion
        Provider->>Provider: PopulateScripts(*script.py) + PopulateSubFolders
    end
    Note over Provider: Skips ignored dirs (bin, obj, .git, venv, ...)
    Provider-->>Orch: ExecutionNodeRoot (folder tree)

    User->>Orch: ExecuteAsync(scriptNode)
    Orch->>Strat: ExecuteAsync(progress, ct)
    Strat->>Init: InitializeAsync()
    Init->>Init: DetectProvider() → Pixi or Pip fallback
    Init->>Init: PythonEngine.Initialize()
    Strat->>Deps: ResolveDependenciesAsync(provider, scriptPath)
    Note over Deps: Parser.py reads PEP 723 inline metadata
    Note over Deps: Pixi: conda-forge first, PyPI fallback
    Note over Deps: Pip: install via python -m pip
    Strat->>Host: ExecuteAsync(() => executor.Execute(scriptPath, rootPath, ...))
    Exec->>Exec: Prepare scope → compile & exec script
    Host-->>Strat: ExecutionResult
    Strat-->>Orch: result
    Orch-->>User: UI update
```

**Key points:**
- Entry scripts: only files matching `*script.py` are discovered
- Ignored folders: `.git`, `bin`, `obj`, `docs`, `venv`, `node_modules`, `.pytest_cache`, etc.
- PEP 723: inline `# /// script` / `# //+` block parsed by `Parser.py`
- Dual backend: Pixi (preferred, conda-forge + PyPI) → Pip fallback (pyRevit CPython)
- `PythonInitializer.DetectProvider()`: tries Pixi first, falls back to Pip if unavailable
- Execution scope: isolated per-run via `PyModule.NewScope()`, module cache reset after each run

### 3. F# Script Execution (`ExecutionMode.Script`)

```mermaid
sequenceDiagram
    participant User as User
    participant Orch as Orchestrator
    participant Strat as FSharpExecutionStrategy
    participant Cache as FSharpCompilationCache
    participant Exec as FSharpExecutor
    participant Host as IHostContextExecutor
    participant Runner as ICommandRunner

    User->>Orch: ExecuteAsync(fsxScriptNode)
    Orch->>Strat: ExecuteAsync(progress, ct)
    Strat->>Cache: GetOrCompile(scriptPath)
    Cache->>Cache: Parse #r "nuget: ..." directives
    Note over Cache: FSharpDependencyResolver → NugetManager
    Note over Cache: Downloads into %APPDATA%/RevitDevTool/nuget
    Cache->>Exec: Compile via FSharpExecutor
    Note over Exec: FSI session with host API references
    Note over Exec: 30 second compilation timeout
    Exec-->>Cache: compiled command object
    Strat->>Host: ExecuteAsync(() => runner.RunFSharpCommand(compiled))
    Host-->>Strat: ExecutionResult
    Strat-->>Orch: result
```

**Key points:**
- `FSharpCompilationCache`: graph-level caching — parent scripts cache compiled outputs that child scripts reuse
- NuGet resolution: `#r "nuget: Some.Package"` directives parsed, packages downloaded via `NugetInstaller` into `%APPDATA%/RevitDevTool/nuget`
- 30-second compile timeout
- `FSharpDependencyResolver`: resolves NuGet package closure via `nuget.org` API
- Host API refs: `IFSharpHostSupport.GetSessionReferences()` provides RevitAPI.dll etc. to the FSI session

---

## Dependency Resolution

### Python — PEP 723 + Pixi/Pip

```mermaid
flowchart LR
    Script["*.py script"] --> Parser["Parser.py\n(parsed outside Python)"]
    Parser -->|stdin: installed state| Deps["PythonDepsManager\n.ResolveDependenciesAsync()"]
    Deps --> Provider{"Backend?"}
    Provider -->|Pixi| Pixi["PixiEnvironmentProvider\nconda-forge → PyPI fallback"]
    Provider -->|Pip| Pip["PipEnvironmentProvider\npython -m pip install"]
    Pixi --> Installed["Packages installed"]
    Pip --> Installed
    Installed --> Refresh["RefreshImportCache()"]
```

**PEP 723 format:**
```python
# /// script
# dependencies = ["pandas==1.5.3", "numpy>=1.24"]
# ///
```

**Required packages** (always pre-installed):
| Package | Spec |
|---------|------|
| `mcp` | `>=1.27,<2` |
| `pytest` | `>=9.0.3,<10` |
| `debugpy` | `>=1.8,<2` |
| `packaging` | `>=26.0,<27` |

### FSharp — `#r "nuget:"` Directives

```
#r "nuget: Newtonsoft.Json, 13.0.3"
#r "nuget: ClosedXML"
```

- Parsed by `FSharpDependencyResolver`
- Downloaded to `%APPDATA%\RevitDevTool\nuget\{package}\{version}\`
- Closure resolution handled by `NugetManager`

### Package Manager UI

The `PackageService` provides a unified package management interface:
- `ListInstalledPackagesAsync()` — Pixi + Pip + NuGet
- `RemovePackageAsync()` — per-marketplace removal
- `UpdateLatestAsync()` — update to latest
- `RepairAsync()` — remove + reinstall
- `RemoveAllAsync(Marketplace)` — bulk cleanup

---

## FileWatcherService

Three-layer file watching with 500ms debounce:

```mermaid
flowchart TB
    subgraph Layer1["Layer 1: FileContent"]
        FW["FileSystemWatcher\n(*.py, *.fsx, *.dll)\nIncludeSubdirectories: true"]
    end
    subgraph Layer2["Layer 2: DirectoryStructure"]
        DW["FileSystemWatcher\n(DirectoryName only)\nIncludeSubdirectories: true"]
    end
    subgraph Layer3["Layer 3: RootLifecycle"]
        RW["FileSystemWatcher\n(parent folder, root name)\n10s delete confirmation"]
    end
    subgraph Debounce["Debounce (500ms)"]
        Merge["MergeChange()\nModified does not override Created/Deleted"]
    end
    subgraph Output["Event"]
        Changed["FileChanged event\n(Path, OldPath, ChangeType, Scope)"]
    end

    FW --> Debounce
    DW --> Debounce
    RW --> Debounce
    Debounce --> Changed
    Changed -->|Orchestrator.OnFileChanged| Orch["HandleFileChangeAsync()\n→ ReloadAffectedRoot()\n→ HandleRootLifecycleEvent()"]
```

| Scope | Detects | Debounce |
|-------|---------|----------|
| `FileContent` | File created/modified/deleted/renamed | 500ms |
| `DirectoryStructure` | Subdirectory created/deleted/renamed | 500ms |
| `RootLifecycle` | Root folder deleted (10s cooldown), renamed | Immediate (rename), 10s delayed (delete) |

---

## TreeStateManager

State persistence for UI tree view between reloads:

```csharp
public interface ITreeStateManager
{
    TreeState CaptureState(IEnumerable<ExecutionNodeBase> nodes);
    void RestoreState(IEnumerable<ExecutionNodeBase> nodes, TreeState state,
                      bool autoExpandNew = false);
}
```

**State tracked:** Expanded nodes, selected node, highlight ranges, last-executed marker.

---

## DI Registration (`AddExecutionServices()`)

```mermaid
flowchart TB
    subgraph Python["Python Runtime"]
        Pixi["PixiEnvironmentProvider\n(Keyed: Pixi)"]
        Pip["PipEnvironmentProvider\n(Keyed: Pip)"]
        Init["PythonInitializer"]
        Executor["PythonExecutor"]
    end
    subgraph Core["Execution Core"]
        TreeMgr["ITreeStateManager → TreeStateManager"]
        FileWatch["IFileWatcherService → FileWatcherService"]
        Orch["IExecutionOrchestrator → ExecutionOrchestrator"]
        Package["IPackageService → PackageService"]
    end
    subgraph Providers["Execution Providers"]
        Assembly["IExecutionProvider → AssemblyExecutionProvider\n(Keyed: Assembly)"]
        ScriptProv["IExecutionProvider → ScriptExecutionProvider\n(Keyed: Script)"]
    end
    subgraph MCP["MCP Registry + Pipe"]
        PipeSrv["DevToolsPipeServer\n(IHostedService)"]
        Registry["ToolRegistryStore + Providers"]
        Dispatchers["Tool/Prompt/Resource Dispatchers"]
    end
    subgraph Pytest["Pytest Bridge"]
        PytestExec["PytestExecutionService"]
        PytestHandler["PytestRequestHandler"]
    end
```

Full registration in `ExecutionExtensions.AddExecutionServices()` (`source/DevTools.Execution/ExecutionExtensions.cs`).

---

## ExecutionOrchestrator — Full API

```csharp
public interface IExecutionOrchestrator
{
    IEnumerable<ExecutionNodeBase> TreeRoot { get; }

    event EventHandler? TreeChanged;
    event EventHandler<RootRemovedEventArgs>? RootRemoved;
    event EventHandler<ExecutionProgressEventArgs>? ExecutionProgressChanged;

    Task LoadFromPathAsync(string path, CancellationToken ct = default);
    Task<IReadOnlyList<string>> LoadSavedPathsAsync(IEnumerable<string> paths, ...);
    Task ReloadAsync(CancellationToken ct = default);
    ExecutionNodeBase? RemoveNode(ExecutionNodeBase node);
    void ClearAll();
    Task<ExecutionResult> ExecuteAsync(ExecutionNodeBase node, ...);
}
```

**Orchestration flow:**
1. `LoadFromPathAsync` → auto-detects provider via `CanHandle()` sorted by `Priority`
2. On file change → `FileWatcher.FileChanged` → `HandleFileChangeAsync()` → `ReloadAffectedRootAsync()` or `HandleRootLifecycleEventAsync()`
3. Root rename/delete → parent watcher fires → auto-reloads or cleans up
4. Execution → `node.ExecuteAsync()` → strategy → host context → Revit API thread

---

## Error Handling

```mermaid
flowchart LR
    Execute["ExecuteAsync()"] --> Try[try]
    Try --> Strategy["strategy.ExecuteAsync()"]
    Strategy -->|OperationCanceledException| Cancelled["ExecutionResult.Cancelled()"]
    Strategy -->|Exception| Failed["ExecutionResult.Failed(ex)"]
    Strategy -->|Success| Ok["ExecutionResult.Succeeded()"]
    Cancelled --> Finally["IsLastExecuted = false"]
    Failed --> Finally
    Ok --> Finally2["IsLastExecuted = true"]
    Finally --> UpdateTime["LastExecutedTime = now"]
    Finally2 --> UpdateTime
```

---

## Architecture Patterns

| Pattern | Application |
|---------|------------|
| **Provider** | `IExecutionProvider` — auto-detect execution mode from file path |
| **Strategy** | `IExecutionStrategy` — polymorphic execution per mode |
| **Composite** | `ExecutionNodeBase` tree — `Children: ObservableCollection<ExecutionNodeBase>` |
| **Observer** | `FileWatcherService` → `IExecutionOrchestrator.TreeChanged` |
| **State** | `TreeStateManager.CaptureState()` / `RestoreState()` |
| **Template Method** | `PyEnvironmentProvider` — abstract base for Pixi/Pip backends |
| **Chain of Responsibility** | Provider selection: highest priority first, fallback to lower |

---

## Related Documentation

- **[MCP Architecture](../MCP/README.md)** — MCP registry and pipe server
- **[PyTest Architecture](../PyTest/README.md)** — pytest bridge between RevitDevTool and `revitdevtool_pytest`
- **[Logging Architecture](../Logging/README.md)** — Trace capture and visualization routing
- **[Visualization Architecture](../Visualization/README.md)** — DirectContext3D rendering

**Source Code:**
- `source/DevTools.Execution/` — Execution engine
- `source/DevTools.Execution/Services/ExecutionOrchestrator.cs` — Main orchestrator
- `source/DevTools.Execution/Providers/` — All three providers + strategies
- `source/DevTools.Execution/External/` — Named pipe server, MCP bridge, pytest bridge

---

_Last updated: 2026-05-03_

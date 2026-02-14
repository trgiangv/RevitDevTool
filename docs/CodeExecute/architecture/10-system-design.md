# 10: Architecture Overview - System Design

**Objective:** Understand how CodeExecute components work together  
**Prerequisite:** Familiarity with design patterns (Provider, Strategy, Composite)  
**Time:** 10 min read

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    REVIT UI (Ribbon/Menu)                   │
└───────────────────────────┬─────────────────────────────────┘
                            │ "Execute Script"
                            ↓
┌─────────────────────────────────────────────────────────────┐
│      ExecutionOrchestrator (Main Controller)                │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ • Discover scripts (via watchers/manual selection)  │   │
│  │ • Load providers based on script type               │   │
│  │ • Validate execution context                        │   │
│  │ • Route to strategy                                 │   │
│  └─────────────────────────────────────────────────────┘   │
└───────────────────┬────────────────────┬────────────────────┘
                    │                    │
        ┌───────────┴──────┐      ┌──────┴──────────────┐
        │                  │      │                     │
        ↓                  ↓      ↓                     ↓
    ┌────────────┐  ┌────────────────┐  ┌─────────────────────┐
    │ Provider   │  │   Provider     │  │  FileWatcher        │
    │ (Python)   │  │ (.NET)         │  │  Service            │
    │            │  │                │  │                     │
    │ Discovery  │  │ Discovery      │  │ • Monitors folders  │
    │ Execution  │  │ Execution      │  │ • Notifies changes  │
    │ Strategy   │  │ Strategy       │  │ • Updates provider  │
    └──────┬─────┘  └────────────────┘  │   registry          │
           │                            └─────────────────────┘
           │
    PEP723 Parsing
           ↓
    ┌─────────────────────┐
    │ DependencyResolver  │
    │ (UV-based)          │
    │ • Parse metadata    │
    │ • Dry-run check     │
    │ • Install packages  │
    └─────────────────────┘
           │
           ↓
    ┌──────────────────────────┐
    │ Execution Strategy       │
    │ (Python/DotNet)          │
    │ • Setup environment      │
    │ • Execute code           │
    │ • Handle output          │
    │ • Cleanup resources      │
    └──────────────────────────┘
           │
           ↓
    ┌──────────────────────┐
    │ Revit API Access     │
    │ (through __revit__)  │
    └──────────────────────┘
```

---

## Core Interfaces

### IExecutionProvider

```csharp
public interface IExecutionProvider
{
    /// Unique identifier for this provider
    string ProviderId { get; }
    
    /// Human-readable name
    string DisplayName { get; }
    
    /// Priority (higher = checked first)
    int Priority { get; }
    
    /// Can this provider handle this file?
    bool CanHandle(string filePath);
    
    /// Get the execution strategy for this file
    IExecutionStrategy GetStrategy(string filePath);
}
```

**Purpose:** Identify which execution environment to use (Python vs .NET vs other)

**Implementations:**
- `PythonExecutionProvider` - Handles `*script.py` files
- `DotNetExecutionProvider` - Handles `*script.cs` files
- Custom providers can be added via IoC container

**Discovery Process:**
```csharp
// In ExecutionOrchestrator
foreach (var provider in _providers.OrderByDescending(p => p.Priority))
{
    if (provider.CanHandle(filePath))
    {
        return provider;  // Found it!
    }
}
```

---

### IExecutionStrategy

```csharp
public interface IExecutionStrategy
{
    /// Validate before execution (check dependencies, etc)
    Task<ValidationResult> ValidateAsync();
    
    /// Execute the file
    Task<ExecutionResult> ExecuteAsync();
    
    /// Get version info (runtime, packages, etc)
    Task<VersionInfo> GetVersionInfoAsync();
}
```

**Purpose:** Define how to execute code once we know the provider

**Implementations:**
- `PythonExecutionStrategy` - Run Python with PEP 723 dep resolution
- `DotNetExecutionStrategy` - Compile and run C# script
- Custom strategies for other runtimes

**Execution Flow:**
```csharp
var strategy = provider.GetStrategy(filePath);
var validation = await strategy.ValidateAsync();  // Check dependencies
if (validation.Success)
{
    var result = await strategy.ExecuteAsync();   // Execute code
}
else
{
    // Show dependency installation dialog
}
```

---

## Component Responsibilities

### ExecutionOrchestrator

**Purpose:** Main controller - routes execution requests

**Responsibilities:**
1. Maintain provider registry
2. Select appropriate provider for file
3. Invoke strategy
4. Handle errors
5. Report status/progress

**Example:**
```csharp
public async Task<ExecutionResult> ExecuteAsync(string filePath)
{
    // 1. Find provider
    var provider = _providers
        .OrderByDescending(p => p.Priority)
        .FirstOrDefault(p => p.CanHandle(filePath));
    
    if (provider == null)
        return ExecutionResult.NotSupported($"No provider for {filePath}");
    
    // 2. Get strategy
    var strategy = provider.GetStrategy(filePath);
    
    // 3. Validate
    var validation = await strategy.ValidateAsync();
    if (!validation.Success)
    {
        // Show installation dialog if needed
        await _dependencyManager.InstallAsync(validation.MissingPackages);
    }
    
    // 4. Execute
    return await strategy.ExecuteAsync();
}
```

### Provider (e.g., PythonExecutionProvider)

**Purpose:** Detect Python scripts and create execution strategy

**Responsibilities:**
1. Check if file matches pattern (e.g., `*.py`)
2. Check for PEP 723 metadata (if required)
3. Create and configure strategy
4. Report capabilities (version, available packages, etc)

**Example:**
```csharp
public class PythonExecutionProvider : IExecutionProvider
{
    public string ProviderId => "python";
    public string DisplayName => "Python 3.x (CPython)";
    public int Priority => 100;  // High priority
    
    public bool CanHandle(string filePath)
    {
        return filePath.EndsWith(".py");
    }
    
    public IExecutionStrategy GetStrategy(string filePath)
    {
        return new PythonExecutionStrategy(
            filePath,
            _pythonExecutor,
            _dependencyManager
        );
    }
}
```

### Strategy (e.g., PythonExecutionStrategy)

**Purpose:** Execute Python code with dependency management

**Responsibilities:**
1. Parse PEP 723 metadata
2. Check if packages installed
3. Provide UI for installation (if needed)
4. Setup Python environment
5. Execute script
6. Handle output/errors
7. Cleanup

**Example:**
```csharp
public class PythonExecutionStrategy : IExecutionStrategy
{
    private readonly string _filePath;
    private readonly PythonExecutor _executor;
    private readonly PythonDependencyManager _depManager;
    
    public async Task<ValidationResult> ValidateAsync()
    {
        // 1. Parse PEP 723
        var dependencies = await Pep723Parser.ParseAsync(_filePath);
        
        // 2. Check if installed
        var missing = await _depManager.GetMissingAsync(dependencies);
        
        if (missing.Any())
            return ValidationResult.RequiresInstallation(missing);
        
        return ValidationResult.Valid();
    }
    
    public async Task<ExecutionResult> ExecuteAsync()
    {
        // 1. Setup
        await _executor.InitializeAsync();
        
        // 2. Run
        var result = await _executor.RunScriptAsync(_filePath);
        
        // 3. Cleanup
        await _executor.ResetAsync();
        
        return result;
    }
}
```

---

## FileWatcher Service

**Purpose:** Monitor script folders for changes

**Features:**
- Watches designated folders (configurable)
- Detects new/modified scripts
- Updates provider registry in real-time
- Fires events: `ScriptDiscovered`, `ScriptModified`, `ScriptDeleted`

**Example:**
```csharp
_fileWatcher.ScriptDiscovered += (path) =>
{
    Trace.TraceInformation($"Found script: {path}");
    // Add to UI list
};

_fileWatcher.ScriptModified += (path) =>
{
    Trace.TraceInformation($"Script updated: {path}");
    // Refresh UI
};
```

---

## Dependency Management

```
User clicks "Execute"
       ↓
PythonExecutionStrategy.ValidateAsync()
       ↓
Pep723Parser.ParseDependencies()       ← Extracts: ["pandas==2.0", "numpy==1.24"]
       ↓
PythonDependencyManager.GetMissingAsync()  ← Checks: Are these installed?
       ↓
If missing:
    ├─ Show PackageInstallWindow
    ├─ User confirms
    ├─ Run: uv pip install pandas==2.0 numpy==1.24
    └─ Wait for completion
       ↓
       If success: Execute script
       If failed: Show error
       ↓
If already installed:
    └─ Execute script immediately
```

---

## Execution Flow (Detailed)

### Phase 1: Discovery
```
FileWatcher detects: C:\scripts\wall_analyzer.py

Events:
1. FileSystemWatcher.OnChanged(wall_analyzer.py)
2. FileWatcherService.ProcessChange()
3. For each provider: CanHandle("wall_analyzer.py")?
4. PythonExecutionProvider.CanHandle() → YES
5. Add to registry with PythonExecutionProvider
6. Fire: ScriptDiscovered event
```

### Phase 2: Selection
```
User clicks "wall_analyzer.py" in UI

Events:
1. ExecutionOrchestrator.ExecuteAsync("wall_analyzer.py")
2. Find provider: PythonExecutionProvider
3. Create strategy: PythonExecutionStrategy
4. Call strategy.ValidateAsync()
```

### Phase 3: Validation
```
PythonExecutionStrategy.ValidateAsync():

1. Parse file header (PEP 723 format)
   - Find: # /// script ... # ///
   - Extract: dependencies = ["pandas==2.0", "requests>=2.25"]

2. Check file exists and is readable
   - Verify: C:\scripts\wall_analyzer.py (OK)

3. Check if Python is initialized
   - If not: Download Python 3.x (first run only)

4. Check if packages installed
   - Run: uv pip show pandas
   - Result: missing ["pandas==2.0"]

5. Return: ValidationResult
   - Status: RequiresInstallation
   - MissingPackages: ["pandas==2.0"]
```

### Phase 4: Installation (if needed)
```
Show UI: PackageInstallWindow
- Display: "Installing pandas==2.0"
- Show progress bar
- User can cancel

If approved:
1. Run: uv pip install pandas==2.0
2. Wait for completion (30-45 seconds on first run)
3. Verify installation
4. If success: Proceed to execution
5. If failed: Show error, suggest troubleshooting
```

### Phase 5: Execution
```
PythonExecutionStrategy.ExecuteAsync():

1. Initialize Python (Python.NET)
   - Load cpython3x.dll
   - Create engine
   - Initialize scope with Revit refs

2. Inject global variables
   - __revit__: Revit API handle
   - __file__: Full path to script
   - __root__: Root document object

3. Run Setup.py (internal)
   - Redirect output to Trace/console
   - Import common modules

4. Execute user script
   - Load and run: wall_analyzer.py code
   - All Revit API calls routed through __revit__

5. Run Reset.py (internal)
   - Clear module cache (avoid stale imports)
   - Free memory

6. Return result
   - Status: Success/Error
   - Output: All Trace messages
   - ExceptionDetails: If failed
```

---

## Thread Safety & Concurrency

### Python.NET Requirements

```csharp
// Python.NET and C# thread coordination matters
// GIL (Global Interpreter Lock) managed by python/thread pool

public async Task<ExecutionResult> ExecuteAsync()
{
    // Python code must run in Python context
    using (Py.GIL())  // Acquire Global Interpreter Lock
    {
        // Now safe to call Python code
        scope.Execute("import pandas");
        scope.Execute("df = pandas.read_csv('data.csv')");
    }
    // GIL released here
}
```

**Key Points:**
- Only one thread can execute Python code at a time (GIL)
- Revit API calls are single-threaded (UI thread only)
- Execution strategies handle thread coordination

### Execution Quarantine

```csharp
// Each execution runs in isolated scope
public async Task<ExecutionResult> ExecuteAsync()
{
    // Create new scope (isolated from previous runs)
    var scope = _engine.CreateScope();
    
    // Run code in this scope only
    scope.Execute(scriptCode);
    
    // Scope is disposed after execution
    // Previous module state cleared
    // No cross-script pollution
}
```

---

## Extensibility Points

### 1. Add Custom Provider

```csharp
// 1. Implement IExecutionProvider
public class CustomLangProvider : IExecutionProvider
{
    public string ProviderId => "mylang";
    public int Priority => 50;
    
    public bool CanHandle(string filePath) 
        => filePath.EndsWith(".mycustom");
    
    public IExecutionStrategy GetStrategy(string filePath)
        => new CustomLangStrategy(filePath);
}

// 2. Register in IoC container
services.AddSingleton<IExecutionProvider>(
    new CustomLangProvider()
);

// Now:
// - ExecutionOrchestrator finds your provider
// - FileWatcher detects *.mycustom files
// - Your strategy handles execution
```

### 2. Add Custom Dependency Resolver

```csharp
// Hook into dependency resolution
public class CustomDependencyResolver : IDependencyResolver
{
    public async Task<IEnumerable<string>> GetMissingAsync(
        IEnumerable<PackageSpec> packages)
    {
        // Custom logic:
        // - Check custom package registry
        // - Validate version constraints
        // - Return missing packages
    }
}
```

### 3. Custom Tree Node Types

```csharp
// Extend BaseNode for custom logic
public class CustomDataNode : BaseNode
{
    public override object Execute(ExecutionContext ctx)
    {
        // Custom execution logic
        return myCustomCalculation();
    }
    
    public override void Reset()
    {
        // Custom cleanup
    }
}
```

---

## Error Handling Strategy

### Provider Level
```csharp
// Provider.CanHandle() exceptions are swallowed
// Execution continues to next provider
try
{
    if (provider.CanHandle(filePath)) { ... }
}
catch (Exception ex)
{
    Trace.TraceError($"Provider error: {ex}");
    // Continue to next provider
}
```

### Strategy Level
```csharp
// Strategy errors bubble up and show to user
try
{
    var result = await strategy.ExecuteAsync();
    if (!result.Success)
    {
        // Show: "Script error: {result.ErrorMessage}"
        return ShowErrorDialog(result.ExceptionDetails);
    }
}
catch (Exception ex)
{
    // Unhandled exception
    return ShowCrashDialog(ex);
}
```

---

## Performance Considerations

| Operation | Duration | Notes |
|-----------|----------|-------|
| Script discovery (100 files) | ~50ms | FileWatcher async |
| Python init (first run) | 3-5 sec | Download + JIT warmup |
| Python init (cached) | ~500ms | Load from disk cache |
| PEP 723 parsing | <5ms | Regex on small string |
| Dependency dry-run check | 100-500ms | UV subprocess call |
| Package installation | 30-45 sec | Network I/O + build |
| Script execution (typical) | 1-10 sec | Depends on Revit API usage |

**Optimization tips:**
- Python initialization is cached (second run reuses process)
- Dependency resolution is parallelizable
- FileWatcher runs on background thread
- Execution strategy can be async throughout

---

## State Persistence

### Tree Model State

```csharp
// BaseNode tree can be serialized
public interface ITreeStateManager
{
    Task SaveAsync(RootNode tree, string filePath);
    Task<RootNode> LoadAsync(string filePath);
}

// Use case: Save execution graph to JSON
// - Persist node state between sessions
// - Allow undo/redo
// - Enable version control of execution graphs
```

---

## Architecture Benefits

✅ **Extensible** - Add new providers/strategies without modifying core  
✅ **Testable** - Each interface can be mocked  
✅ **Thread-Safe** - GIL management built-in  
✅ **Performant** - Lazy loading, caching, async throughout  
✅ **User-Friendly** - Progress UI, error details, helpful messages  
✅ **Maintainable** - Clear separation of concerns

---

**Related Docs:**
- See **[11: Provider Pattern](11-provider-pattern.md)** for detailed provider implementation
- See **[12: Strategy Pattern](12-strategy-pattern.md)** for strategy details
- See **[13: Tree Model](13-tree-model.md)** for graph persistence
- See **[40: Python Runtime](40-python-runtime.md)** for Python.NET details

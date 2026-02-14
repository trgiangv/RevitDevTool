# Strategy Pattern - Execution Strategies

## Overview

The Strategy pattern enables different execution approaches for different language runtimes (Python, C#, JavaScript) while maintaining a consistent interface. Each strategy handles validation, execution, and result reporting.

**Prerequisites:** Read [10-architecture-overview.md](10-architecture-overview.md) and [11-provider-pattern.md](11-provider-pattern.md)

## IExecutionStrategy Interface

**Source:** `IExecutionStrategy.cs` in `RevitDevTool.CodeExecute` namespace

```csharp
public interface IExecutionStrategy
{
    Task<ValidationResult> ValidateAsync();
    Task<ExecutionResult> ExecuteAsync();
    Task<VersionInfo> GetVersionInfoAsync();
}
```

**Key responsibilities:**
- **ValidateAsync**: Check environment, dependencies, permissions before execution
- **ExecuteAsync**: Run the code file and return structured result
- **GetVersionInfoAsync**: Report runtime version (Python 3.12, .NET 10, Node 20, etc.)

## Validation Flow

### ValidationResult

Represents validation outcome with recovery guidance:

```csharp
public class ValidationResult
{
    public bool Success { get; set; }
    public bool IsRecoverable { get; set; }
    public string Message { get; set; }
    public IEnumerable<string> MissingDependencies { get; set; }
}
```

**States:**
- **Valid**: Ready to execute
- **RequiresInstallation**: Missing dependencies but can be installed (recoverable)
- **Error**: Critical failure like missing runtime (not recoverable)

### Validation Pipeline

1. **File Access** - Verify file exists and is readable
2. **Runtime Availability** - Check runtime installed and compatible version
3. **Dependencies** - Parse metadata (PEP 723, package.json) and check installed packages
4. **Context** - Validate resources (memory, temp directory, permissions)

Result determines next action:
- **Success** → Proceed to execution
- **RequiresInstallation** → Show install dialog with missing packages
- **Error** → Display error message, block execution

## ExecutionResult

Captures execution outcome and artifacts:

```csharp
public class ExecutionResult
{
    public bool Success { get; set; }
    public string Output { get; set; }           // Stdout/logs
    public string Error { get; set; }            // Stderr or exceptions
    public TimeSpan Duration { get; set; }       // Execution time
    public int? ExitCode { get; set; }          // Process exit code
    public Dictionary<string, object> Metadata { get; set; }  // Runtime info
}
```

## Strategy Lifecycle

### Single Execution

```
Create Strategy Instance
    ↓
ValidateAsync()
    ↓
[If Success=true] → ExecuteAsync()
    ↓
Return ExecutionResult
    ↓
Dispose Strategy
```

### Multiple Executions

Strategy instances can be reused if runtime state persists (e.g., in-process Python engine):

```
Create Strategy
    ↓
ValidateAsync() [once]
    ↓
ExecuteAsync() [file 1]
    ↓
ExecuteAsync() [file 2]
    ↓
ExecuteAsync() [file 3]
    ↓
Dispose Strategy
```

## Strategy Implementations

### PythonExecutionStrategy

**Source:** `PythonExecutionStrategy.cs` in `RevitDevTool.CodeExecute.Python` namespace

- **Validation**: Check Python.NET runtime, parse PEP 723 dependencies
- **Execution**: Execute via pyRevit's Python engine (in-process)
- **Dependencies**: Install via `uv pip install` if needed
- **Result**: Capture output from Python scope

### DotNetExecutionStrategy

**Source:** `DotNetExecutionStrategy.cs` in `RevitDevTool.CodeExecute.DotNet` namespace

- **Validation**: Check .NET SDK, parse project file for NuGet packages
- **Execution**: Run via `dotnet run` (subprocess)
- **Dependencies**: Restore via `dotnet restore`
- **Result**: Capture stdout/stderr from process

### (Future) NodeExecutionStrategy

Placeholder for JavaScript/TypeScript execution:

- **Validation**: Check Node.js installed, parse package.json
- **Execution**: Run via `node script.js` (subprocess)
- **Dependencies**: Install via `npm install`
- **Result**: Capture console output

## Strategy Patterns

### Subprocess-Based Strategy

**Pattern:** Launch external process, capture output, parse exit code

**Advantages:**
- Isolated environment (crashes don't affect Revit)
- Clean dependency separation
- Easy to manage versions (different Python installs)

**Disadvantages:**
- IPC overhead for data passing
- No access to Revit API context
- Slower startup

**Used by:** DotNetExecutionStrategy, (future) NodeExecutionStrategy

### In-Process Strategy

**Pattern:** Execute code in same process, share memory/context

**Advantages:**
- Direct access to Revit API
- Fast execution (no process spawn)
- Can share state between executions

**Disadvantages:**
- Crashes can affect Revit
- Harder to isolate dependencies
- Runtime version locked to host

**Used by:** PythonExecutionStrategy (via Python.NET in pyRevit)

## Integration with Provider Pattern

**LanguageProviderFactory** selects strategy via file extension:

```
File Extension → LanguageProvider → IExecutionStrategy
    .py       →  PythonProvider   → PythonExecutionStrategy
    .cs/.csx  →  CSharpProvider   → DotNetExecutionStrategy
    .js/.ts   →  (future)         → NodeExecutionStrategy
```

See [11-provider-pattern.md](11-provider-pattern.md) for provider details.

## Error Handling

Strategies should catch and wrap exceptions in `ExecutionResult`:

```csharp
public async Task<ExecutionResult> ExecuteAsync()
{
    try
    {
        // Execute code
        return ExecutionResult.Success(output);
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "Execution failed");
        return ExecutionResult.Failure(ex.Message);
    }
}
```

## Best Practices

### For Strategy Implementers

1. **Validate thoroughly** before execution to catch issues early
2. **Provide actionable feedback** in validation messages
3. **Mark dependencies as recoverable** when auto-install is possible
4. **Capture all output** (stdout, stderr, logs) for debugging
5. **Set timeouts** for long-running executions

### For Strategy Users

1. **Always validate** before attempting execution
2. **Handle ValidationResult states** appropriately (offer install, show error)
3. **Display ExecutionResult.Error** to user when execution fails
4. **Log execution time** for performance monitoring
5. **Dispose strategies** to free resources

## Configuration

**Source:** `Settings/ExecutionSettings.cs`

- `DefaultTimeout`: Maximum execution duration
- `AutoInstallDependencies`: Enable/disable auto-install prompt
- `CaptureOutput`: Enable/disable stdout/stderr capture
- `EnableLogging`: Send execution logs to logging system

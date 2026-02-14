# Python Execution Implementation

## Overview

The Python execution system handles Python.NET-specific challenges when executing Python code in Revit. This document explains the architecture decisions for interface implementation, transaction management, and type conversion.

## Technical Context

**Python.NET vs IronPython:**
- **IronPython**: Native .NET implementation, can implement .NET interfaces directly
- **Python.NET**: CPython with .NET bridge, uses dynamic binding (duck typing), cannot directly implement .NET interfaces

RevitDevTool uses Python.NET (via pyRevit) to access modern Python ecosystem (pip, numpy, pandas) while working around its limitations.

## Key Challenges & Solutions

### 1. Interface Implementation

**Challenge:** Python.NET cannot directly implement .NET interfaces like `IExternalCommand` or `IExternalEventHandler` because Python uses duck typing while .NET requires explicit type contracts at compile time.

**Solution in RevitDevTool:**

Use C# wrapper classes that implement required interfaces and delegate to Python code:

```csharp
// C# wrapper implements interface
public class PythonWrapper : IExternalCommand
{
    public Result Execute(...)
    {
        var scope = ExecutePythonScript("script.py");
        return (Result)scope.GetVariable("result");
    }
}
```

**Architecture Pattern:**
1. **C# Wrapper** implements .NET interface
2. **Python Script** executes inside wrapper scope
3. **Result** returned via scope variables or return value
4. **.NET** sees only the C# wrapper, not Python code

### 2. Transaction Management

**Challenge:** Python.NET doesn't auto-wrap `using` statements for IDisposable objects. Transactions must be explicitly managed to avoid Revit API violations.

**Solution in RevitDevTool:**

**Option A: Wrapper Auto-Transaction**

C# wrapper automatically creates and manages transaction:

```csharp
public class PythonCommandWrapper : IExternalCommand
{
    public Result Execute(...)
    {
        using var transaction = new Transaction(doc, "Python Command");
        transaction.Start();
        
        ExecutePythonScript(...);
        
        transaction.Commit();
        return Result.Succeeded;
    }
}
```

**Option B: Python Manual Transaction**

Python code explicitly manages transaction via try/finally:

```python
t = Transaction(doc, "My Operation")
try:
    t.Start()
    # ... modifications ...
    t.Commit()
finally:
    if t.HasStarted() and not t.HasEnded():
        t.RollBack()
```

**Architecture Decision:**
- **Auto-transaction**: For simple scripts without branching logic
- **Manual transaction**: For complex scripts requiring conditional commits/rollbacks

### 3. Type Conversion

**Challenge:** Python types (int, float, str, list) don't automatically convert to .NET types (Int32, Double, String, IList<T>). Mismatches cause runtime errors.

**Common Conversion Issues:**

| Python Type | .NET Type Expected | Solution |
|------------|-------------------|----------|
| `float` | `Double` or `Decimal` | Explicit cast: `Double(value)` |
| `list` | `IList<T>` | Convert: `List[ElementId](python_list)` |
| `str` | `String` | Usually auto-converts |
| `int` | `Int32` or `Int64` | May need explicit: `Int32(value)` |
| `None` | `null` reference | Check before passing |

**Solution in RevitDevTool:**

Provide helper methods for common conversions:

```python
# In Python helper module
def to_element_id_list(python_list):
    return List[ElementId]([ElementId(x) for x in python_list])

def to_xyz(point_tuple):
    return XYZ(float(point_tuple[0]), float(point_tuple[1]), float(point_tuple[2]))
```

### 4. Exception Handling

**Challenge:** Python exceptions don't propagate cleanly through .NET/Python.NET boundary. Stack traces can be lost.

**Solution in RevitDevTool:**

Capture Python exceptions in C# wrapper and log with full traceback:

```csharp
try
{
    ExecutePythonScript(...);
}
catch (PythonException ex)
{
    _logger.Error(ex, "Python script failed");
    // Extract and log Python traceback
    var traceback = ex.Data["traceback"];
    message = $"{ex.Message}\n{traceback}";
    return Result.Failed;
}
```

## Implementation in CodeExecute Module

### PythonCodeExecutor

**Source:** `PythonCodeExecutor.cs` in `RevitDevTool.CodeExecute` namespace

Handles Python script execution with:
- Scope creation and management
- Transaction wrapper (if enabled in settings)
- Exception capture and logging
- Type conversion helpers
- Python.NET engine initialization

### PythonRuntime Configuration

**Source:** `Settings/PythonRuntimeSettings.cs`

Configuration options:
- `AutoTransaction`: Enable/disable automatic transaction wrapping
- `TransactionName`: Default transaction name for auto-wrapped scripts
- `PythonEngine`: Engine selector (Python.NET engine via pyRevit)

## Best Practices

### For Script Authors

1. **Use explicit type conversions** when passing data to Revit API
2. **Manage transactions manually** for complex scripts with branching
3. **Check for None/null** before passing to .NET methods
4. **Use try/finally** for transaction safety
5. **Import helper utilities** for common conversions

### For RevitDevTool Developers

1. **Wrap interfaces in C#** - never expect Python to implement .NET interfaces
2. **Capture Python exceptions** with full traceback for debugging
3. **Provide conversion helpers** for common type mismatches
4. **Document transaction requirements** for scripts
5. **Test with both auto and manual transaction modes**

## Integration Points

- **CodeExecute Module**: Orchestrates Python script execution
- **Logging System**: Captures Python exceptions with tracebacks (see [05-Python-Integration.md](../Logging/architecture/05-Python-Integration.md))
- **Settings Service**: Configuration for transaction mode and engine options

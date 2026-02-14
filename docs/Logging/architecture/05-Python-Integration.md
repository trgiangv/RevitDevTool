# Python Logging Integration

## Overview

The Python logging integration enables Python scripts running in Revit (via pyRevit/IronPython) to send log messages to the RevitDevTool logging system with automatic stack trace capture.

## Architecture Components

### C# Bridge - PyTrace

**Source:** `PyTrace.cs` in `RevitDevTool.Logging.Python` namespace

Static class that accepts Python log messages and formats them with Python stack traces before forwarding to the .NET `System.Diagnostics.Trace` system.

**Key responsibilities:**
- Initialize with settings service (one-time setup)
- Receive Python messages with traceback strings
- Filter traceback to show only relevant file lines
- Respect `IncludeStackTrace` and `StackTraceDepth` settings
- Forward formatted messages to .NET tracing infrastructure

### Python Helper - trace.py

**Location:** Distributed with PythonDemo scripts

Python module that provides convenient tracing functions for pyRevit scripts.

**Key responsibilities:**
- Detect if RevitDevTool assembly is available
- If yes: Use `PyTrace.Write()` with full traceback capture
- If no: Fallback to `System.Diagnostics.Trace.Write()`
- Export helper functions: `trace()`, `trace_var()`, `trace_collection()`, `trace_error()`

## Integration Flow

1. **Python script** calls `trace("message")`
2. **trace.py** captures current stack using `traceback.extract_stack()`
3. **trace.py** formats traceback as string
4. **PyTrace.Write()** receives message + traceback
5. **PyTrace** filters traceback based on settings (depth, include/exclude)
6. **System.Diagnostics.Trace** receives final formatted message
7. **TraceListeners** process and route the message

## Usage Pattern

```python
from trace import trace, trace_var, trace_error

def process_walls():
    trace("Starting wall processing")
    walls = collect_walls()
    trace_var("Walls found", len(walls))
    
    try:
        for wall in walls:
            analyze_wall(wall)
    except Exception as e:
        trace_error(str(e))
```

## Stack Trace Format

Python tracebacks are filtered to show only file location lines:

```
Message: Processing element failed
Traceback (Last call first):
  File "C:\Scripts\process_walls.py", line 42, in process_wall
  File "C:\Scripts\process_walls.py", line 18, in main
  File "<string>", line 1, in <module>
```

The depth is controlled by `LogConfig.StackTraceDepth` setting (default: 5 frames).

## Configuration

```csharp
// During application startup
PyTrace.Initialize(settingsService);

// Configure stack trace behavior
var config = settingsService.LogConfig;
config.IncludeStackTrace = true;  // Enable Python tracebacks
config.StackTraceDepth = 5;       // Show up to 5 frames
```

## Troubleshooting

### Stack Trace Not Appearing

Verify `IncludeStackTrace` setting is enabled in configuration.

### RevitDevTool Assembly Not Found

When `trace.py` cannot load RevitDevTool assembly, it automatically falls back to `System.Diagnostics.Trace`. Messages are still logged, but without enhanced traceback filtering.

### Interactive Scripts Show `<string>`

Scripts executed in pyRevit interactive shell show `<string>` as the filename. This is expected behavior for code not saved to `.py` files.

# Log Level

RevitDevTool uses standard `System.Diagnostics.Trace` output and assigns each entry a log level. The selected minimum level controls which messages are shown or written to the configured outputs.

## Available levels

| Level | Use it for |
| --- | --- |
| `Trace` | Very detailed diagnostic output |
| `Debug` | Development and troubleshooting details |
| `Information` | Normal progress and completed actions |
| `Warning` | Recoverable problems or unexpected conditions |
| `Error` | An operation failed but the host can continue |
| `Critical` | A severe failure requiring immediate attention |

## Configure the minimum level

Choose the minimum level in the Trace Log settings. Messages below that level are filtered from the logging pipeline. For normal use, `Information` is a good default; use `Debug` or `Trace` while investigating a problem.

## Write log messages

Use the standard .NET trace APIs from C#, F#, Python.NET, IronPython, or a compatible add-in:

```csharp
using System.Diagnostics;

Trace.TraceInformation("Export started");
Trace.TraceWarning("Room has no boundary");
Trace.TraceError("Export failed: {0}", exception.Message);
Debug.WriteLine("Detailed diagnostic value");
```

Python scripts can write through the same trace pipeline:

```python
from System.Diagnostics import Trace

Trace.TraceInformation("Analysis started")
Trace.TraceWarning("Element was skipped")
Trace.TraceError("Analysis failed")
```

RevitDevTool also redirects supported `Console.WriteLine` and Python `print()` output into the trace pipeline. Use explicit trace methods when the severity matters.

## What happens next

The level is applied before the message is delivered to the configured [Log Output](/docs/logging/Observability-Http) targets. Revit geometry objects written through the standard trace listener pipeline are handled by [Geometry Visualization](/docs/logging/Visualization-Overview).

## Related

- [Log Output](/docs/logging/Observability-Http) — Monitor, File, and HTTP destinations
- [Geometry Visualization](/docs/logging/Visualization-Overview) — transient geometry in Revit

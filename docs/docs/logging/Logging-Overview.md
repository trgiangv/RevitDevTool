# Log Level

RevitDevTool uses standard `System.Diagnostics.Trace` output and assigns each entry a log level. The selected minimum level controls which messages are shown or written to the configured outputs.

![Log entries streaming into the RevitDevTool panel](/images/features/Feature_Logging.gif)

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

Choose the minimum level in **Settings → Log**. Messages below that level are filtered from the logging pipeline. For normal use, `Information` is a good default; use `Debug` or `Trace` while investigating a problem.

![Log settings: minimum level, stack trace, sinks and file logging](/images/settings/Settings_Logging.png)

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

:::info IronPython on .NET 8 (Revit 2025 and later)
Revit 2025 and later run on .NET 8, where `Trace` and `Console` live in assemblies that IronPython does not load by default, such as the one pyRevit uses. Add the references before importing them:

```python title="ironpython_log_script.py"
import clr
clr.AddReference("System.Diagnostics.TraceSource")
clr.AddReference("System.Console")

from System.Diagnostics import Debug, Trace
from System import Console

Trace.TraceInformation("Python script executed")
Trace.WriteLine("[WARN] Element not found")
Debug.WriteLine("Detailed diagnostic value")
Console.WriteLine("[INFO] Done")
```

- `System.Diagnostics.TraceSource` provides `Trace`.
- `System.Console` provides `Console`.
- `Debug` needs no reference on .NET 8.
- On Revit 2024 and earlier (.NET Framework 4.8) the references are not needed.
:::

:::tip Debug or Trace in compiled add-ins
`Debug.WriteLine` is compiled only into Debug builds (the `DEBUG` constant), while `Trace` methods need the `TRACE` constant. Use `Trace` for messages that must also appear in a Release build.
:::

## How the level is chosen

Messages written with `Trace.WriteLine`, `Console.WriteLine` or `print()` carry no level of their own, so RevitDevTool reads it from the text:

1. **Prefix:** a bracketed tag at the start of the message is checked first.
2. **Keywords:** otherwise the message is scanned for level keywords, in the order Critical, Error, Warning, Information. The first match wins.
3. **No match:** the message is logged as `Debug`.

| Level | Prefixes | Default keywords |
| --- | --- | --- |
| Critical | `[FATAL]` `[CRITICAL]` | `fatal`, `critical`, `crash` |
| Error | `[ERROR]` `[ERR]` | `error`, `failed`, `exception` |
| Warning | `[WARNING]` `[WARN]` | `warning`, `warn`, `caution` |
| Information | `[INFO]` `[INFORMATION]` | `info`, `success`, `completed` |
| Debug | `[DEBUG]` `[DBG]` | none; the fallback |

Short forms such as `[FTL]`, `[WRN]` and `[INF]` also work, and `[TRACE]` or `[VERBOSE]` select the `Trace` level. Matching ignores case.

```csharp
Trace.WriteLine("[WARN] Configuration not found, using defaults");   // Warning
Trace.WriteLine("Export completed");                                  // Information
Trace.WriteLine("Failed to process element 123456");                  // Error
Trace.WriteLine("Processing elements...");                            // Debug, no keyword matched
```

:::tip Change the keywords
Edit them in **Settings → Log** under **Level Detection Keywords**: comma-separated, up to 5 per level.
:::

:::warning Ambiguous messages
A message that contains keywords for two levels takes the higher one, so `Error handling completed` is logged as Error. Add a prefix such as `[INFO]` when you want to be explicit.
:::

### Example: log an element's properties

Pick an element and log each of its properties at the right level. Properties that throw are logged as warnings instead of stopping the command:

```csharp title="ElementInfoCommand.cs"
using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

[Transaction(TransactionMode.Manual)]
public class ElementInfoCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uidoc = commandData.Application.ActiveUIDocument;
        var reference = uidoc.Selection.PickObject(ObjectType.Element, "Select an element");
        var element = uidoc.Document.GetElement(reference);

        foreach (var property in element.GetType().GetProperties())
        {
            try
            {
                Trace.TraceInformation($"{property.Name}: {property.GetValue(element)}");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"{property.Name} could not be read: {ex.Message}");
            }
        }

        return Result.Succeeded;
    }
}
```

## What happens next

The level is applied before the message is delivered to the configured [Log Output](/docs/logging/Observability-Http) targets. Revit geometry objects written through the standard trace listener pipeline are handled by [Geometry Visualization](/docs/logging/Visualization-Overview).

## Related

- [Log Output](/docs/logging/Observability-Http) — Monitor, File, and HTTP destinations
- [Geometry Visualization](/docs/logging/Visualization-Overview) — transient geometry in Revit

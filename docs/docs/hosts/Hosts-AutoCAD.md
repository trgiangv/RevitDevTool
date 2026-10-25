# AutoCAD

RevitDevTool supports AutoCAD-family hosts through the same execution, testing, MCP, and logging features used across the product. Use the host-specific Autodesk API for the document, database, editor, and transaction work.

<LogoRow items={['autocad', 'civil3d', 'plant3d']} />

To test against a specific AutoCAD-based product, set the host name from the table in [Host settings](/docs/testing/Testing-Overview#host-settings).

## What you can do in AutoCAD-family hosts

- **Run scripts and add-ins**
  - [Python](/docs/execution/python/Execution-Python) scripts, with dependencies from [Python Dependencies](/docs/execution/Python-Dependencies)
  - [C#](/docs/execution/csharp-script/Execution-CSharp) `.csx`, [F#](/docs/execution/fsharp-script/Execution-FSharp) `.fsx` and [IronPython](/docs/execution/ironpython/Execution-IronPythonStandalone) scripts
  - Compiled [.NET assemblies](/docs/execution/dotnet-assembly/Execution-Assembly)
- **Debug**
  - [Attach a debugger](/docs/execution/python/Execution-PythonDebugging) to scripts and assemblies
  - Read logs with [Log Level](/docs/logging/Logging-Overview) and [Log Output](/docs/logging/Observability-Http)
- **Test inside a live host**
  - [NUnit](/docs/testing/NUnit), [TUnit](/docs/testing/TUnit) or [MSTest](/docs/testing/MSTest)
  - [pytest](/docs/testing/pytest) checks that need AutoCAD context
- **Connect AI clients**
  - Expose tools through [MCP .NET](/docs/mcp/MCP-CSharp) or [MCP Python](/docs/mcp/MCP-Python)

:::info No geometry visualization
[Geometry Visualization](/docs/logging/Visualization-Overview) works on Revit only.
:::

## Supported versions

| Version | Target framework |
| --- | --- |
| 2022-2024 | `net48` |
| 2025-2026 | `net8.0-windows` |
| 2027 | `net10.0-windows` |

## API starting point

```python
from Autodesk.AutoCAD.ApplicationServices.Core import Application

doc = Application.DocumentManager.MdiActiveDocument
db = doc.Database
editor = doc.Editor
editor.WriteMessage("RevitDevTool is connected to AutoCAD")
```

For database changes, use the normal AutoCAD transaction pattern. Scripts, MCP code and tests already run in the active drawing's command context, so the current drawing does not need `LockDocument()`. Use `LockDocument()` when changing a different drawing that is already open. Assigning the active document takes effect after the script returns.

```python
from Autodesk.AutoCAD.DatabaseServices import OpenMode

tr = db.TransactionManager.StartTransaction()
try:
    block_table = tr.GetObject(db.BlockTableId, OpenMode.ForRead)
    # Work with database objects here.
    tr.Commit()
except Exception:
    tr.Abort()
    raise
finally:
    tr.Dispose()
```

## Testing

For pytest, select the AutoCAD host in the test configuration:

```toml
[tool.pytest.ini_options]
host_name = "autocad"
host_version = "2026"
```

Run the test suite with `uv run pytest -v`. .NET tests run under Microsoft Testing Platform; see the test framework pages above.
## Related

- [Execution Overview](/docs/execution/Execution-Overview)
- [Testing Overview](/docs/testing/Testing-Overview)
- [ModelContextProtocol](/docs/mcp/MCP-Protocol)

# AutoCAD

RevitDevTool supports AutoCAD-family hosts through the same execution, testing, MCP, and logging features used across the product. Use the host-specific Autodesk API for the document, database, editor, and transaction work.

## AutoCAD-family routing

All AutoCAD-family products use this page. Select the matching values when configuring pytest or Microsoft Testing Platform tests:

| Product | Product ID | pytest `host_name` | .NET test `HostName` |
| --- | ---: | --- | --- |
| AutoCAD | `01` | `autocad` | `AutoCad` |
| Civil 3D | `00` | `civil3d` | `Civil3D` |
| Map 3D | `02` | `acadmap3d` | `AcadMap3D` |
| Architecture | `04` | `acadarch` | `AcadArch` |
| Mechanical | `05` | `acadmech` | `AcadMech` |
| MEP | `06` | `acadmep` | `AcadMep` |
| Electrical | `07` | `acadelec` | `AcadElec` |
| Plant 3D | `17` | `plant3d` | `Plant3D` |

Use `autocad` / `AutoCad` for plain AutoCAD and the family-specific value when the host is a vertical product.

## Supported Versions

AutoCAD-family hosts use the same target-framework mapping as Revit:

| AutoCAD-family version | Target framework |
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
```

For database changes, use the normal AutoCAD transaction pattern:

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

## Python execution

Use the AutoCAD document and database APIs from a Python script. Third-party packages follow the normal [Python Dependencies](/docs/execution/Python-Dependencies) workflow.

```python
from Autodesk.AutoCAD.ApplicationServices.Core import Application

doc = Application.DocumentManager.MdiActiveDocument
editor = doc.Editor
editor.WriteMessage("RevitDevTool is connected to AutoCAD")
```

## Testing

For pytest, select the AutoCAD host in the test configuration:

```toml
[tool.pytest.ini_options]
host_name = "autocad"
host_version = "2026"
```

Run the test suite with `uv run pytest -v`. .NET tests use the [NUnit](/docs/testing/NUnit) or [TUnit](/docs/testing/TUnit) pages under Microsoft Testing Platform.

## MCP and logging

Register an [MCP .NET](/docs/mcp/MCP-CSharp) or [MCP Python](/docs/mcp/MCP-Python) toolset for AutoCAD actions. Use [Log Level](/docs/logging/Logging-Overview) and [Log Output](/docs/logging/Observability-Http) for diagnostics; Revit geometry visualization is not part of the AutoCAD host workflow.

## Related

- [Execution Overview](/docs/execution/Execution-Overview)
- [Testing Overview](/docs/testing/Testing-Overview)
- [ModelContextProtocol](/docs/mcp/MCP-Protocol)

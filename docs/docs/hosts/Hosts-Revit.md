# Revit

Revit is the primary RevitDevTool v3.0 host — full [four pillars](/docs/getting-started/Platform-v3.0-Overview#four-pillars).

| Pillar | Entry points |
|--------|--------------|
| **Execution** | [Execution Overview](/docs/execution/Execution-Overview), [Modern Python Scripting](/docs/execution/python/Execution-Python), [C# Scripts](/docs/execution/csharp-script/Execution-CSharp), [F# Scripts](/docs/execution/fsharp-script/Execution-FSharp), [.NET Assembly](/docs/execution/dotnet-assembly/Execution-Assembly) |
| **Testing** | [pytest](/docs/testing/pytest), [NUnit](/docs/testing/NUnit), [TUnit](/docs/testing/TUnit) |
| **AI Tool Integration** | [MCP .NET](/docs/mcp/MCP-CSharp), [MCP Python](/docs/mcp/MCP-Python) |
| **Observability** | [Log Level](/docs/logging/Logging-Overview), [Geometry Visualization](/docs/logging/Visualization-Overview) |

## Supported Versions

The current build matrix supports Autodesk/Revit configurations from 2022 through 2027:

| Revit version | Target framework |
| --- | --- |
| 2022-2024 | `net48` |
| 2025-2026 | `net8.0-windows` |
| 2027 | `net10.0-windows` |

## What You Can Do In Revit

- run Python scripts that call the Revit API;
- install Python dependencies from script metadata;
- debug Python in VSCode;
- run compiled Revit add-ins;
- run C# `.csx`, F# `.fsx`, and IronPython scripts;
- inspect logs and Python stack traces;
- visualize temporary Revit geometry;
- expose selected tools through MCP;
- run pytest-style checks that need Revit context;
- run NUnit or TUnit tests inside a live Revit session;
- search and run commands through Command Browser;
- watch memory usage when diagnosing heavy scripts.

## Good Use Cases

| Use case | Recommended page |
| --- | --- |
| Quick Python automation | [Modern Python Scripting](/docs/execution/python/Execution-Python) |
| Revit API add-in development | [.NET Assembly](/docs/execution/dotnet-assembly/Execution-Assembly) |
| Fast C# or F# experiment | [C# Scripts](/docs/execution/csharp-script/Execution-CSharp), [F# Scripts](/docs/execution/fsharp-script/Execution-FSharp) |
| Visual geometry debugging | [Geometry Visualization](/docs/logging/Visualization-Overview) |
| AI-assisted tool access | [MCP .NET](/docs/mcp/MCP-CSharp), [MCP Python](/docs/mcp/MCP-Python) |
| Tests that need Revit API context (pytest) | [pytest](/docs/testing/pytest) |
| Tests that need Revit API context (NUnit/TUnit) | [NUnit](/docs/testing/NUnit), [TUnit](/docs/testing/TUnit) |

## Python Runtime

CPython runs through Python.NET with a Pixi-owned interpreter (Python 3.14 in `%APPDATA%\RevitDevTool\pixi-env`). Revit does not embed CPython — there is **no uv sidecar** on this host.

IronPython **execution** (`*_ipy_script.py`) and IronPython **testing** (`test_*_ipy.py`) both prefer the **pyRevit engine** when pyRevit is installed. pyRevit's current default engine is still **IronPython 2.7.12**, which matches most pyRevit tool development. Without pyRevit, RevitDevTool falls back to IronPython 3.4.2. See [Modern Python Scripting](/docs/execution/python/Execution-Python), [Python Ecosystems](/docs/execution/python/Execution-PythonEcosystems), and [IronPython — pyRevit Folder Compatibility](/docs/execution/ironpython/Execution-VsPyRevit).

## Revit API Context

Revit API calls must run in a valid Revit API context. Scripts launched through RevitDevTool are designed for that. Modeless UI or background work still needs to marshal Revit API calls back through the provided Revit execution path.

## Geometry Visualization

Visualization is currently a Revit DirectContext3D feature. Geometry traced or printed by scripts can be shown temporarily in the active view without creating model elements.

Supported geometry categories include:

- XYZ/points
- curves and polylines
- bounding boxes/outlines
- meshes
- faces
- solids
- planes

See [Geometry Visualization](/docs/logging/Visualization-Overview).

## MCP Integration

Local MCP ([MCP .NET](/docs/mcp/MCP-CSharp) or [MCP Python](/docs/mcp/MCP-Python)) discovers running Revit instances (`DevToolsMcp_Revit_{Year}_{PID}`) and exposes host capabilities through `search_dynamic` and `invoke_dynamic`.

## Revit-Specific Caveats

- Revit API behavior cannot be fully validated by pure unit tests.
- Some workflows need a live Revit session.
- Geometry visualization is tied to active Revit view/rendering state.
- `net48` compatibility still matters for Revit 2022-2024.
- Python/IronPython behavior depends on installed runtimes and enterprise machine restrictions.

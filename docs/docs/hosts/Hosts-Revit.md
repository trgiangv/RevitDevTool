# Revit

Revit is the primary RevitDevTool host and supports every capability, including geometry visualization. See the [Overview](/docs/) for the whole platform.

<LogoRow items={['revit']} />

## What you can do in Revit

- **Run scripts and add-ins**
  - [Python](/docs/execution/python/Execution-Python) scripts that call the Revit API, with dependencies installed from script metadata
  - [C#](/docs/execution/csharp-script/Execution-CSharp) `.csx`, [F#](/docs/execution/fsharp-script/Execution-FSharp) `.fsx` and [IronPython](/docs/execution/ironpython/Execution-VsPyRevit) scripts
  - Compiled [.NET assemblies](/docs/execution/dotnet-assembly/Execution-Assembly)
- **Debug**
  - [Attach VS Code or PyCharm](/docs/execution/python/Execution-PythonDebugging) to Python scripts
  - Read logs and Python stack traces with [Log Level](/docs/logging/Logging-Overview) and [Log Output](/docs/logging/Observability-Http)
  - Preview temporary geometry with [Geometry Visualization](/docs/logging/Visualization-Overview)
- **Test inside a live Revit session**
  - [NUnit](/docs/testing/NUnit), [TUnit](/docs/testing/TUnit) or [MSTest](/docs/testing/MSTest)
  - [pytest](/docs/testing/pytest) checks that need Revit context
- **Connect AI clients**
  - Expose tools through [MCP .NET](/docs/mcp/MCP-CSharp) or [MCP Python](/docs/mcp/MCP-Python); clients reach them through the `code_mode` tool
- **Work faster in Revit**
  - Search and run ribbon commands
  - Find elements by ID

## Supported versions

| Revit version | Target framework |
| --- | --- |
| 2022-2024 | `net48` |
| 2025-2026 | `net8.0-windows` |
| 2027 | `net10.0-windows` |

## Revit utilities

Two utilities are available on Revit only:

- **Search Commands** finds any ribbon command by name and runs it. Recently used commands appear first, and you can mark favorites.
- **Element Finder** takes ElementIds, UniqueIds or IFC GUIDs, comma or line separated, and finds the elements in the active document or in a linked model. It can select them, zoom to them, or zoom with a section box in a 3D view, and it copies the IDs of the current selection as ElementId, UniqueId or IfcGuid.

![Search Commands and Element Finder in Revit](/images/features/Feature_SearchCommandElementFinder.gif)

## Python runtime

CPython runs through Python.NET with a Pixi-owned interpreter (Python 3.14 in `%APPDATA%\RevitDevTool\pixi-env`). Revit does not embed CPython, so there is no uv sidecar on this host.

IronPython execution (`*_ipy_script.py`) and IronPython testing (`test_*_ipy.py`) both prefer the pyRevit engine when pyRevit is installed. Without pyRevit, RevitDevTool falls back to IronPython 3.4.2. See [Python Ecosystems](/docs/execution/python/Execution-PythonEcosystems) and [IronPython — pyRevit Folder Compatibility](/docs/execution/ironpython/Execution-VsPyRevit).

## Revit API context

Revit API calls must run in a valid Revit API context. Scripts launched through RevitDevTool already do. Modeless UI or background work must marshal Revit API calls back through the provided execution path.

:::info Revit 2022–2024
Code that targets these versions runs on .NET Framework 4.8. Tests that touch the Revit API need a live Revit session; see [Testing Overview](/docs/testing/Testing-Overview).
:::
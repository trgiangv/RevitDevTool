# Example Scripts

Practical samples grouped by the [four platform pillars](/docs/getting-started/Platform-v3.0-Overview#four-pillars). Most Revit-focused scripts live under [`samples/PythonDemo/commands/`](https://github.com/trgiangv/RevitDevTool/tree/main/samples/PythonDemo/commands/).

![Python Demo](/images/examples/PythonDemo.gif)

---

## Execution

| Example | Description |
|---------|-------------|
| [Data Analysis with Polars](/docs/examples/Examples-DataAnalysis) | Collect Revit data → analyze with Polars → visualize outliers |
| [Dashboard with WebView2](/docs/examples/Examples-Dashboard) | React + TypeScript dashboard in Revit |
| [C# Script Demo](/docs/examples/Examples-CSharpScript) | Roslyn `.csx` without a full add-in project |
| [F# Script Demo](/docs/examples/Examples-FSharpScript) | F# `.fsx` scripting in Revit |
| [PEP 440 Version Specifiers](/docs/examples/Examples-Pep440Versions) | Dependency version syntax for PEP 723 blocks |
| [AutoCAD C# Demo](/docs/examples/Examples-AutoCAD) | AutoCAD-family C# command sample |
| [Civil 3D Python samples](/docs/examples/Examples-Civil3D) | AutoCAD-family civil API scripts |

AutoCAD and Civil 3D example pages illustrate the shared **AutoCAD-family** host — they are not separate RevitDevTool products.

Additional scripts in the demo folder: [logging batch](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/logging_batch_script.py), [modeless window](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/modeless_script.py), [sklearn](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/sklearn_script.py), [shapely](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/shapely_script.py), [trimesh](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/trimesh_script.py), [host Python probe](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/host_python_probe_script.py) (Plant 3D embedded-runtime layout).

---

## Testing

| Example | Description |
|---------|-------------|
| [Civil 3D Examples — pytest section](/docs/examples/Examples-Civil3D#using-pytest-for-validation) | `uv run pytest --host civil3d` with `acad_*` fixtures |

Setup and multi-host configuration: [pytest](/docs/testing/pytest) · NUnit/TUnit: [NUnit](/docs/testing/NUnit) and [TUnit](/docs/testing/TUnit)

---

## AI Tool Integration (MCP)

| Example | Description |
|---------|-------------|
| [MCP Toolsets](/docs/examples/Examples-MCPToolsets) | Python and C# custom toolset samples |

---

## Observability

| Example | Description |
|---------|-------------|
| [Logging & Syntax Highlighting](/docs/examples/Examples-LoggingFormat) | Color keywords, structured output |
| [Curve Visualization](/docs/examples/Examples-VisualizationCurves) | DirectContext3D curves (Revit-only) |

Additional visualization scripts: [XYZ points](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/visualization_xyz_script.py), [solids](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/visualization_solid_script.py)

---

## Quick Start

1. Open RevitDevTool panel → Code Execution tab
2. Load folder: `samples/PythonDemo/commands/`
3. Click any script to execute
4. Dependencies install automatically on first run (PEP 723)

---

## Documentation

- [Code Execution](/docs/execution/Execution-Overview) — script execution and dependencies
- [Logging](/docs/logging/Logging-Overview) — output and formatting
- [Visualization](/docs/logging/Visualization-Overview) — 3D geometry display (Revit)

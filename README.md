# RevitDevTool

<p style="vertical-align: center;">
Developer platform for .NET-based CAD/BIM applications — code execution, AI integration, testing, visualization, and logging.
</p>

<div style="vertical-align: center;">
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/badge/Revit-2022--2027-blue.svg?style=for-the-badge" alt="RevitVersion"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/badge/AutoCAD-2022--2027-orange.svg?style=for-the-badge" alt="AutoCADVersion"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/github/v/release/trgiangv/RevitDevTool?style=for-the-badge" alt="Badge"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/github/downloads/trgiangv/RevitDevTool/total?style=for-the-badge" alt="Badge"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/commits/develop"><img src="https://img.shields.io/github/last-commit/trgiangv/RevitDevTool/develop?style=for-the-badge" alt="Badge"></a>
</div>

---

## 🎯 Why RevitDevTool?

RevitDevTool is a **development platform for .NET-based CAD/BIM applications**. Run Python/C#/F# scripts, connect AI assistants, and execute tests against live host instances, all from a single toolkit. Currently supports Revit and AutoCAD-family — designed to extend to any .NET-capable host.

**For developers and researchers who need:**

- **Modern Python** — CPython 3.13 with full ecosystem (pandas, numpy, scikit-learn, AI/ML)
- **VSCode Debugging** — Set breakpoints, step through code, inspect Revit/AutoCAD API objects
- **Zero-friction dependencies** — Declare packages inline (PEP 723), auto-install with [Pixi](https://pixi.sh/)
- **AI-powered workflows** — [Model Context Protocol (MCP)](https://github.com/trgiangv/RevitDevTool/wiki/MCP-Overview) integration for AI assistants
- **Remote testing** — [pytest bridge](https://github.com/trgiangv/RevitDevTool.PyTest) runs tests inside live Revit/AutoCAD/Civil3D
- **Multiple script runtimes** — CPython, IronPython (pyRevit), C# `.csx`, F# `.fsx`
- **3D Visualization** — Render geometry directly in Revit view (Revit host)
- **Real-time logging** — Color-coded output with stack traces and JSON formatting

---

## 🖥️ Supported Hosts

| Host | Versions | Python | .NET | MCP | PyTest | Visualization |
|------|----------|--------|------|-----|--------|---------------|
| **Revit** | 2022–2027 | CPython 3.13 | C# / F# / Assembly | Full | Full | DirectContext3D |
| **AutoCAD** | 2022–2027 | CPython 3.13 | C# / Assembly | Full | Full | — |
| **Civil 3D** | 2022–2027 | CPython 3.13 | C# / Assembly | Full | Full | — |
| **Plant 3D** | 2022–2027 | CPython 3.13 | C# / Assembly | Full | Full | — |

AutoCAD-family hosts (Civil 3D, Plant 3D, AutoCAD Architecture, MEP, Electrical, Mechanical, Map 3D) share the same add-in and feature set.

---

## 🎬 See It In Action

### Python Debugging with VSCode

Set breakpoints, inspect variables, step through Revit API calls in real-time.

![Python Debugger](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_PythonDebugger.gif)

**[Python Debugging Guide](https://github.com/trgiangv/RevitDevTool/wiki/Execution-PythonDebugging)**

---

### Automatic Dependency Management

Declare packages inline. Pixi auto-installs. No manual pip, no venv setup.

![Python Dependency Resolve](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_PythonDependencyResolve.gif)

```python
# /// script
# dependencies = ["pandas==2.1.0", "numpy>=1.24"]
# ///

from Autodesk.Revit import DB

doc = __revit__.ActiveUIDocument.Document
walls = DB.FilteredElementCollector(doc).OfClass(DB.Wall)

data = [{"Name": w.Name, "Area": w.Area} for w in walls]
df = pd.DataFrame(data)
print(df.groupby("Level").agg({"Area": ["sum", "mean"]}))
```

**[Python Execution Guide](https://github.com/trgiangv/RevitDevTool/wiki/Execution-Python)**

---

### Real-time Logging with Stack Traces

Monitor output with color coding, JSON formatting, and Python stack traces.

![Monitor Logging](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_MonitorLogging.gif)

![Stack Trace](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_StackTrace.gif)

**[Logging Guide](https://github.com/trgiangv/RevitDevTool/wiki/Logging-Overview)**

---

### 3D Geometry Visualization (Revit)

Render curves, faces, solids directly in Revit view without creating model elements.

![Geometry Visualization](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_TraceGeometry.gif)

```python
ref = uidoc.Selection.PickObject(ObjectType.Edge)
edge = elem.GetGeometryObjectFromReference(ref)
print(edge)  # Renders in 3D view
```

**[Visualization Guide](https://github.com/trgiangv/RevitDevTool/wiki/Visualization-Overview)**

---

## ⚡ Quick Start

### 1. Install

Download and run the MSI installer from [Releases](https://github.com/trgiangv/RevitDevTool/releases/latest).

### 2. Write Your First Script

Create `hello.script.py`:

```python
# /// script
# dependencies = []
# ///

from Autodesk.Revit import DB

doc = __revit__.ActiveUIDocument.Document
walls = DB.FilteredElementCollector(doc).OfClass(DB.Wall)

print(f"Found {walls.GetElementCount()} walls")
```

### 3. Execute

1. Open RevitDevTool panel in Revit (or AutoCAD)
2. Load folder containing your script
3. Click Execute
4. See output in Trace panel

**[Complete Getting Started Guide](https://github.com/trgiangv/RevitDevTool/wiki/Home#getting-started)**

---

## 🌟 Key Features

### Python Execution

- **CPython 3.13** with full ecosystem access
- **PEP 723 inline dependencies** — no separate requirements.txt
- **Pixi resolver** — automatic package installation with conda-forge + PyPI
- **VSCode debugger** — full IDE debugging with breakpoints
- **Module isolation** — clean cache between runs
- **Type stubs** — full Revit/AutoCAD API autocomplete in IDE

### .NET Execution

- **IExternalCommand discovery** — automatic command detection
- **FileWatcher** — auto-reload on assembly changes
- **Dependency loading** — all DLLs loaded automatically
- **C# / F# scripts** — Roslyn-based script execution

### AI Integration (MCP)

- **Model Context Protocol** — AI assistants interact with live host instances
- **Multi-host discovery** — `DevTools.Daemon` tray app finds all running hosts
- **Built-in tools** — `list_host_instances`, `launch_host`, `open_model`, `execute_csharp_code`, `open_document`
- **Custom toolsets** — Python/C# MCP tools registered via file convention
- **Works with** — Cursor, Claude Desktop, VS Code Copilot, any MCP client
- **Architecture docs**: [MCP Architecture](docs/MCP/README.md)

### Remote Testing (PyTest)

- **pytest bridge** — run tests inside live host processes via Named Pipe
- **Multi-host** — `--host revit`, `--host autocad`, `--host civil3d`
- **Auto-discovery** — finds running host instances automatically
- **Auto-launch** — spawns host process if needed
- **IDE integration** — VS Code, Cursor, PyCharm test runners
- **Separate repo**: [`RevitDevTool.PyTest`](https://github.com/trgiangv/RevitDevTool.PyTest) | PyPI: [`revitdevtool_pytest`](https://pypi.org/project/revitdevtool_pytest/)
- **Architecture docs**: [PyTest Bridge](docs/PyTest/README.md)

### Logging System

- **Multi-source capture** — Trace, Console, Debug, Python print
- **Syntax highlighting** — automatic color coding by keywords
- **JSON formatting** — pretty-print with syntax highlighting
- **Stack traces** — Python exception formatting with file links
- **Geometry interception** — auto-visualize printed geometry (Revit)

### Visualization (Revit)

- **DirectContext3D** — transient rendering (no model elements)
- **Multiple geometry types** — curves, faces, solids, meshes, points, bounding boxes
- **Thread-safe** — buffered rendering from any thread
- **Performance optimized** — caching and batch updates

---

## 🆚 Comparisons

### vs pyRevit

| Aspect | RevitDevTool | pyRevit |
|--------|-------------|---------|
| **Target User** | Developers & researchers | End users |
| **Hosts** | Revit + AutoCAD family | Revit only |
| **Python** | CPython 3.13 | IronPython 2.7 (default) |
| **Packages** | Full ecosystem (pandas, numpy, AI/ML) | Limited (Revit API only) |
| **Dependencies** | Automatic (PEP 723 + Pixi) | Manual pip install |
| **Debugging** | VSCode (full IDE) | pdb (command-line) |
| **AI Integration** | MCP protocol | — |
| **Testing** | pytest bridge | — |
| **Best For** | Development, research, data science | Ribbon automation for teams |

**[Detailed Comparison](https://github.com/trgiangv/RevitDevTool/wiki/Execution-VsPyRevit)**

### Python Ecosystem Options

| Tool | Python | Auto-deps | Debugger | AI (MCP) | Best For |
|------|--------|-----------|----------|----------|----------|
| **pyRevit** | IronPython 2.7 | — | — | — | End-user automation |
| **Dynamo** | CPython 3.9 | — | — | — | Visual programming |
| **RevitDevTool** | CPython 3.13 | Pixi | VSCode | Full | Development & research |

**[Complete Ecosystem Analysis](https://github.com/trgiangv/RevitDevTool/wiki/Execution-PythonEcosystems)**

---

## 📦 Installation

### Requirements

- Autodesk Revit 2022–2027 and/or AutoCAD-family 2022–2027
- Windows 10/11
- .NET Framework 4.8 (Revit 2022–2024), .NET 8.0 (2025–2026), .NET 10.0 (2027)

### Install Steps

1. Download MSI installer from [Releases](https://github.com/trgiangv/RevitDevTool/releases/latest)
2. Run installer (admin rights required)
3. Launch Revit or AutoCAD
4. Find RevitDevTool in the ribbon

**[Installation Guide](https://github.com/trgiangv/RevitDevTool/wiki/Getting-Started-Install)**

---

## 📖 Examples

<table>
<tr>
<td valign="top">

## Python
**[Samples/PythonDemo/](Samples/PythonDemo)**

- `dashboard_script.py` — Production BIM dashboard (WebView2 + React)
- `data_analysis_script.py` — Polars data analysis
- `visualization_curve_script.py` — 3D geometry rendering
- `logging_format_script.py` — Log formatting examples
- `sklearn_script.py` — ML with scikit-learn
- `shapely_script.py` — Geometric operations
- `trimesh_script.py` — 3D mesh processing

</td>
<td valign="top">

## .NET
**[Samples/CSharpDemo/](Samples/CSharpDemo)**

- Logging sample with stack trace and JSON formatting
- Geometry visualization sample

## MCP Toolsets
**[Samples/PythonDemo/mcp_tools/](Samples/PythonDemo/mcp_tools)**

- Python MCP toolsets for AI integration
- Custom tools, prompts, resources

</td>
</tr>
</table>

**[All Examples](https://github.com/trgiangv/RevitDevTool/wiki/Examples-Overview)**

---

## 🛠️ Building from Source

### Prerequisites

- .NET 10.0 SDK (via `global.json`)

### Build Steps

```bash
git clone https://github.com/trgiangv/RevitDevTool.git
cd RevitDevTool

# Build for a specific Revit/AutoCAD year
scripts/agent/build-host.ps1 -Year 2025

# Or build directly
dotnet build RevitDevTool.slnx -c "Debug.Autodesk.2025"
```

**Available configurations:** `Debug.Autodesk.2022` through `Debug.Autodesk.2027`, `Release.Autodesk.2022` through `Release.Autodesk.2027`.

**[Build Guide](https://github.com/trgiangv/RevitDevTool/wiki/Build-From-Source)**

---

## 🤝 Contributing

Contributions welcome!

1. **Read architecture docs** — [docs/](docs) for the module you're modifying
2. **Follow design patterns** — Provider, Strategy, Composite patterns
3. **Keep host boundaries** — shared platform in `DevTools.*`, host-specific in `RevitDevTool` / `AcadDevTool`
4. **Update docs** — architecture docs + Wiki if user-facing

**[GitHub Discussions](https://github.com/trgiangv/RevitDevTool/discussions)** for ideas |
**[GitHub Issues](https://github.com/trgiangv/RevitDevTool/issues)** for bugs

---

## 🙏 Acknowledgments

Built on the shoulders of giants:

- [RevitLookup](https://github.com/lookup-foundation/RevitLookup) — DirectContext3D implementation reference
- [RevitDevTool (Original)](https://github.com/Zhuangkh/RevitDevTool) — original project inspiration
- [RevitAddinManager](https://github.com/chuongmep/RevitAddinManager) — add-in hot reload patterns
- [pyRevit](https://github.com/pyrevitlabs/pyRevit) — Python integration inspiration
- [RevitPythonShell](https://github.com/architecture-building-systems/revitpythonshell) — IronPython scripting environment
- [Dynamo](https://github.com/DynamoDS/Dynamo) — visual programming
- [Pixi](https://pixi.sh/) — fast cross-platform package management
- [PythonNet](https://github.com/pythonnet/pythonnet) — Python.NET bridge

---

## 📜 License

MIT License — see [LICENSE](LICENSE) for details.

---

## 💬 Get Help

- 📖 **Documentation** — [GitHub Wiki](https://github.com/trgiangv/RevitDevTool/wiki)
- 🐛 **Bug Reports** — [GitHub Issues](https://github.com/trgiangv/RevitDevTool/issues)
- 💡 **Feature Requests** — [GitHub Discussions](https://github.com/trgiangv/RevitDevTool/discussions)
- ❓ **Questions** — [GitHub Discussions](https://github.com/trgiangv/RevitDevTool/discussions)

---

**Made with ❤️ for the CAD/BIM developer community**
⭐ Star this repo if you find it useful!
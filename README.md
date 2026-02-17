# RevitDevTool

<p style="vertical-align: center;">
Comprehensive developer toolkit for Autodesk Revit with code execution, visualization, and logging.
</p>

<div style="vertical-align: center;">
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/badge/Revit-2022--2026-blue.svg?style=for-the-badge" alt="RevitVersion"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/github/v/release/trgiangv/RevitDevTool?style=for-the-badge" alt="Badge"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/github/downloads/trgiangv/RevitDevTool/total?style=for-the-badge" alt="Badge"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/commits/develop"><img src="https://img.shields.io/github/last-commit/trgiangv/RevitDevTool/develop?style=for-the-badge" alt="Badge"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/actions?query=branch%3Adevelop"><img src="https://img.shields.io/github/check-runs/trgiangv/RevitDevTool/develop?style=for-the-badge&logo=github&label=Build" alt="Badge"></a>
</div>

---

## 🎯 Why RevitDevTool?

**For developers and researchers who need:**

- 🐍 **Modern Python** - CPython 3.13 with full ecosystem (pandas, numpy, scikit-learn, AI/ML libraries)
- 🐛 **VSCode Debugging** - Set breakpoints, step through code, inspect Revit API objects
- 📦 **Zero-friction dependencies** - Declare packages inline, auto-install with UV (10-15x faster than pip)
- 🎨 **3D Visualization** - Render geometry directly in Revit view (no model elements)
- 📊 **Real-time logging** - Color-coded output with stack traces and JSON formatting

**Not another Revit add-in loader.** RevitDevTool is a complete development environment focused on **rapid prototyping, computational design, and data science workflows**.

---

## 🎬 See It In Action

### Python Debugging with VSCode

Set breakpoints, inspect variables, step through Revit API calls in real-time.

![Python Debugger](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_PythonDebugger.gif)

**[→ Python Debugging Guide](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-PythonDebugging)**

---

### Automatic Dependency Management

Declare packages inline. System auto-installs. No manual pip, no venv setup.

![Python Dependency Resolve](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_PythonDependencyResolve.gif)

```python
# /// script
# dependencies = ["pandas==2.1.0", "numpy>=1.24"]
# ///

import pandas as pd
from Autodesk.Revit import DB

doc = __revit__.ActiveUIDocument.Document
walls = DB.FilteredElementCollector(doc).OfClass(DB.Wall)

data = [{"Name": w.Name, "Area": w.Area} for w in walls]
df = pd.DataFrame(data)
print(df.groupby("Level").agg({"Area": ["sum", "mean"]}))
```

**[→ Python Execution Guide](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-PythonExecution)**

---

### Real-time Logging with Stack Traces

Monitor output with color coding, JSON formatting, and Python stack traces.

![Monitor Logging](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_MonitorLogging.gif)

![Stack Trace](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_StackTrace.gif)

**[→ Logging Guide](https://github.com/trgiangv/RevitDevTool/wiki/Logging-Overview)**

---

### 3D Geometry Visualization

Render curves, faces, solids directly in Revit view without creating model elements.

![Geometry Visualization](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_TraceGeometry.gif)

```python
# Pick edge, visualize automatically
ref = uidoc.Selection.PickObject(ObjectType.Edge)
edge = elem.GetGeometryObjectFromReference(ref)
print(edge)  # Renders in 3D view
```

**[→ Visualization Guide](https://github.com/trgiangv/RevitDevTool/wiki/Visualization-Overview)**

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

1. Open RevitDevTool panel in Revit
2. Load folder containing your script
3. Click Execute
4. See output in Trace panel

**[→ Complete Getting Started Guide](https://github.com/trgiangv/RevitDevTool/wiki/Home#getting-started)**

---

## 🌟 Key Features

### 🐍 Python Execution

- **CPython 3.13** - Latest Python with full ecosystem access
- **PEP 723 inline dependencies** - No separate requirements.txt
- **UV resolver** - Automatic package installation (10-15x faster than pip)
- **VSCode debugger** - Full IDE debugging with breakpoints
- **Module isolation** - Clean cache between runs
- **Type stubs** - Full Revit API autocomplete in IDE

### 🔧 .NET Execution

- **IExternalCommand discovery** - Automatic command detection
- **FileWatcher** - Auto-reload on assembly changes
- **Dependency loading** - All DLLs loaded automatically
- **No temp folder** - Direct execution for Revit 2024- (.NET 4.8)

### 📊 Logging System

- **Multi-source capture** - Trace, Console, Debug, Python print
- **Syntax highlighting** - Automatic color coding by keywords
- **JSON formatting** - Pretty-print with syntax highlighting
- **Stack traces** - Python exception formatting with file links
- **Geometry interception** - Auto-visualize printed geometry

### 🎨 Visualization

- **DirectContext3D** - Transient rendering (no model elements)
- **Multiple geometry types** - Curves, faces, solids, meshes, points, bounding boxes
- **Thread-safe** - Buffered rendering from any thread
- **Performance optimized** - Caching and batch updates

---

## 🆚 Comparisons

### vs pyRevit

| Aspect                 | RevitDevTool                          | pyRevit                     |
| ---------------------- | ------------------------------------- | --------------------------- |
| **Target User**  | Developers & researchers              | End users                   |
| **Python**       | CPython 3.13                          | IronPython 2.7 (default)    |
| **Packages**     | Full ecosystem (pandas, numpy, AI/ML) | Limited (Revit API only)    |
| **Dependencies** | Automatic (PEP 723 + UV)              | Manual pip install          |
| **Debugging**    | VSCode (full IDE)                     | pdb (command-line)          |
| **Best For**     | Development, research, data science   | Ribbon automation for teams |

**[→ Detailed Comparison](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-VsPyRevit)**

### Python Ecosystem Options

| Tool                   | Python         | Auto-dependencies | VSCode Debugger | Best For               |
| ---------------------- | -------------- | ----------------- | --------------- | ---------------------- |
| **pyRevit**      | IronPython 2.7 | ❌                | ❌              | End-user automation    |
| **Dynamo**       | CPython 3.9    | ❌                | ❌              | Visual programming     |
| **RevitDevTool** | CPython 3.13   | ✅ UV             | ✅ debugpy      | Development & research |

**[→ Complete Ecosystem Analysis](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-PythonEcosystems)**

---

## 📦 Installation

### Requirements

- Autodesk Revit 2022-2026
- Windows 10/11

### Install Steps

1. Download MSI installer from [Releases](https://github.com/trgiangv/RevitDevTool/releases/latest)
2. Run installer (admin rights required)
3. Launch Revit
4. Find RevitDevTool in External Tools ribbon

**[→ Installation Guide](https://github.com/trgiangv/RevitDevTool/wiki/Home#installation)**

---

## 📖 Documentation

<table>
<tr>
<td style="vertical-align: top;width: 50%;">

## 🎓 Python Scripts
**[source/RevitDevTool.PythonDemo/commands/](source/RevitDevTool.PythonDemo/commands)**

- `dashboard_script.py` - Production BIM dashboard
- `data_analysis_script.py` - Polars data analysis
- `debugpy_script.py` - VSCode debugger integration
- `visualization_curve_script.py` - 3D geometry rendering
- `logging_format_script.py` - Log formatting examples
- `sklearn_script.py` - ML with scikit-learn
- `shapely_script.py` - Geometric operations
- `trimesh_script.py` - 3D mesh processing

**[→ All Examples](https://github.com/trgiangv/RevitDevTool/wiki/Examples-Overview)**

</td>
<td style="vertical-align: top;width: 50%;">

## 🛠️ .NET Commands
**[source/RevitDevTool.DotnetDemo/](source/RevitDevTool.DotnetDemo)**

- **Basic IExternalCommand**: Các ví dụ cơ bản về thực thi lệnh.
- **Logging**: Trình diễn hệ thống ghi log.
- **Geometry**: Các pattern hiển thị hình học trực quan.

</td>
</tr>    
</table>

---

## 🛠️ Building from Source

### Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 or Rider
- Git with Git LFS

### Build Steps

```bash
git clone https://github.com/trgiangv/RevitDevTool.git
cd RevitDevTool

# Restore and build for Revit 2025
dotnet restore
dotnet build RevitDevTool.sln -c "Release R25"
```

**Available configurations:** `Release R22`, `Release R23`, `Release R24`, `Release R25`, `Release R26`

**[→ Build Guide](docs/README.md#🤝 Contributing)**

---

## 🤝 Contributing

Contributions welcome! Here's how to get started:

1. **Read architecture docs** - [docs/](docs) for module you're modifying
2. **Follow design patterns** - Provider, Strategy, Composite patterns
3. **Add tests** - Demo scripts or unit tests
4. **Update docs** - Architecture docs + Wiki if user-facing

**[→ GitHub Discussions](https://github.com/trgiangv/RevitDevTool/discussions)** for ideas
**[→ GitHub Issues](https://github.com/trgiangv/RevitDevTool/issues)** for bugs

---

## 🙏 Acknowledgments

Built on the shoulders of giants:

- [RevitLookup](https://github.com/lookup-foundation/RevitLookup) - DirectContext3D implementation reference
- [RevitDevTool (Original)](https://github.com/Zhuangkh/RevitDevTool) - Original project inspiration
- [RevitAddinManager](https://github.com/chuongmep/RevitAddinManager) - Add-in hot reload patterns
- [pyRevit](https://github.com/pyrevitlabs/pyRevit) - Python integration inspiration
- [Dynamo](https://github.com/DynamoDS/Dynamo) - Visual programming
- [RevitPythonShell](https://github.com/architecture-building-systems/revitpythonshell) - IronPython scripting environment
- [UV](https://github.com/astral-sh/uv) - Fast Python package resolver
- [PythonNet](https://github.com/pythonnet/pythonnet) - Python.NET bridge

---

## 📜 License

MIT License - see [LICENSE](LICENSE) for details.

---

## 💬 Get Help

- 📖 **Documentation** - [GitHub Wiki](https://github.com/trgiangv/RevitDevTool/wiki)
- 🐛 **Bug Reports** - [GitHub Issues](https://github.com/trgiangv/RevitDevTool/issues)
- 💡 **Feature Requests** - [GitHub Discussions](https://github.com/trgiangv/RevitDevTool/discussions)
- ❓ **Questions** - [GitHub Discussions](https://github.com/trgiangv/RevitDevTool/discussions)

---

**Made with ❤️ for the Revit developer community**
⭐ Star this repo if you find it useful!
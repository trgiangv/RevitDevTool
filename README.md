# RevitDevTool

<p align="left">
Comprehensive developer toolkit for Autodesk Revit with code execution, visualization, and logging.
</p>

<div align="center">
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/badge/Revit-2022--2026-blue.svg?style=for-the-badge" alt="RevitVersion"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/github/v/release/trgiangv/RevitDevTool?style=for-the-badge" alt="Badge"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/releases/latest"><img src="https://img.shields.io/github/downloads/trgiangv/RevitDevTool/total?style=for-the-badge" alt="Badge"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/commits/develop"><img src="https://img.shields.io/github/last-commit/trgiangv/RevitDevTool/develop?style=for-the-badge" alt="Badge"></a>
    <a href="https://github.com/trgiangv/RevitDevTool/actions?query=branch%3Adevelop"><img src="https://img.shields.io/github/check-runs/trgiangv/RevitDevTool/develop?style=for-the-badge&logo=github&label=Build" alt="Badge"></a>
</div>

---

## 🚀 Quick Start

RevitDevTool provides three integrated modules for Revit development:

| Module | Description | Documentation |
|--------|-------------|---------------|
| **🐍 CodeExecute** | Multi-language code execution with auto dependency management | [User Guide](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-Overview) • [Architecture](docs/CodeExecute/architecture/) |
| **📊 Logging** | Real-time log capture with color coding and JSON formatting | [User Guide](https://github.com/trgiangv/RevitDevTool/wiki/Logging-Overview) • [Architecture](docs/Logging/architecture/) |
| **🎨 Visualization** | 3D geometry rendering using DirectContext3D | [User Guide](https://github.com/trgiangv/RevitDevTool/wiki/Visualization-Overview) • [Architecture](docs/Visualization/architecture/) |

**📚 [Full Documentation](https://github.com/trgiangv/RevitDevTool/wiki)** • **🎓 [Getting Started](https://github.com/trgiangv/RevitDevTool/wiki/Home)**

---

## 📸 Visual Demos

### Montior Logging
![Montior Logging](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_MonitorLogging.gif)

### Stack Traces
![Stack Trace](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_StackTrace.gif)

### Geometry Visualization
![Geometry Visualization](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_TraceGeometry.gif)

### File Logging
![File Logging](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_FileLogging.gif)

### Floating Window
![Floating Window](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_AutoFloating.gif)

---

## ✨ Key Features

### CodeExecute
- ✅ Execute Python 3.13 scripts with CPython + PythonNet3
- ✅ PEP 723 inline dependency declaration
- ✅ Automatic package installation with [UV](https://github.com/astral-sh/uv) resolver
- ✅ .NET hot-reload (no temp folder copying)
- ✅ File watcher for instant reload

### Logging  
- ✅ Multi-source capture (Trace, Console, Debug, Python print)
- ✅ Syntax highlighting with keyword detection
- ✅ Pretty JSON formatting
- ✅ Geometry interception → automatic visualization
- ✅ Python stack trace formatting

### Visualization
- ✅ Real-time 3D geometry display (no model elements)
- ✅ Support for curves, faces, solids, meshes, points, bounding boxes
- ✅ DirectContext3D transient rendering
- ✅ Thread-safe buffering
- ✅ Performance optimization with caching

---

## 📦 Installation

1. **Download** the latest release from [GitHub Releases](https://github.com/trgiangv/RevitDevTool/releases)
2. **Run** the MSI installer
3. **Launch** Revit (2022-2026)
4. **Open** the RevitDevTool panel from External Tools ribbon

---

## 📖 Documentation

### 📚 User Guides
**Location:** [GitHub Wiki](https://github.com/trgiangv/RevitDevTool/wiki)

Step-by-step guides for using RevitDevTool features:

- **[Home](https://github.com/trgiangv/RevitDevTool/wiki/Home)** - Overview and quick links
- **[Getting Started](https://github.com/trgiangv/RevitDevTool/wiki/Home#getting-started)** - Your first script

**Module Documentation:**
- **[CodeExecute](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-Overview)** - Python & .NET execution
- **[Logging](https://github.com/trgiangv/RevitDevTool/wiki/Logging-Overview)** - Logging system
- **[Visualization](https://github.com/trgiangv/RevitDevTool/wiki/Visualization-Overview)** - Geometry visualization

**Comparisons:**
- [vs pyRevit](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-VsPyRevit) - Python execution comparison
- [vs RevitAddinManager](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-VsRevitAddinManager) - Add-in development comparison
- [Python Ecosystems](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-PythonEcosystems) - IronPython vs CPython vs PythonNet3

### 🏗️ Architecture Documentation
**Location:** [docs/](docs/)

Technical documentation for developers and contributors:

- **[docs/README.md](docs/README.md)** - Architecture overview
- **[CodeExecute/architecture/](docs/CodeExecute/architecture/)** - Execution framework design
- **[Logging/architecture/](docs/Logging/architecture/)** - Logging infrastructure
- **[Visualization/architecture/](docs/Visualization/architecture/)** - Rendering system

---

## 🎯 Common Use Cases

| I want to... | Go to... |
|-------------|----------|
| Execute a Python script | [CodeExecute Overview](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-Overview) |
| Auto-install packages | [Python Runtime](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-PythonRuntime) |
| Compare Python options | [Python Ecosystems](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-PythonEcosystems) |
| See trace output with colors | [Logging Overview](https://github.com/trgiangv/RevitDevTool/wiki/Logging-Overview) |
| Visualize geometry in 3D | [Visualization Overview](https://github.com/trgiangv/RevitDevTool/wiki/Visualization-Overview) |
| Generate type stubs | [Stub Generation](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-StubGeneration) |
| Understand hot reload | [.NET Runtime](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-DotNetRuntime) |
| Build custom providers | [CodeExecute Architecture](docs/CodeExecute/architecture/) |

---

## 🛠️ Development

### Building from Source

```bash
# Clone repository with Git LFS support
git lfs install
git clone https://github.com/trgiangv/RevitDevTool.git
cd RevitDevTool

# Restore packages
dotnet restore

# Build for specific Revit version (R22, R23, R24, R25, R26)
dotnet build RevitDevTool.sln -c "Release R25"
```

**Note:** This project uses [Git LFS](https://git-lfs.com/) for binary files. Make sure you have Git LFS installed before cloning.

### Project Structure

```
RevitDevTool/
├── source/
│   ├── RevitDevTool/              # Main plugin
│   │   ├── CodeExecute/           # Execution framework
│   │   ├── Logging/               # Logging infrastructure
│   │   └── Visualization/         # Rendering system
│   ├── RevitDevTool.PythonDemo/   # Python demo: 12+ scripts + dashboard (data analysis, visualization)
│   ├── RevitDevTool.DotnetDemo/   # .NET demo: Basic examples (logging, geometry)
│   └── PythonNetStubGenerator/    # Type stub generator for Revit API
├── docs/                          # Architecture documentation
├── install/                       # Installer scripts
└── build/                         # Nuke build
```

### Contributing

Contributions are welcome! See [GitHub Discussions](https://github.com/trgiangv/RevitDevTool/discussions) for ideas or [GitHub Issues](https://github.com/trgiangv/RevitDevTool/issues) for bugs to fix.

---

## 🙏 Acknowledgments

- [RevitDevTool](https://github.com/trgiangv/RevitDevTool) - DirectContext3D implementation reference
- [RevitDevTool (Original)](https://github.com/Zhuangkh/RevitDevTool) - Original project inspiration
- [RevitAddinManager](https://github.com/chuongmep/RevitAddinManager) - Add-in hot reload patterns
- [pyRevit](https://github.com/pyrevitlabs/pyRevit) - Python integration inspiration
- [Dynamo](https://github.com/DynamoDS/Dynamo) - Visual programming
- [RevitPythonShell](https://github.com/architecture-building-systems/revitpythonshell) - IronPython REPL

---

## 📜 License

This project is licensed under the MIT License - see [LICENSE](LICENSE) file for details.

---

## 💬 Support & Community

- **Issues**: [GitHub Issues](https://github.com/trgiangv/RevitDevTool/issues)
- **Discussions**: [GitHub Discussions](https://github.com/trgiangv/RevitDevTool/discussions)
- **Wiki**: [Documentation Wiki](https://github.com/trgiangv/RevitDevTool/wiki)
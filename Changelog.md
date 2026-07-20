# 4.0.0 (Breaking coordinated release)

## Unified MCP runtime

This release makes standard MCP the sole host data-plane protocol. A host now
publishes exactly one canonical pipe, `DevTools_{HostApp}_{HostVersion}_{PID}`.
The retained pipe-name format carries MCP only; it is not a compatibility
bridge.

### Breaking changes

- `DevTools.Mcp.v2.{PID}` has been removed.
- The framed Python pytest bridge and its clients are unsupported.
- There is no protocol fallback, alias, sniffing path, or `dynamic_*` tool
  alias.
- Gateway tunnel v1 daemons cannot connect to Gateway v2. They disconnect with
  `unsupported_tunnel_protocol` until upgraded.
- The installer upgrades compatible desktop components together; deploy the
  matching `revitdevtool_pytest` 0.4.0 and McpGateway 2.0.0 release set.

### Deployment window

Prepare the desktop installer and Python plugin artifacts, enter the declared
maintenance window, deploy Gateway v2, then release and announce the matching
desktop components. Do not mix old daemon binaries with Gateway v2.

The root package version remains GitVersion/tag-derived. `4.0.0` is the
coordinated release tag; no source constant or host-pipe protocol version is
introduced for this release.

### Acceptance status

Automated compatibility and artifact evidence is recorded in
[the 4.0.0 release gate](docs/MCP/release-4.0.0.md). Publication remains
blocked until the listed live Revit/AutoCAD and authenticated Gateway scenarios
are performed.

# 3.0.0

v3.0 transforms RevitDevTool from a Revit-only developer tool into a **development platform for .NET-based CAD/BIM applications**. The execution engine, logging, dependency management, AI integration, and remote testing are now shared across hosts. Revit remains the primary experience, AutoCAD-family products are fully supported, and the architecture is designed to extend to any .NET-capable host (Tekla, Bentley, Rhino, etc.).

## Platform Architecture (new)

Revit-specific code has been extracted into a host project. All reusable behavior now lives in shared `DevTools.*` libraries that multiple hosts consume:

- **Shared platform**: `DevTools.Execution`, `DevTools.Logging`, `DevTools.McpParser`, `DevTools.McpServer`, `DevTools.Presentation`, `DevTools.Settings`, `DevTools.Telemetry`, `DevTools.UI`, `DevTools.Utilities`
- **Host projects**: `RevitDevTool` (Revit), `AcadDevTool` (AutoCAD / Civil 3D / Plant 3D)
- **Solution**: `RevitDevTool.slnx` replaces `RevitDevTool.sln`
- **Build configurations**: `Debug.Autodesk.2022` through `Debug.Autodesk.2027`, `Release.Autodesk.2022` through `Release.Autodesk.2027` (replaces old `Release R25` naming)

Adding a new host (Tekla, Bentley, etc.) requires only a host-specific adapter project — shared libraries stay untouched.

## Multi-Host Support (new)

### AutoCAD-Family

Full add-in for AutoCAD, Civil 3D, Plant 3D, and all AutoCAD verticals (Architecture, MEP, Electrical, Mechanical, Map 3D). Shares the same execution engine, logging, MCP, and PyTest bridge as Revit.

### Revit 2027 / .NET 10

- Added Revit 2027 support targeting `net10.0-windows`
- Build matrix: 2022–2024 → `net48`, 2025–2026 → `net8.0-windows`, 2027 → `net10.0-windows`

## MCP — AI Tool Integration (new)

### Standalone MCP Server

`MCPServer.exe` runs outside host processes and exposes tools to AI assistants (Cursor, Claude Desktop, VS Code Copilot, any MCP client) via the Model Context Protocol:

- **Multi-host discovery**: scans Named Pipes matching `{Host}_{Version}_{PID}` to find all running instances
- **Built-in tools**: `list_host_instances`, `launch_host`, `open_model`, `read_file_info`
- **Routing**: dispatches tool calls to the correct host instance

### In-Host MCP Runtime

Each host registers custom toolsets (Python and C#) that the server can dispatch to via Named Pipe:

- **Built-in in-host tools**: `execute_csharp_code`, `open_document`
- **Custom toolsets**: Python/C# MCP tools registered via folder convention (`mcp_tools/`)
- **Document bridge**: `IDocumentBridge` — shared abstraction for opening documents (Revit projects, DWG files)

## PyTest Bridge — Remote Testing (new)

Run pytest tests inside live host processes via Named Pipe. Published as a separate package: [`revitdevtool_pytest`](https://github.com/trgiangv/RevitDevTool.PyTest) on [PyPI](https://pypi.org/project/revitdevtool_pytest/).

- **Multi-host CLI**: `--host revit`, `--host autocad`, `--host civil3d`, `--host-version`, `--host-pipe`, `--host-launch`
- **Auto-discovery**: finds running host instances and connects automatically
- **Auto-launch**: spawns a new host process if none running
- **IDE integration**: VS Code, Cursor, PyCharm test runners
- **Flexible pipe pattern**: version accepts any string (year, semver, dotted)

## Execution — New Script Runtimes

### C# Scripts (new)

Execute `.csx` files directly — Roslyn-based compilation without a project file.

### F# Scripts (new)

Execute `.fsx` files for interactive/functional scripting workflows.

### IronPython (new)

First-class script mode with pyRevit runtime support and IronPython 3.4.2 fallback. Runs `*_ipy_script.py` files.

### Revit Command Browser (new)

Searchable access to all registered Revit commands from the dockable panel.

## Logging

### High-Performance Log Monitor

The logging subsystem has been re-architected for performance:

- **ZLogger** replaces Serilog as the structured logging backend — significantly lower allocation and faster throughput
- **Scintilla.NET** replaces WinForms RichTextBox as the log rendering surface — handles large volumes of output without UI freezing
- Syntax highlighting, JSON formatting, and color keywords retain the same user experience on the new backend

## Dependency Management

### Pixi-First

**Pixi** replaces UV as the primary dependency resolver:

- Resolves from conda-forge + PyPI channels
- Pixi environment managed under `%APPDATA%\RevitDevTool\pixi-env\`
- PEP 723 inline dependencies auto-installed on script execution
- pip/pyRevit CPython available as fallback

## Bug Fixes

- **Python state attribute**: unified to `sys.__devtool__` across all hosts (was `sys.__revitdevtool__` for Revit, `sys.__acaddevtool__` for AutoCAD — caused crashes on AutoCAD)
- **Startup dialog resolver**: expanded keywords to cover AutoCAD, Civil 3D, Plant 3D startup dialogs

# 2.2.0

## 🚀 Major Features

### Python Debugging with VSCode

Full IDE debugging support for Python scripts in Revit:

- **VSCode Debugger Integration**
  - Set breakpoints directly in VSCode
  - Step through code execution (F10, F11)
  - Inspect variables and Revit API objects
  - Debug console for expression evaluation
  - Conditional breakpoints support
  - Visual connection status indicator (🔴/🟢)

  ![Python Debugger](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_PythonDebugger.gif)

- **Technical Implementation**
  - `debugpy` integration via PEP 723 automatic installation
  - Configurable debug port (default: 5678)
  - In-process debug adapter for seamless integration
  - Connection status monitoring in UI
  - Non-blocking - scripts run normally without debugger attached

- **New Components**
  - `PythonInitializer` - Manages Python runtime and debugpy listener
  - `IsDebuggerConnected` property - Real-time connection status
  - Debug port configuration in General Settings
  - Status indicator in Trace panel

**Documentation:**
- [Python Debugging Guide](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-PythonDebugging)
- Demo script: `debugpy_script.py`

## 🎯 New Features

### PythonNet3 Interface Implementation Demo

- **Interface Implementation Workaround**
  - Added `selectionfilter_script.py` demonstrating `__namespace__` pattern
  - Shows how to implement .NET interfaces (e.g., `ISelectionFilter`) in PythonNet3
  - Documents runtime overhead trade-off for dynamic type checking

## 🔧 Improvements

### Python Execution

- **Streamlined Script Execution**
  - Refactored `PythonExecutor` for cleaner scope management
  - Improved script content execution flow
  - Better separation of concerns between initialization and execution
  - Enhanced error handling and cleanup

### Stub Generation

- **Enhanced Method Writing**
  - Improved generic type handling in stub generation
  - Better overload detection and signature writing
  - More accurate property accessor detection
  - Enhanced type mapping for complex .NET types

## 📚 Documentation

### Comprehensive Documentation Updates

- **README Enhancements**
  - Redesigned for better first impression
  - Visual-first approach with demos and context
  - Clear value proposition and target audience
  - Improved structure and navigation
  - Added comparison tables (vs pyRevit, Python ecosystems)

- **Wiki Updates**
  - New dedicated [Python Debugging](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-PythonDebugging) page
  - Updated [Python Execution](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-PythonExecution) with debugger info
  - Revised [vs pyRevit](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-VsPyRevit) to focus on target audiences
  - Updated [Python Ecosystems](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-PythonEcosystems) with objective analysis
  - Simplified [CodeExecute Overview](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-Overview) to be true overview
  - Enhanced [Dashboard Example](https://github.com/trgiangv/RevitDevTool/wiki/Examples-Dashboard) with accurate architecture

- **Architecture Documentation**
  - Converted text diagrams to Mermaid charts for better readability
  - Updated PythonDemo architecture with debugging workflow
  - Improved cross-repo link handling (relative paths → URLs)
  - Removed architecture details from Wiki (moved to docs/)

# 2.1.1

## 🎯 New Features

- **Python UV Installer Improvements**
  - Added robust UV installation and update management flow
  - Improved detection and handling for local UV binaries
  - Enhanced reliability for Python package tooling bootstrap

## 🔧 Improvements

- **CI Workflow Enhancements**
  - Compile workflow now builds per-Revit version via dynamic matrix
  - Matrix versions are derived from `source/Directory.Build.props` to avoid hardcoded values
  - Added workflow concurrency controls with auto-cancel on newer runs
  - Added cross-workflow lock so compile and release do not run in parallel

- **Build Configuration Flexibility**
  - Exposed build configuration patterns as Nuke parameter for targeted CI builds

## 🛠️ Technical Changes

- Updated dependency definitions for Python/UV execution path
- Refined UV resource handling in project configuration

---

**Stats**: 9 files changed, 221 insertions(+), 34 deletions(-)

# 2.1.0

## 🚀 Major Features

### Python Ecosystem Integration

A complete Python runtime environment with professional package management and development tools:

- **Python Script Execution**
  - Execute Python scripts directly in Revit with native Python runtime
  - Support for PEP 723 inline script metadata for dependency declarations
  - Built-in `__file__` variable for script file path access
  - Enhanced stdout/stderr redirection for better debugging
  - Support for processing Python iterable objects (lists, tuples, generators) in GeometryListener

- **Python Dependency Management**
  - Integrated `uv` package manager for fast, reliable Python package installation
  - Professional dependency resolution and conflict management
  - UI for installing and managing Python packages
  - Automatic detection of newly-installed packages for immediate use
  - Support for `pyproject.toml` dependency specifications

  ![Python Dependency Management](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_PythonDependencyResolve.gif)

- **Python Stub Generator (PythonNetStubGenerator)**
  - Generate Python type stubs (.pyi) from .NET assemblies for IntelliSense support
  - Complete support for methods, properties, events, and nested types
  - XML documentation comments extraction and conversion
  - Enhanced IDE experience with autocomplete and type hints

  ![Python Stubs](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_PythonStubs.png)

- **Enhanced Python Type System**
  - Improved type conversion for delegates and indexers
  - Better handling of Python-to-.NET type conversions
  - Support for complex Python data structures

### Code Execution System (NEW)

Brand new code execution infrastructure built from scratch with modern design patterns:

- **Unified Execution System**
  - Strategy Pattern implementation for DotNet and Python execution
  - Centralized ExecutionOrchestrator for managing execution workflows
  - Clean separation between execution providers and strategies
  - Support for multiple execution modes (File, Selection, Addin)

- **Tree-Based Code Structure**
  - New tree model for representing directory and file hierarchies
  - Efficient tree state management with caching
  - Smart file watching with debouncing for hot reload
  - Enhanced tree operations (expand, collapse, refresh)
  - Visual feedback with highlight ranges for executed code

  ![Code Execute Hot Reload](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_CodeExecuteHotReload.gif)

- **Add-in Manager**
  - Load and execute external Revit add-ins (.dll) dynamically
  - Assembly Load Context isolation for proper unloading
  - Track last executed add-in for quick re-execution
  - Support for IExternalCommand implementations

### Python Dashboard Demo

Professional-grade Python analytics dashboard showcasing advanced integration:

- **Modern Web-Based UI**
  - Built with React, TypeScript, and TailwindCSS
  - Hosted in WebView2 with seamless Revit integration
  - Real-time bidirectional communication bridge
  - Support for light/dark themes

  ![Python Dashboard](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_PythonDashboard.png)

- **Comprehensive Analytics**
  - Project metrics and statistics dashboard
  - Family and category inventory analysis
  - Health monitoring with warnings matrix
  - Heavy families tracker for performance optimization
  - Schedule data explorer with export to Excel

- **Advanced Features**
  - Element filtering and selection synchronization
  - Keyboard shortcuts for power users
  - Session history tracking
  - Automatic data refresh with progress indicators

  ![Python Demo](https://github.com/trgiangv/RevitDevTool/wiki/images/RevitDevTool_PythonDemo.gif)

## 🎯 New Features

- **Code Execute View**
  - New dockable panel for code execution interface
  - Tree view with directory and file browsing
  - Syntax highlighting for selected code regions
  - Double-click to execute files or selections

- **Trace Toggle Setting**
  - New `IsTraceEnabled` setting to enable/disable trace listener
  - Improves performance when tracing is not needed
  - Configurable via settings UI

- **CSharp Execution Thread Safety**
  - Wrapped command execution in external event handler
  - Prevents threading issues when executing code from UI

- **Python Package Imports**
  - Ensured newly-installed packages are immediately importable
  - Fixed module path resolution after package installation

## 📚 Documentation

- **Comprehensive Architecture Documentation**
  - Added architecture guides for all major modules:
    - Code Execution (Strategy Pattern, Tree Model)
    - Logging System (Listeners, Theme System, Python Integration)
    - Visualization System (Geometry Processing)
    - Python Demo (Dashboard, WebView2 Integration)
    - PythonNetStubGenerator (Stub Generation Process)
  - Developer guides for extending functionality
  - Detailed README files with diagrams and examples

## 🔧 Improvements

- **Refactored Logging Infrastructure**
  - Reorganized listeners for better modularity
  - Improved separation between LoggerTraceListener and NotifyListener
  - Enhanced GeometryListener with Python iterable support
  - Better error handling and diagnostics

- **Optimized File Operations**
  - Enhanced file watching with better performance
  - Debouncing for file change events
  - Reduced unnecessary tree refreshes

- **Code Quality**
  - Cleaned up invalid assembly and directory paths
  - Simplified module removal logic in PythonExecutor
  - Better script execution and output handling
  - More robust error handling throughout

- **Settings Management**
  - Added CodeExecuteConfig for execution-related settings
  - Improved settings persistence and loading
  - Better validation for configuration values

## 🐛 Fixes

- **Dockable Pane Visibility**
  - Streamlined visibility handling for trace command
  - Better state management for floating vs docked windows

## 🛠️ Technical Changes

- **Build System**
  - Added Git LFS filters for executable and archive files (.exe, .dll, .msi, .zip)
  - Updated global.json for SDK compatibility
  - Enhanced project dependencies management

- **Resources**
  - Added Python icon (python32.png) for UI consistency
  - Bundled uv executables (uv.exe, uvw.exe, uvx.exe) for offline package management
  - Moved and reorganized image assets

---

**Stats**: 236 files changed, 32,835 insertions(+), 773 deletions(-)

# 2.0.1

## Fixed

- Ensure UI thread handling for open/close FloatingWindow

## Documentation

- Updated usage instructions in README
- Clarified `Trace` methods support for Geometry and Pretty JSON

# 2.0.0

## Breaking Changes

- **Dropped Revit 2021 Support** - Minimum supported version is now Revit 2022
- **Removed SQLite Log Format** - SQLite (.db) logging format has been removed. Use JSON or CLEF formats for structured logging
- **Removed CLEF Log Format** - CLEF (.clef) format has been removed. Use JSON format for Seq integration

## Added

- **WPF Trace Listener**
  - Capture WPF binding errors and trace output directly in the log panel
  - Configurable WPF trace level (Off, Error, Warning, Information, Verbose)
  - Helps debug XAML binding issues without external tools

- **Filter Keywords for Log Level Detection**
  - Automatic log level detection based on message content
  - Customizable keywords for each log level (Information, Warning, Error, Critical)
  - Default keywords: `info,success,completed` for Info, `warning,warn,caution` for Warning, etc.
  - Supports prefix detection: `[INFO]`, `[WARN]`, `[ERROR]`, `[FATAL]`, `[DEBUG]`

- **Pretty JSON Output**
  - Enable formatted JSON output for complex objects in trace logs
  - Objects logged via `Trace.WriteLine(object)` are automatically serialized
  - Configurable via settings UI

- **Revit Enrichers**
  - Add Revit context to every log entry automatically
  - Available enrichers: Revit Version, Document Title, Document Path, Model Path, Addin ID
  - Configurable via settings UI

- **New Single-Page Settings UI**
  - All settings now accessible in a single integrated panel
  - No more separate settings window - everything in the dockable panel
  - Cleaner, more intuitive configuration experience

- **Live Geometry Count Badge**
  - Real-time badge showing count of geometry objects currently visualized
  - Visual feedback for geometry pool status
  - Helps track visualization state at a glance

- **Python Stack Trace Support**
  - Enhanced logging for Python/pyRevit scripts with full traceback
  - `PyTrace.Write(message, traceback)` bridge for Python integration
  - Respects stack trace depth settings
  - Helper script: [`source/RevitDevTool/Logging/Python/trace.py`](source/RevitDevTool/Logging/Python/trace.py)

- **Auto-Clean Log Files**
  - Automatically clean old log files based on rolling interval
  - Prevents log folder from growing indefinitely

- **Process ID in Log Files**
  - Log file names now include process ID for multi-instance scenarios
  - Easier identification of logs from different Revit sessions

## Changed

- **Logging Architecture Refactored**
  - Complete reorganization of logging infrastructure
  - Improved separation of concerns between trace listener and logger adapter
  - Better extensibility for future logging backends

- **UI Enhancements**
  - Improved settings panel layout and organization
  - Better visual feedback for pending changes
  - Enhanced theme consistency

## Fixed

- Stack trace now properly included in UI output
- Settings reset functionality improved
- geometry rendering ratio calculations

# 1.4.0

## Added

- **Automatic Floating Window on Trace Events**
  - Floating trace log window automatically opens when trace events occur and no document is open
  - Listener monitors document open/close events to manage window visibility
  - Auto-hides when document is opened to avoid interference

- **External File Logging System**
  - Multiple log file format support:
    - **Plain Text (.log)** - Human-readable format with timestamps
    - **JSON (.json)** - Structured JSON format for parsing
    - **CLEF (.clef)** - Compact Log Event Format for analysis tools like Seq
    - **SQLite (.db)** - Database format for querying and data analysis
  - Configurable rolling intervals (daily, hourly, etc.)
  - File size limit controls
  - Shared file access support for multi-process logging

- **Log Configuration UI**
  - Settings panel for managing log output preferences
  - Enable/disable external file logging
  - Select log format and output path
  - Configure rolling intervals and file management
  - Stack trace inclusion toggle with depth control

- **Enhanced Stack Trace in Logs**
  - Stack traces now show actual caller location (method, file, line number)
  - Automatic filtering of framework and system internals
  - Configurable stack trace depth
  - Displays call chain in readable format (e.g., `MyClass.Execute:42 > Helper.Process:18`)
  - Includes Revit version and active document name in structured log context

## Changed

- **Visualization System**
  - Adjusted default axis length for better scale
  - Optimized geometry rendering pipeline

- **UI Adjustments**
  - Disabled window backdrop (mica/acrylic) to fix RichTextBox overlay issues
  - Adjusted glass frame thickness for better WinForms control integration

## Fixed

- Proper cleanup of trace listener when Revit closes
- Window theme subscription leaks on close

# 1.3.3

## Changed

- Updated manual test case: batch log

## Fixed

- `Clear` method in TraceLog - text not actually clearing and reappearing on next trace event due to `OnIdling` event handler interference
- Curve Test case - cast to `Edge` instead of `Curve`

# 1.3.2

## Added

- DockablePane visibility tracking to automatically subscribe/unsubscribe TraceLog when panel is shown/hidden
- Proper lifecycle management for TraceLog with subscription state tracking

## Changed

- Changed default `AxisLength` in `XyzVisualizationSettings` from 6 to 2 for better default visualization scale
- Changed Settings window startup location from `CenterScreen` to `CenterOwner`
- Improved TraceLog initialization and disposal flow with better state management
- Refactored TraceLog to completely unsubscribe when panel is not visible
- Enhanced theme change handling with proper dispatcher invocation
- Removed unused external event handlers (`AsyncEventHandler`, `AsyncCollectionEventHandler`, `IdlingEventHandler`)
- Updated `.csproj` repack configuration to exclude only UI assemblies

## Fixed

- Prevented multiple Settings windows from opening simultaneously by controlling `OpenSettings` command execution state
- TraceLog resource cleanup and memory management
- Trace listeners registration to ensure persistence across application lifecycle

# 1.3.1

## Fixed

- Removed `PixiEditor.ColorPicker` to avoid conflicts between `Microsoft.Xaml.Behaviors.Wpf` and other add-ins.

# 1.3.0

## Added

- Settings integration
  - Visualization options
  - Theme management options
- Suppor `Outline` object

## Fixed

- Revit 2026 context isolation
- Ensure Trace.Listeners are re-added if removed by other add-ins

# 1.2.0

## Added

- DirectContext3D visualization support for geometry rendering
- Support for visualizing multiple geometry types (Solid, Face, Curve, XYZ, CurveLoop, Outline)
- Trace support for ICollection `<object>` and IEnumerable `<CurveLoop>`
- Serilog sink for WinForms RichTextBox with theme support
- Nice3Point packages integration
- New designed icons and button icons

## Fixed

- Multiple geometries visualization
- Face normal vector visualization
- SolidVisualizationServer rendering issues
- VisualizationServerController startup
- Exception handling when retrieving bounding box of solids
- Skip solids with zero volume in bounding box calculation

## Changed

- Improved theme management and resource handling
- Enhanced TraceLog with RichTextBox using dynamic theme and max log lines
- Optimized bounding box calculations by transforming corners individually
- Streamlined buffer disposal and clearing methods
- Refactored XYZ rendering methods with updated color settings and axis length support
- Updated PolylineVisualizationServer rendering
- Improved safe server registration
- Better start/stop mechanism for Visualization server

## Performance

- Disposed unmanaged resources properly

## Documentation

- Added RenderHelper in-depth documentation
- Updated README with Acknowledgments section
- Refined geometry visualization details in README
- Streamlined table of contents and updated Python references for Revit 2025

# 1.1.1

- Listen to Console.WriteLine
- Auto start listening on revit start

# 1.1.0

- Use single DockablePanel
- Simplified codebase
- UI ehancements

# 1.0.0

Initial release. Enjoy!

# Install And First Run

## Install

1. Download `RevitDevTool-Setup.exe` from [GitHub Releases](https://github.com/trgiangv/RevitDevTool/releases).
2. Run the Inno Setup installer (not an MSI).
3. Start the target Autodesk host — **Revit** or **AutoCAD-family** (2022–2027).
4. Open the RevitDevTool or DevTools panel from the host ribbon.

The installer registers the add-in bundle under `%APPDATA%\Autodesk\ApplicationPlugins\RevitDevTool.bundle\`.

## AI Tool Integration

Point a **local** MCP v2 client (from 28 July 2026) at the MCP server that ships in the bundle — Cursor, Claude Desktop, VS Code Copilot, ChatGPT Desktop, …. Requires [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0). Cloud marketplace connectors are not included.

**MCP client config:**

```json
{
  "mcpServers": {
    "revitdevtool": {
      "type": "stdio",
      "command": "%APPDATA%/Autodesk/ApplicationPlugins/RevitDevTool.bundle/Contents/DevTools.Daemon.exe",
      "args": ["--stdio"]
    }
  }
}
```

After installation, MCP clients and test runners can discover the selected Autodesk host automatically.

## First Run

Typical workflow (applies to both Revit and AutoCAD-family hosts):

1. Open the RevitDevTool dockable panel.
2. Add script folders or assembly folders in settings.
3. Pick a discovered command.
4. Run it.
5. Inspect trace output (Monitor sink), log files, HTTP logs if configured, and optional geometry visualization (**Revit only**).

## Script Folder Rules

The shared script provider discovers script entry files by suffix:

| Script type | Entry suffix |
| --- | --- |
| CPython | `*script.py` |
| IronPython | `*_ipy_script.py` |
| C# script | `*script.csx` |
| F# script | `*script.fsx` |

Folders without a supported entry script are skipped.

## Python Backends

Use PEP 723 metadata at the top of a CPython script:

```python
# /// script
# dependencies = ["polars", "numpy"]
# ///
```

The runtime prepares dependencies before execution:

| Backend | When | Install location |
| --- | --- | --- |
| **Pixi-owned** | Default — Revit and AutoCAD-family | `%APPDATA%\RevitDevTool\pixi-env` (CPython 3.14) |
| **Host-owned + uv** | **Plant 3D only** (embedded CPython runtime — runtime callout, not a separate product) | Sidecar `%APPDATA%\RevitDevTool\uv-env\{major.minor}` |

If Pixi is blocked by enterprise policy (or uv on Plant 3D), RevitDevTool falls back to pip or pyRevit's bundled CPython when available.

IronPython scripts (`*_ipy_script.py`) use the IronPython runtime directly — no PEP 723, Pixi, or uv install step. On Revit they prefer the pyRevit engine (default **IronPython 2.7.12**) when pyRevit is installed. IronPython has **no debugger** — use logging or switch to CPython for breakpoint debugging.

## Debugging

**CPython (debugpy):** VSCode/debugpy integration when enabled.

1. Start VSCode debugger attach configuration.
2. Run the Python script from RevitDevTool.
3. Breakpoints should be hit when the debugger connects.

If debugging does not attach, check debugger port configuration, local firewall/security policy, and whether `debugpy` was installed into the active runtime.

**IronPython:** no debugger support.

**.NET add-ins and Roslyn scripts:** attach your IDE debugger to the host process (Revit.exe or acad.exe) for breakpoint debugging of compiled assemblies and C# / F# scripts.

## Observability

Three log sinks are available out of the box:

| Sink | Purpose |
|------|---------|
| **Monitor** | Trace Panel in the dockable UI — syntax highlighting, color keywords |
| **File** | Persistent logs under `%APPDATA%\RevitDevTool\{Year}\Logs\` |
| **HTTP** | Remote log streaming — see [Log Output](/docs/logging/Observability-Http) |

Revit-only geometry visualization renders transient DirectContext3D geometry — see [Geometry Visualization](/docs/logging/Visualization-Overview).

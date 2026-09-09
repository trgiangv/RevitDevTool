# Feature Overview

RevitDevTool v3.0 is a development platform for .NET-based CAD/BIM applications. It supports **Revit** and **AutoCAD-family** hosts (2022–2027) and is designed to extend to any .NET-capable host. Iterate on automation code without rebuilding, reinstalling, or restarting the host.

---

## Four Pillars

| Pillar | What it covers | Key entry points |
|--------|----------------|------------------|
| **Execution** | CPython, IronPython, C# / F# scripts, .NET assemblies | [Run Code Overview](/docs/execution/Execution-Overview), [Modern Python Scripting](/docs/execution/python/Execution-Python) |
| **Testing** | pytest bridge + MTP TestAdapter for in-host tests | [Testing Overview](/docs/testing/Testing-Overview), [pytest](/docs/testing/pytest), [NUnit](/docs/testing/NUnit), [TUnit](/docs/testing/TUnit) |
| **AI Tool Integration** | Local MCP v2 clients via `DevTools.Daemon.exe --stdio` | [MCP .NET](/docs/mcp/MCP-CSharp), [MCP Python](/docs/mcp/MCP-Python) |
| **Logging** | Log level, Monitor, File, and HTTP output + Revit geometry visualization | [Log Level](/docs/logging/Logging-Overview), [Log Output](/docs/logging/Observability-Http), [Geometry Visualization](/docs/logging/Visualization-Overview) |

---

## Claimed Hosts

RevitDevTool markets two host products. AutoCAD-family verticals (Architecture, MEP, Electrical, Mechanical, Map 3D, Civil 3D, Plant 3D, …) load the same `AcadDevTool` add-in — they are not separate product lines in this documentation.

| Host | Versions | Status |
|------|----------|--------|
| **Revit** | 2022–2027 | Primary experience — full four pillars including geometry visualization |
| **AutoCAD-family** | 2022–2027 | Full execution, testing, MCP, and logging; no geometry visualization |

> **Runtime callout — Plant 3D:** the only current host with an embedded CPython runtime. PEP 723 dependencies install via a **uv sidecar** instead of Pixi. This is a Python backend exception, not a separate RevitDevTool product.

---

## Pillar Highlights

### Execution
- CPython 3.14 via Pixi (default) with PEP 723 auto-install
- IronPython scripts and tests; on Revit, pyRevit engine preferred (default IronPython 2.7.12) — **no debugger**
- CPython debugpy + .NET IDE attach for compiled add-ins and Roslyn scripts
- C# / F# scripts via Roslyn; .NET assembly hot-reload

### Testing
- **PyTest bridge** — CPython pytest and IronPython unittest run inside the selected host
- **MTP TestAdapter** — NUnit `4.6.1` / TUnit `1.66.27` inside live hosts via `DevTools.TestAdapter` (NuGet `RevitDevTool.TestAdapter` `0.0.7`)
- `--host revit`, `--host autocad`, and other AutoCAD-family names; `--force-launch`, `--per-test-timeout`

### AI Tool Integration
- Any **local MCP v2 client** (from 28 July 2026): Cursor, Claude Desktop, VS Code Copilot, ChatGPT Desktop, …
- Daemon entry: `DevTools.Daemon.exe --stdio` (replaces legacy `MCPServer.exe`)
- Built-in discovery tools + `search_dynamic` / `invoke_dynamic` for host-registered capabilities
- Custom Python and C# toolsets in Settings → MCP

### Logging
- **Three log sinks:** Monitor (Trace Panel), file, HTTP
- Color keywords, pretty JSON, Python stack trace formatting
- **Revit-only** DirectContext3D geometry visualization (curves, faces, solids, meshes, points)

---

## What Changed From v2.x

- **Four-pillar platform:** shared `DevTools.*` libraries — execution, testing, MCP, and observability are not Revit-specific
- **Two claimed hosts:** Revit + AutoCAD-family (2022–2027); Revit 2027 adds .NET 10 target
- **Python backends:** Pixi-owned CPython 3.14 is the default; Plant 3D attaches to the host interpreter with a uv sidecar — v2's UV-only model is gone
- **Unified state:** `sys.__devtool__` replaces host-specific attributes
- **Daemon MCP server:** `DevTools.Daemon.exe --stdio` with multi-host pipe discovery
- **Testing as first-class pillar:** pytest bridge + MTP TestAdapter, not buried under execution docs

---

## Best Starting Points

- New to the tool: [Install And First Run](/docs/getting-started/Getting-Started-Install)
- Running Python: [Modern Python Scripting](/docs/execution/python/Execution-Python)
- Running compiled add-ins: [Run .NET Add-ins](/docs/execution/dotnet-assembly/Execution-Assembly)
- Testing options: [Testing Overview](/docs/testing/Testing-Overview)
- Connecting AI tools: [MCP .NET](/docs/mcp/MCP-CSharp) or [MCP Python](/docs/mcp/MCP-Python)
- Logs and geometry: [Log Level](/docs/logging/Logging-Overview)
- AutoCAD-family workflows: [AutoCAD Features](/docs/hosts/Hosts-AutoCAD)
- Upgrading from v2.x: [Migration To v3.0](/docs/getting-started/Migration-To-v3.0)

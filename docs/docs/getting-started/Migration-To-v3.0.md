# Migration To v3.0

v3.0 is a major architecture shift from the v2.x Revit-only toolset to a **development platform for .NET-based CAD/BIM applications**. It supports **Revit** and **AutoCAD-family** hosts (2022–2027), designed to extend to any .NET-capable host.

Documentation is organized around **four pillars:** Execution, Testing, AI Tool Integration, and Observability.

---

## What Changed Since v2.x

### Architecture

| v2.x | v3.0 |
|------|------|
| Revit-only codebase | Shared `DevTools.*` platform + host-specific projects |
| `RevitDevTool.sln` | `RevitDevTool.slnx` |
| `Release R25` configs | `Release.Autodesk.2025` configs |
| Revit 2022–2026 | Revit 2022–2027 + AutoCAD-family 2022–2027 |
| .NET 4.8 / .NET 8.0 | + .NET 10.0 (Revit 2027) |
| Logging + Visualization as separate concerns | **Observability** pillar — Monitor, file, HTTP sinks + Revit geometry viz |

### Python

| v2.x | v3.0 |
|------|------|
| UV-first deps | Pixi-first deps (conda-forge + PyPI) |
| `sys.__revitdevtool__` | `sys.__devtool__` (unified for all hosts) |

### New Capabilities

| Pillar | Feature | Notes |
|--------|---------|-------|
| Execution | AutoCAD-family host | Full script and assembly execution via `AcadDevTool` add-in |
| Execution | IronPython | First-class script mode; Revit prefers pyRevit engine (default IronPython 2.7.12) — **no debugger** |
| Execution | C# / F# scripts | Roslyn-based script execution |
| Testing | PyTest multi-host | `--host revit`, `--host autocad`, and other AutoCAD-family names |
| Testing | MTP TestAdapter | NUnit `4.6.1` / TUnit `1.66.27` inside live hosts via `DevTools.TestAdapter` (NuGet `RevitDevTool.TestAdapter` `0.0.7`) |
| Testing | IronPython tests | `test_*_ipy.py` with unittest semantics |
| AI Tool Integration | MCP | `DevTools.Daemon.exe --stdio` discovers running hosts |
| AI Tool Integration | MCP in-host tools | `search_dynamic` / `invoke_dynamic` for host capabilities |
| Observability | HTTP logging | Remote log streaming sink |
| Observability | Revit Command Browser | Searchable command access (Revit only) |

AutoCAD-family verticals (Civil 3D, Plant 3D, Architecture, MEP, …) load the same add-in — they are not separate marketed products. **Plant 3D** is the only host that uses a **uv sidecar** for PEP 723 (embedded CPython runtime).

---

## Since 3.0.0

Changes after the initial v3.0 release:

| Area | Current behavior |
|------|------------------|
| MCP server | `DevTools.Daemon.exe --stdio` (replaces `MCPServer.exe`) |
| MCP clients | Any local MCP v2 client (from 28 July 2026): Cursor, Claude Desktop, VS Code Copilot, ChatGPT Desktop, … |
| MCP discovery | Infrastructure tools plus **`search_dynamic`** / **`invoke_dynamic`** for host-registered capabilities |
| pytest 0.4.0 | **`--force-launch`** and **`--per-test-timeout`** options |
| Python runtime | **Pixi-owned** CPython 3.14 by default; **uv sidecar only on Plant 3D** |
| Host testing | **MTP TestAdapter** (`DevTools.TestAdapter` from NuGet `RevitDevTool.TestAdapter` `0.0.7` + Microsoft Testing Platform `2.4.0`) |
| IronPython tests | `test_*_ipy.py` with unittest semantics; Revit prefers pyRevit engine |
| Startup diagnostics | `crash_{app}_{ver}_{pid}.log` under `%APPDATA%\RevitDevTool\{Year}\Logs\` when startup fails |
| Sample layout | Lowercase **`samples/`** at repo root |

---

## Path Changes

| Old Reference | Current Location |
|---------------|-----------------|
| `RevitDevTool.sln` | `RevitDevTool.slnx` |
| `source/RevitDevTool.PythonDemo/` | `samples/PythonDemo/` |
| `source/RevitDevTool.DotnetDemo/` | `samples/CSharpDemo/` |
| `Release R25`, `Debug R25` | `Release.Autodesk.2025`, `Debug.Autodesk.2025` |
| Revit-only execution source | Shared `source/DevTools.Execution/` + host adapters |

---

## Python Package Management

v2.x docs were UV-centric. v3.0 uses Pixi for PEP 723 on every current host except Plant 3D:

- **Pixi-owned** (Revit, AutoCAD-family): PEP 723 metadata declares dependencies; Pixi resolves from conda-forge + PyPI into `%APPDATA%\RevitDevTool\pixi-env\`
- **uv sidecar (Plant 3D only):** PEP 723 installs into `%APPDATA%\RevitDevTool\uv-env\{major.minor}` via uv — never into the host prefix
- pip fallback for locked-down machines

---

## Execution Modes

| Mode | File Pattern |
|------|-------------|
| CPython 3.13+ | `*script.py` |
| IronPython | `*_ipy_script.py` |
| C# script | `*script.csx` |
| F# script | `*script.fsx` |
| .NET assembly | `.dll` with supported command types |

---

## For pytest Plugin Users

The `revitdevtool_pytest` plugin v0.3.0 replaced Revit-specific CLI options. v0.4.0 renamed a few options to match [RevitDevTool.TestAdapter](/docs/testing/NUnit).

| Old (v0.2.x) | v0.3.0 | v0.4.0 |
|---------------|-------------|-------------|
| `--revit-version` | `--host-version` | (unchanged) |
| `--revit-pipe` | `--host-pipe` | (unchanged) |
| `--revit-launch` | `--host-launch` | `--force-launch` |
| `--revit-timeout` | `--host-timeout` | `--per-test-timeout` |
| N/A | `--host-launch-timeout` | `--launch-timeout` |
| N/A | `--host` | (unchanged) |

v0.4.0 also improves host selection and test-runner options. Use the command examples in [pytest](/docs/testing/pytest).

Config in `pyproject.toml`:

```toml
[tool.pytest.ini_options]
host_name = "revit"
host_version = "2025"
force_launch = false
per_test_timeout = "60"
```

---

## Observability

The logging backend has been re-architected under the **Observability** pillar:

| v2.x | v3.0 |
|------|------|
| Serilog | ZLogger (lower allocation, faster) |
| WinForms RichTextBox | Scintilla.NET (handles large log volumes without freezing) |
| UI + file sinks only | Monitor + file + **HTTP** sinks |
| Visualization as separate module | Part of Observability — Revit-only DirectContext3D geometry |

The user experience (syntax highlighting, color keywords, JSON formatting) remains the same. Startup failure dumps are written separately — see [Log Level](/docs/logging/Logging-Overview).

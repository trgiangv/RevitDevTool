# Known Limitations

## Claimed Hosts

RevitDevTool documents **Revit** and **AutoCAD-family** as the two marketed host products. Civil 3D, Plant 3D, and other AutoCAD verticals load the same `AcadDevTool` add-in but are not promoted as separate product lines.

---

## Observability

### Visualization Is Revit-Only

The geometry visualization module (DirectContext3D) is a Revit-specific rendering API. AutoCAD-family hosts do not have an equivalent transient rendering path in RevitDevTool. Logging (Monitor, file, HTTP) works on all claimed hosts.

### Log Output

The HTTP log sink requires network configuration and a reachable endpoint. It is optional — Monitor and file sinks work without it.

---

## AutoCAD-Family Host

- **Command Browser** is Revit-only — not yet available for AutoCAD-family hosts
- **Host API stubs** are not bundled for every Autodesk version; generate `.pyi` files from the target .NET assemblies when needed
- **Geometry visualization** is not available (Revit only)

---

## Execution

### IronPython Has No Debugger

IronPython scripts (`*_ipy_script.py`) and IronPython tests (`test_*_ipy.py`) run without breakpoint debugging. On Revit, both prefer the pyRevit engine when installed (default **IronPython 2.7.12**). Use logging, print output, or convert to CPython for debugpy breakpoint debugging.

### Plant 3D Python Backend

Plant 3D is the only current host with an embedded CPython runtime. PEP 723 packages install via a **uv sidecar** to `%APPDATA%\RevitDevTool\uv-env\{major.minor}` — not via Pixi. This is a runtime backend exception, not a separate RevitDevTool product.

---

## AI Tool Integration

- **.NET 10 required** — local MCP clients spawn `DevTools.Daemon.exe --stdio` as the MCP server; without the runtime, AI tools are unavailable even when hosts are running
- **Host must load the add-in** — the MCP server discovers pipes only from running hosts with RevitDevTool loaded; a crash during add-in startup leaves no MCP pipe (see `crash_*` logs in [Troubleshooting](/docs/getting-started/Troubleshooting))
- **Local MCP v2 clients only** — any local tool that supports MCP v2 (from 28 July 2026): Cursor, Claude Desktop, VS Code Copilot, ChatGPT Desktop, …. Configure a stdio command. Cloud marketplace connectors are not included in this release.

---

## Testing

- **IronPython tests** (`test_*_ipy.py`) use unittest semantics — no pytest fixtures or PEP 723 dependency install. On Revit they use the pyRevit engine when installed (default **IronPython 2.7.12**), else IronPython 3.4.2
- **CPython vs IPy split** — one pytest invocation cannot mix two separate `conftest.py` trees; run CPython and IronPython test folders as separate pytest sessions against the same host
- **Sequential execution** — host API calls run on the main thread via `IHostContextExecutor`; tests in a session do not run in parallel inside one host instance

---

## Python Runtime

Python execution depends on local machine state:

- **Pixi-owned** (Revit and AutoCAD-family) — CPython 3.14 in `%APPDATA%\RevitDevTool\pixi-env`; requires writable AppData and reachable conda-forge/PyPI
- **Plant 3D only — host-owned + uv sidecar** — PEP 723 packages install to `%APPDATA%\RevitDevTool\uv-env\{major.minor}` matched to the host interpreter; the host prefix itself is not modified
- **Fallback** — if Pixi is blocked (or uv on Plant 3D), RevitDevTool falls back to pip or pyRevit's bundled CPython when pyRevit is installed
- If no manager or fallback is available, the CPython execution pipeline is unavailable — contact IT to whitelist Pixi (or uv on Plant 3D) or pyRevit
- **CPython debugging** requires `debugpy` and an available debug port in the active runtime

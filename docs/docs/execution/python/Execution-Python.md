# Modern Python Scripting

Use CPython scripting when you want fast automation, data extraction, computational geometry, reporting, debugger support, or package-heavy workflows inside the host.

This page covers the modern CPython path: PythonNet in-process with CAD/BIM hosts. For IronPython, C#/F# scripts, and assemblies, see [Execution Overview](/docs/execution/Execution-Overview).

![Python runtimes](/images/execution/PythonRuntimes.png)

![PEP 723 dependency resolve](/images/execution/PythonDependencyResolve.gif)

## Python runtime by host

**Claimed hosts** for CPython today: **Revit** and the **AutoCAD-family** (AutoCAD, Civil 3D, Architecture, MEP, Electrical, Mechanical, Map 3D, Plant 3D).

In the current product lineup, **only Plant 3D** ships an embedded CPython runtime. All other claimed hosts use a Pixi-owned interpreter.

### Default — Pixi-owned (Revit, AutoCAD, Civil 3D, Architecture, MEP, Electrical, Mechanical, Map 3D)

In-process CPython from Pixi. `pixi.toml` pins `python = "3.14.*"`. Environment: `%APPDATA%\RevitDevTool\pixi-env`. PEP 723 packages install through **Pixi** (conda-forge + PyPI). If Pixi cannot run, pip / pyRevit `cengines` is the fallback.

### Plant 3D only — host-owned + uv sidecar

Plant 3D is an AutoCAD-family vertical — the **only** one that embeds CPython before the add-in loads. RevitDevTool attaches pythonnet to the host `python3xx.dll` and never starts a second in-process interpreter (a Pixi 3.14 DLL would crash). PEP 723 packages install into a **uv sidecar** at `%APPDATA%\RevitDevTool\uv-env\{major.minor}` (often `3.13`), then `site.addsitedir` overlays that prefix. Pixi is not used on this path. If uv cannot run, pip matching the host minor via pyRevit `cengines` is the fallback.

See the [Plant 3D example](/docs/hosts/Hosts-AutoCAD) for the Plant-specific workflow.

## Entry File

CPython script folders are discovered through files ending with:

```text
*script.py
```

Example (repo path):

```text
samples/PythonDemo/commands/data_analysis_script.py
```

Browse samples on GitHub: [PythonDemo/commands](https://github.com/trgiangv/RevitDevTool/tree/main/samples/PythonDemo/commands)

## Dependency Metadata

Dependencies are declared with PEP 723 metadata in the script file:

```python
# /// script
# dependencies = ["polars", "numpy"]
# ///
```

The runtime parses metadata before execution and prepares the environment with **Pixi** on every claimed host except Plant 3D, which uses the **uv sidecar**.

## Host Context

### Revit

Scripts receive `__revit__` (`UIApplication`) and a shared state namespace on `sys.__devtool__` for scope-local RevitDevTool state.

### AutoCAD family

There is no `__revit__`. Use the host `Application` API instead. See [AutoCAD Features](/docs/hosts/Hosts-AutoCAD) for the AutoCAD host context pattern.

For modeless UI or background work in any host, route host API calls through the supported host execution path.

## Debugging

Python debugging uses `debugpy` and the debugger bridge. Configure VSCode attach, start the debugger, then run the script from RevitDevTool.

For the full workflow, port settings, .NET attach for scripts/assemblies, and troubleshooting, see [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

## IronPython vs CPython Conventions

These suffixes serve different purposes — do not mix them:

| Purpose | Entry suffix | Notes |
| --- | --- | --- |
| Run IronPython automation | `*_ipy_script.py` | No debugger attach — [Execution Overview](/docs/execution/Execution-Overview) |
| Test IronPython in-host | `test_*_ipy.py` | pytest routing only; same Revit engine preference — [unittest](/docs/testing/unittest) |

`*_ipy_script.py` is for commands you run from RevitDevTool. `test_*_ipy.py` is how the pytest plugin routes IronPython unittest files to the host — it is not an execution entry name.

## Examples

Current Python samples live under:

```text
samples/PythonDemo/commands/
```

Notable examples:

- `data_analysis_script.py`
- `dashboard_script.py`
- `debugpy_script.py`
- `selectionfilter_script.py`
- `visualization_curve_script.py`
- `visualization_solid_script.py`
- `visualization_xyz_script.py`
- `logging_format_script.py`
- `modeless_script.py`

## Caveats

- Package installation can be blocked by local policy, a missing `pixi` executable (or `uv` on Plant 3D), or network access.
- On Plant 3D, host-owned attach cannot load conda `Library\bin` activation or a second in-process Python layout — prefer PyPI wheels with private native deps.
- Host API calls still require valid host context.
- PythonNet behavior differs from IronPython, especially around .NET interop.

## Related

- [Attach Debugger](/docs/execution/python/Execution-PythonDebugging)
- [Execution Overview](/docs/execution/Execution-Overview)
- [Python Ecosystems](/docs/execution/python/Execution-PythonEcosystems)
- [IronPython — pyRevit Folder Compatibility](/docs/execution/ironpython/Execution-VsPyRevit)

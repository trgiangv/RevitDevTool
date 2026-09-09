# Python Ecosystems For Revit



Revit Python development has several runtime options. Choose based on the workflow you need: modern packages, direct .NET compatibility, visual programming, or team deployment.



**Claimed hosts** for RevitDevTool Python today: **Revit** and the **AutoCAD-family**.



## Quick Choice



| Need | Recommended option |
| --- | --- |
| Modern Python packages | RevitDevTool CPython |
| VSCode breakpoints | RevitDevTool CPython |
| Data analysis / ML / computational geometry | RevitDevTool CPython |
| Existing IronPython or pyRevit scripts | RevitDevTool IronPython or pyRevit |
| Visual graph workflow | Dynamo Python |
| Team ribbon deployment | pyRevit |
| Simple no-package Revit API automation | IronPython or compiled .NET |



## Runtime Comparison



| Runtime | Best for | Strengths | Tradeoffs |
| --- | --- | --- | --- |
| RevitDevTool CPython | package-heavy development | modern Python, PEP 723 dependencies, `debugpy` debugging | uses PythonNet for .NET interop |
| RevitDevTool IronPython | IronPython scripts and existing pyRevit/RevitPythonShell-style code | On Revit, execution and tests prefer the pyRevit engine (default IronPython 2.7.12); IronPython 3.4.2 fallback on Revit; bundled 3.4.2 on AutoCAD-family | **no debugger attach**; no modern CPython package ecosystem |
| pyRevit IronPython | ribbon automation and team scripts | mature extension model, direct CLR style | older Python ecosystem |
| pyRevit CPython | experimentation | CPython access inside pyRevit ecosystem | manual/runtime caveats vary by setup |
| Dynamo CPython | graph + Python workflows | visual programming, bundled packages | less suited for code-first debugging |
| Compiled .NET | production add-ins | strong typing, host-native API model, attach debugger | slower iteration than scripts |



## RevitDevTool CPython



Use this for new Python work when packages matter.



Good fit:



- model data extraction;

- dataframe analysis;

- Excel/report generation;

- geometry processing;

- research scripts;

- AI/ML experiments;

- scripts that benefit from VSCode debugging.



Dependency example:



```python

# /// script

# dependencies = [

#     "polars==1.38.1",

#     "numpy==2.4.2",

#     "openpyxl==3.1.5",

# ]

# ///

```



### Interpreter owner



| Path | Hosts | Python source |
| --- | --- | --- |
| Pixi-owned | Revit, AutoCAD, Civil 3D, and other AutoCAD verticals (not Plant 3D) | Pixi conda env at `%APPDATA%\RevitDevTool\pixi-env` (`python = "3.14.*"`) |
| Host-attached | **Plant 3D only** — AutoCAD-family exception | Host `python3xx.dll` via pythonnet — no second in-process interpreter |



### Package manager



| Path | Primary | Fallback |
| --- | --- | --- |
| Pixi-owned | Pixi (conda-forge + PyPI, search-first per PEP 723) | pip / pyRevit `cengines` |
| Plant 3D host-attached | **uv** sidecar (`%APPDATA%\RevitDevTool\uv-env\{major.minor}`) | pip matching host minor via pyRevit `cengines` |



Pixi is the default package manager on Revit and AutoCAD-family. Plant 3D is the only AutoCAD-family vertical that embeds CPython before the add-in loads, so uv is used there and Pixi is not attempted.

For the full Plant 3D path, see the [Plant 3D example](/docs/hosts/Hosts-AutoCAD) and [Modern Python Scripting](/docs/execution/python/Execution-Python).



## IronPython



Use IronPython when IronPython/.NET behavior or compatibility is more important than modern CPython packages.



Good fit:



- existing pyRevit/RevitPythonShell-style scripts;

- simple Revit API automation;

- workflows that do not need NumPy, pandas, Polars, Shapely, or other CPython packages.



On **Revit**, IronPython **scripts** (`*_ipy_script.py`) and IronPython **tests** (`test_*_ipy.py`) both prefer the pyRevit engine when it is installed — pyRevit's current default is still **IronPython 2.7.12**, which matches most pyRevit tool work. If pyRevit is not installed, RevitDevTool falls back to IronPython 3.4.2.



On **AutoCAD-family** hosts, RevitDevTool uses bundled **IronPython 3.4.2** (no pyRevit).



IronPython has **no debugger attach** in RevitDevTool. Use CPython when you need VSCode breakpoints.



The two filename conventions are for different purposes (execute vs pytest routing); they share the same Revit engine preference.



## Dynamo Python



Use Dynamo Python when the surrounding workflow is a Dynamo graph.



Good fit:



- visual programming;

- graph-controlled inputs/outputs;

- Dynamo package ecosystem;

- workflows used by designers who prefer node graphs.



## pyRevit



Use pyRevit when the main goal is distributing buttons and automations to users.



Good fit:



- team ribbon tools;

- established pyRevit extensions;

- reusable buttons;

- end-user automation workflows.



RevitDevTool can still be useful beside pyRevit for development, package experiments, and IronPython/CPython compatibility workflows. RevitDevTool also uses pyRevit `cengines` as a pip fallback when Pixi cannot run (or uv on Plant 3D).



## Interface And .NET Interop Notes



CPython uses PythonNet to talk to .NET APIs. This gives access to modern Python packages, but .NET interop is not identical to IronPython.



Watch for:



- .NET interface implementation differences;

- overload resolution;

- extension methods;

- `out`/`ref` style APIs;

- dynamic object behavior inside the debugger.



For pure Revit API automation with heavy .NET interop and no Python packages, IronPython or compiled .NET may be simpler. For package-heavy work, CPython is usually the better choice.



## Related Examples



- [Data Analysis](/docs/examples/Examples-DataAnalysis)

- [Dashboard](/docs/examples/Examples-Dashboard)

- [Visualization Curves](/docs/examples/Examples-VisualizationCurves)

- [Attach Debugger](/docs/execution/python/Execution-PythonDebugging)

- [IronPython — pyRevit Folder Compatibility](/docs/execution/ironpython/Execution-VsPyRevit)


import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

# Attach Debugger

This page covers debugging for **CPython** and **IronPython** (attach from VS Code, PyCharm or any IDE that speaks the debug adapter protocol) and **.NET attach** (C# `.csx`, F# `.fsx`, compiled `.dll`).

## Quick Reference

| Runtime | Attach method | Notes |
| --- | --- | --- |
| CPython `*script.py` | Attach to a localhost port (preferred **5678**) | Attach **before** running the script |
| IronPython `*_ipy_script.py` | Attach to a localhost port (preferred **4567**) | Works on the pyRevit engine and on IronPython 3.4.2; attach **before** running |
| C# `*script.csx` | Attach to host process | Debug + portable PDB binds to original `.csx` |
| F# `*script.fsx` | Attach to host process | FSI `--debug+` maps back to original `.fsx` |
| .NET `.dll` | Attach to host process | Standard compiled command debugging |

---

## CPython — debugpy

Python debugging lets you attach your IDE (VS Code, PyCharm, or any client of the same protocol) to a Python script running inside RevitDevTool. Use it when print debugging is too slow or when a bug only appears with real host API objects.

<SequenceFlow
  participants={[
    { id: 'ide', title: 'Your IDE', subtitle: 'VS Code or PyCharm' },
    { id: 'host', title: 'Revit / AutoCAD', subtitle: 'RevitDevTool + your script' },
  ]}
  steps={[
    { note: 'The host listens on a localhost port', over: ['ide', 'host'], detail: 'Each host instance listens on its own port when Python starts' },
    { from: 'ide', to: 'host', text: 'Attach', detail: 'Start the attach configuration in your IDE' },
    { note: 'You run the script from RevitDevTool', over: ['ide', 'host'], detail: 'Run the command from the RevitDevTool panel' },
    { from: 'host', to: 'ide', text: 'Stopped at breakpoint', back: true, detail: 'Execution pauses at your breakpoint and the host UI waits' },
    { from: 'ide', to: 'host', text: 'Inspect, step, continue', detail: 'Read variables, step through the code, then continue' },
    { from: 'host', to: 'ide', text: 'Script finished', back: true, detail: 'The script ends and its output appears in the Trace panel' },
  ]}
/>

### Quick Start

1. Open the host with RevitDevTool loaded.
2. Open your script folder in your IDE.
3. Add an attach configuration (see below).
4. Set breakpoints in your Python script.
5. Start the attach configuration.
6. Run the script from RevitDevTool.
7. Inspect variables when execution pauses.

:::tip Debug port
CPython prefers port **5678** and IronPython prefers **4567**. Each host instance, Revit or AutoCAD, uses its preferred port when it is free; otherwise the OS assigns a free port. Set your attach configuration to the port shown in the RevitDevTool panel for that instance.
:::

### Attach configuration

<Tabs groupId="python-ide">
<TabItem value="vscode" label="VS Code" default>

Create or update `.vscode/launch.json`:

```json title=".vscode/launch.json"
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Attach RevitDevTool Python",
      "type": "debugpy",
      "request": "attach",
      "connect": {
        "host": "localhost",
        "port": 5678
      },
      "justMyCode": false
    }
  ]
}
```

</TabItem>
<TabItem value="pycharm" label="PyCharm (JetBrains)">

Add a shared run configuration in the `.run` folder of your project. The repository ships this one:

```xml title=".run/Attach.run.xml"
<component name="ProjectRunConfigurationManager">
  <configuration default="false" name="Attach Host: Python" type="PythonDapAttachConfiguration">
    <option name="localRoot" value="$PROJECT_DIR$" />
    <option name="remoteAddress" value="localhost:5678" />
    <option name="remoteRoot" value="$PROJECT_DIR$" />
    <method v="2" />
  </configuration>
</component>
```

`localRoot` and `remoteRoot` both point at the project folder, so breakpoints map to the files on disk.

</TabItem>
</Tabs>

Change the port (`5678` here) to the one your instance uses.

### Sample Script

[`debugpy_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/debugpy_script.py)

You can also debug normal scripts such as:

- [`data_analysis_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/data_analysis_script.py)
- [`visualization_curve_script.py`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/PythonDemo/commands/visualization_curve_script.py)

### Debugging A Revit Collector

```python
from Autodesk.Revit import DB

doc = __revit__.ActiveUIDocument.Document
walls = DB.FilteredElementCollector(doc).OfClass(DB.Wall).ToElements()

for wall in walls:
    curve = wall.Location.Curve
    length = curve.Length
    print(f"Wall {wall.Id}: {length:.2f}")
```

Useful breakpoints:

- before `FilteredElementCollector`;
- inside the wall loop;
- before calculations using geometry or parameters.

Useful values to inspect:

- `doc.Title`;
- `len(walls)`;
- `wall.Id`;
- `wall.Name`;
- `wall.Location`;
- `curve.Length`;
- parameters returned by `wall.get_Parameter(...)`.

### Debugging Patterns

#### Element Collection

Pause after a collector runs and inspect the count before processing the elements.

```python
elements = DB.FilteredElementCollector(doc).OfClass(DB.FamilyInstance).ToElements()
print(f"Found {len(elements)} instances")
```

#### Parameter Access

Pause after retrieving a parameter so you can check whether it exists before calling `AsString()`, `AsDouble()`, or `AsInteger()`.

```python
param = wall.get_Parameter(DB.BuiltInParameter.WALL_USER_HEIGHT_PARAM)
height = param.AsDouble() if param else 0
```

#### Selection

Use breakpoints after user selection to inspect the selected reference and element.

```python
ref = uidoc.Selection.PickObject(UI.Selection.ObjectType.Element)
element = doc.GetElement(ref)
```

### CPython Common Problems

| Symptom | Check |
| --- | --- |
| Breakpoint is never hit | your IDE is attached before running the script |
| IDE cannot connect | the port in your configuration matches the one shown in the RevitDevTool panel for that instance |
| `debugpy` import fails | Python dependencies were prepared successfully |
| Debugger attaches but file is not mapped | open the same script folder in your IDE |
| Script blocks waiting | a modal host selection/dialog may be active |
| Host freezes while paused | this is expected; continue/step from your IDE |

---

## IronPython — attach

IronPython scripts (`*_ipy_script.py`) can be debugged the same way as CPython, on the pyRevit engine and on IronPython 3.4.2. Use the same attach configuration as above, with the IronPython port: preferred **4567**.

The steps are the same as for CPython: start the attach configuration, set breakpoints, then run the script from RevitDevTool. This also covers IronPython tests (`test_*_ipy.py`): the debugger attaches to the port, so whichever engine runs your file stops at its breakpoints.

:::tip One port per runtime and instance
CPython prefers **5678** and IronPython **4567**. If the preferred port is taken, the OS assigns a free one, so use the port shown in the RevitDevTool panel.
:::

On **Revit**, IronPython execution prefers the pyRevit engine, else IronPython 3.4.2; **AutoCAD-family** hosts use bundled **IronPython 3.4.2**. A breakpoint on the very first line is missed unless your IDE is already attached. The first session downloads the debugger, so it needs the network once, like the Pixi and uv setup.

---

## .NET Attach — C# Scripts, F# Scripts, Assemblies

C# `.csx`, F# `.fsx`, and compiled `.dll` commands all run inside the host process. Attach your .NET debugger to that process — not to a separate debug port.

### Visual Studio

1. Build or open the script/assembly project (or open the script folder).
2. Set breakpoints in `.csx`, `.fsx`, or your compiled source.
3. Start the host (`Revit.exe`, `acad.exe`, …) with RevitDevTool loaded.
4. **Debug → Attach to Process…** and select the host.
5. Run the command from RevitDevTool.

Breakpoints in C# and F# scripts bind to the original `.csx` and `.fsx` files on disk.

### VS Code (C# Dev Kit)

1. Open the script or project folder.
2. Set breakpoints.
3. Use **Run and Debug → Attach to Process** and pick the host PID.
4. Run the command from RevitDevTool.

Ionide can edit F# scripts; attach still targets the host process the same way.

### .NET Attach Tips

- Attach **before** running the command when you need to break on startup logic.
- Keep script files at the same paths while debugging; moving them breaks the link to your breakpoints.
- On Revit 2022–2024 (.NET Framework), loaded assemblies cannot be unloaded, so restart the host to release one. Breakpoints still work — see [.NET Assembly](/docs/execution/dotnet-assembly/Execution-Assembly).

:::warning The host freezes while paused
While paused at a breakpoint, the host UI is frozen until you continue. This is the same as with CPython `debugpy`.
:::

---

## Related

- [Modern Python Scripting](/docs/execution/python/Execution-Python)
- [C# Scripts](/docs/execution/csharp-script/Execution-CSharp)
- [F# Scripts](/docs/execution/fsharp-script/Execution-FSharp)
- [.NET Assembly](/docs/execution/dotnet-assembly/Execution-Assembly)
- [Execution Overview](/docs/execution/Execution-Overview)
- [Troubleshooting](/docs/getting-started/Troubleshooting)

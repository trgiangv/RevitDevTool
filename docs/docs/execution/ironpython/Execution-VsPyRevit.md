# IronPython - pyRevit Engine

RevitDevTool supports running Python scripts through the **pyRevit IronPython engine**. Any script whose filename matches `*_ipy_script.py` can be selected and executed as an IronPython/pyRevit command.

## Script recognition

Use the `_ipy_script.py` suffix for any Python script that should run with IronPython or the pyRevit engine:

```text
collect_walls_ipy_script.py
export_schedule_ipy_script.py
```

The suffix is the important part. The script can remain in the folder structure already used by your pyRevit work. RevitDevTool does not require the script to be converted into a different project layout.

## pyRevit compatibility

Folder compatibility means that RevitDevTool understands the conventions commonly used by pyRevit scripts, including how scripts locate supporting libraries and how relative paths are resolved from the script or command folder.

| Compatibility area | Behavior |
| --- | --- |
| Script layout | Existing pyRevit-oriented folders can be used without restructuring |
| Supporting libraries | Python modules located beside the script or in the expected library paths remain importable |
| Relative paths | Paths used by the script continue to resolve relative to its command context where supported |
| Command discovery | `*_ipy_script.py` identifies the file as an IronPython execution command |

This allows existing pyRevit-style scripts to be used as RevitDevTool commands while preserving their organization and library assumptions as far as the host environment permits.

## Engine behavior

On Revit, RevitDevTool uses the installed pyRevit IronPython engine when pyRevit is available (current default **IronPython 2.7.12**). If pyRevit is not installed, RevitDevTool uses its IronPython 3.4.2 fallback.

AutoCAD-family hosts use bundled **IronPython 3.4.2**, because pyRevit is a Revit-only engine. For that independent runtime, see [IronPython Standalone](/docs/execution/ironpython/Execution-IronPythonStandalone).

## Example command

```python
from Autodesk.Revit import DB

doc = __revit__.ActiveUIDocument.Document
walls = DB.FilteredElementCollector(doc).OfClass(DB.Wall).ToElements()
print(f"Found {len(walls)} walls")
```

Save the file with the `_ipy_script.py` suffix, select its folder in RevitDevTool, and run the discovered command.

## Limitations

- IronPython does not support the CPython package workflow or PEP 723 dependencies.
- IronPython has no `debugpy` or debugger attach path in RevitDevTool.
- Host API access still follows the active document, transaction, and document-lock rules.
- Use [Python Dependencies](/docs/execution/Python-Dependencies) and [Attach Debugger](/docs/execution/python/Execution-PythonDebugging) when the workflow requires modern packages or breakpoints.

## Related

- [IronPython Standalone](/docs/execution/ironpython/Execution-IronPythonStandalone)
- [Python Ecosystems For Revit](/docs/execution/python/Execution-PythonEcosystems)

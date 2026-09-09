# IronPython Standalone

Standalone IronPython runs without pyRevit. Choose it when you need a self-contained IronPython runtime, are working in an AutoCAD-family host, or do not want the script to depend on pyRevit's extension and engine setup.

## Runtime

| Host | Standalone runtime | Notes |
| --- | --- | --- |
| Revit | IronPython 3.4.2 fallback | RevitDevTool prefers pyRevit's IronPython 2.7.12 when pyRevit is installed |
| AutoCAD-family | Bundled IronPython 3.4.2 | pyRevit is not available in these hosts |

Standalone IronPython executes inside the host process and uses the host's .NET API assemblies. It is suitable for direct API calls, but it does not provide the modern CPython package ecosystem.

## When to choose Standalone

- pyRevit is not installed or should not be a prerequisite;
- the script must run consistently across AutoCAD-family hosts;
- the workflow needs IronPython/.NET compatibility but not CPython packages;
- you want a small script runtime with no extension deployment model.

For new package-heavy Python work, choose [Modern Python Scripting](/docs/execution/python/Execution-Python) instead.

## Script conventions

Use the IronPython script suffix recognized by the command discovery rules:

```text
commands/my_command_ipy_script.py
```

The filename controls discovery; it does not turn the script into CPython. Use the `_ipy_script.py` suffix for commands.

## Limitations

- No `debugpy` attach is available for IronPython.
- Modern CPython-only packages such as NumPy, pandas, and Polars are not supported by this runtime.
- Python 2 and Python 3 syntax compatibility depends on the selected engine; test legacy pyRevit code before migrating it.
- Host API calls still require the correct active document, selection, transaction, and document-lock context.

If you need breakpoints, migrate the workflow to CPython and use [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

## Related

- [IronPython With pyRevit](/docs/execution/ironpython/Execution-VsPyRevit)
- [Python Ecosystems For Revit](/docs/execution/python/Execution-PythonEcosystems)
- [Modern Python Scripting](/docs/execution/python/Execution-Python)

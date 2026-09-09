# Python Dependencies

Declare Python dependencies next to the script with **PEP 723** metadata. RevitDevTool reads that metadata, prepares the supported Python environment, and then runs the script with its dependencies available.

## Inline metadata

```python
# /// script
# dependencies = [
#     "polars==1.38.1",
#     "numpy==2.4.2",
#     "openpyxl==3.1.5",
# ]
# ///
```

The metadata is part of the script, so it can be committed, reviewed, and reproduced without a separate environment file.

## Package sources

| Host path | Primary source | Fallback or exception |
| --- | --- | --- |
| Revit and most AutoCAD-family hosts | Pixi with conda-forge and PyPI | pip or pyRevit `cengines` |
| Plant 3D | uv sidecar environment | pip matching the host Python minor version |

Plant 3D is different because it embeds CPython before RevitDevTool loads. RevitDevTool attaches to that interpreter and uses a uv sidecar; it does not start a second in-process interpreter.

For runtime selection and host differences, see [Python Ecosystems For Revit](/docs/execution/python/Execution-PythonEcosystems).

## Versioning guidance

- Pin versions for production scripts and examples.
- Use compatible versions for the Python runtime and host architecture.
- Prefer packages with wheels available for the target Python version.
- Keep dependency declarations small; remove unused packages.
- Treat package upgrades as code changes and test against the target Autodesk host.

Unpinned ranges are useful while exploring, but can produce different results when the package index changes.

## Common workflows

### Data analysis

Use packages such as Polars, NumPy, and OpenPyXL for model extraction, analysis, and reporting:

```python
# /// script
# dependencies = ["polars", "openpyxl"]
# ///

import polars as pl
```

### Geometry and research

Add computational-geometry or scientific packages when the workflow needs them, then keep Revit API interaction at the boundary of the script. This makes package failures easier to separate from host API failures.

### Debugging dependency code

Prepare dependencies before starting the debugger. For CPython breakpoints, attach VS Code to the `debugpy` port shown by RevitDevTool, then run the script. See [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

## Troubleshooting

| Symptom | Check |
| --- | --- |
| A package cannot be resolved | Check the package name, version, network access, and whether a wheel exists for the target Python version |
| Import fails after a successful install | Confirm the import name differs from the distribution name and that the script uses the prepared environment |
| Plant 3D installation behaves differently | Use the uv sidecar path; Plant 3D does not use the Pixi-owned interpreter path |
| A package conflicts with host behavior | Try a compatible version and avoid packages that replace assemblies or native libraries already loaded by the host |
| Debugger cannot import the package | Prepare dependencies first, then attach to the active CPython debug port |

## Related

- [Modern Python Scripting](/docs/execution/python/Execution-Python)
- [Python Ecosystems For Revit](/docs/execution/python/Execution-PythonEcosystems)
- [Attach Debugger](/docs/execution/python/Execution-PythonDebugging)
- [NuGet Dependencies](/docs/execution/NuGet-Dependencies)

# Python Stub Generation

Python stubs are `.pyi` files generated from .NET assemblies. They give Python tools a static view of CLR namespaces, classes, methods, properties, and overloads so autocomplete, navigation, and type checking work even when the runtime API is exposed dynamically. Revit API assemblies are one use case; the feature is not limited to Revit.

## Use This When

- VS Code/Pylance, Pyright, or BasedPyright cannot infer the types exposed by a .NET assembly;
- you want better autocomplete for Revit, AutoCAD, or another .NET API;
- you are writing larger Python scripts and need safer navigation;
- you want PyCharm or another Python IDE to understand a CLR API;
- you want type hints for APIs that Python normally sees dynamically.

## What Stubs Help With

| Need | How stubs help |
| --- | --- |
| Discover .NET API members | IDE autocomplete can list known members from the generated stubs |
| Reduce typo errors | A type checker can flag missing names and incompatible arguments earlier |
| Read unfamiliar APIs | signatures are easier to inspect |
| Navigate large scripts | type hints improve jump-to-definition behavior |

## Supported Python Tools

The generated files are standard Python stubs, not a RevitDevTool-specific editor format. They can be consumed by tools that understand `.pyi` files, including:

| Tool | Typical use |
| --- | --- |
| VS Code + Pylance | autocomplete, navigation, and inline diagnostics |
| Pyright | command-line or editor type checking |
| BasedPyright | stricter Pyright-compatible analysis |
| PyCharm | indexing, completion, and inspections |
| Other Python IDEs and type checkers | any tool that supports standard `.pyi` stubs |

Point the IDE or type checker at the directory containing the generated stubs. Keep that directory in the development environment only; it is not required for the script to execute.

## Open The Stub Builder

Open the **StubBuilder** command where it is available in the RevitDevTool UI. Select the .NET assemblies that define the API you want to author against and choose an output directory for the `.pyi` files. The input can be Autodesk assemblies or any other compatible .NET assembly.

The generated stubs are consumed by the Python tooling listed above. The host used to generate them does not change the standard `.pyi` format.

## Practical Guidance

Stub generation is an IDE productivity feature. It does not change how a script runs inside RevitDevTool.

For runtime behavior and host-specific execution, start with:

- [Modern Python Scripting](/docs/execution/python/Execution-Python)
- [Python Ecosystems](/docs/execution/python/Execution-PythonEcosystems)
- [Examples Overview](/docs/examples/Examples-Overview)

## Caveats

- Generated stubs can lag behind the exact API version you use.
- Some dynamic PythonNet behavior cannot be represented perfectly as static type hints.
- Stubs describe the assembly surface available when they were generated; regenerate them when the assembly version changes.
- If a stub disagrees with runtime behavior, trust the runtime and the documentation for the source .NET assembly.

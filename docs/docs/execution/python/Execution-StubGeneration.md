import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

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

## Choose your editor

The generated files are standard `.pyi` stubs, so any editor that reads them works. Point it at the output folder, which holds one folder per namespace (`Autodesk`, `System`, and so on). Keep that folder in your development environment only; scripts do not need it to run.

| Editor | Type checker | Use it when |
| --- | --- | --- |
| VS Code | Pylance | You use Microsoft's VS Code. This is the shortest setup |
| VS Code forks (Devin, Trae, Kiro and others) or Zed | BasedPyright | The editor cannot install Pylance. BasedPyright is a free Pyright fork that works there |
| PyCharm | Built-in | You want PyCharm's refactoring and inspections |
| Anything else | Pyright or BasedPyright on the command line | You also want type checks in CI |

<Tabs groupId="stub-editor">
<TabItem value="vscode" label="VS Code" default>

Install the Python and Pylance extensions, then add the stub folder to `.vscode/settings.json`:

```json title=".vscode/settings.json"
{
  "python.analysis.extraPaths": ["C:/Users/you/AppData/Roaming/RevitDevTool/2025/Stubs"]
}
```

</TabItem>
<TabItem value="basedpyright" label="BasedPyright (forks, Zed)">

Install the BasedPyright extension (from Open VSX in forks) or enable it in Zed, then add a `pyrightconfig.json` beside your scripts:

```json title="pyrightconfig.json"
{
  "extraPaths": ["C:/Users/you/AppData/Roaming/RevitDevTool/2025/Stubs"]
}
```

The same file works for the `pyright` and `basedpyright` command-line tools.

</TabItem>
<TabItem value="pycharm" label="PyCharm">

Open **Settings → Project → Python Interpreter**, choose **Show All** and add the stub folder under **Interpreter Paths**. PyCharm then indexes the stubs for completion and inspections.

</TabItem>
</Tabs>

Use the folder that matches the host year you generated stubs for.
## Open The Stub Builder

Open the **StubBuilder** command where it is available in the RevitDevTool UI. Select the .NET assemblies that define the API you want to author against and choose an output directory for the `.pyi` files. The input can be Autodesk assemblies or any other compatible .NET assembly.

![Stubs Builder listing the loaded assemblies](/images/features/Feature_StubBuilder.png)

1. Search the list by assembly name, then select the assemblies to include. Shift+click and Ctrl+click select several rows.
2. **Select All** and **Select None** apply to the full list. **Hide unselected** shows only what you picked.
3. Check the **Output Path**. By default it is `%APPDATA%\RevitDevTool\<year>\Stubs`; use the **...** button to choose another folder.
4. Choose **Generate**.

Assemblies loaded in memory with no file on disk show `(no disk path)`. They can still be selected: Generate writes stubs for them, plus a reconstructed DLL for each into the output folder. These DLLs are rebuilt from metadata, so they are good for inspecting the API in a decompiler but are not byte-for-byte copies of the originals.

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
- A reconstructed DLL can omit resources or metadata that cannot be rebuilt. Use it to inspect the API, not as a replacement for the original assembly.

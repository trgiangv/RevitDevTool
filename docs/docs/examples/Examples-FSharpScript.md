# Example: F# Script Demo

**Scripts:**

- [`main_script.fsx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/FSharpScriptDemo/main_script.fsx)
- [`selection_script.fsx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/FSharpScriptDemo/selection_script.fsx)

Use this example when you want to try F# scripting inside Revit.

## What It Shows

- referencing Revit API assemblies;
- referencing NuGet packages from a script;
- loading local F# modules;
- creating an `IExternalCommand`;
- selecting a Revit element;
- printing element metadata;
- trying computational geometry packages such as `geometry3Sharp` and `NetTopologySuite`.

## Run

1. Open Revit 2025 or adjust the `#r` paths for your installed Revit version.
2. Add `samples/FSharpScriptDemo/` as a script folder.
3. Run `main_script.fsx`.
4. Select an element when prompted.
5. Review the trace panel output.

First run may take longer because NuGet packages need to be resolved.

## What To Edit

Common edits:

- Change Revit API reference paths for another Revit version.
- Add F# helper code under `modules/`.
- Try a smaller set of NuGet packages while debugging.
- Replace selection with a collector when you do not need user input.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Revit API reference not found | update `#r` paths |
| NuGet package fails | check package name/version and network access |
| selection cancels | ESC/cancel is expected to stop the command |
| command not discovered | file name must end with `script.fsx` |

## Related

- [F# Scripts](/docs/execution/fsharp-script/Execution-FSharp)
- [PEP 440 Versions](/docs/examples/Examples-Pep440Versions)

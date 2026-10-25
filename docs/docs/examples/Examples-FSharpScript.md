# Example: F# Script Demo

**Scripts:**

- [`main_script.fsx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/FSharpScriptDemo/main_script.fsx)
- [`selection_script.fsx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/FSharpScriptDemo/selection_script.fsx)

Use this example when you want to try F# scripting inside Revit.

<LogoRow items={['fsharp', 'nuget', 'revit']} />

## The script

`main_script.fsx` references NuGet packages, loads a local module, then asks you to pick an element and prints its metadata. This is the start of the file; the repository version continues with geometry examples using `geometry3Sharp` and `NetTopologySuite`:

```fsharp title="main_script.fsx"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPIUI.dll"
#r "nuget: Newtonsoft.Json"
#r "nuget: geometry3Sharp"
#r "nuget: NetTopologySuite"

#load "./modules/Processor.fsx"

open Autodesk.Revit.Attributes
open Autodesk.Revit.UI
open Autodesk.Revit.UI.Selection
open Demo
open Newtonsoft.Json
open System

[<Transaction(TransactionMode.Manual)>]
type ShowMessageCommand() =
    interface IExternalCommand with
        member _.Execute(commandData, _message, _elements) =
            let text = Processor.getDialogText commandData
            TaskDialog.Show("F# Script", text) |> ignore

            let uiDoc = commandData.Application.ActiveUIDocument
            let selectRef = uiDoc.Selection.PickObject(ObjectType.Element)
            let element = uiDoc.Document.GetElement(selectRef.ElementId)

            Console.WriteLine(element.GetType())
            Console.WriteLine(element.Name)
            Console.WriteLine(element.Category)

            let o = {| X = 2; Y = "Hello" |}
            Console.WriteLine $"%s{JsonConvert.SerializeObject o}"

            Result.Succeeded
```

:::tip Packages are resolved for you
The three `#r "nuget: …"` lines are all it takes. The first run is slower while packages restore; later runs reuse the cache. See [NuGet Dependencies](/docs/execution/NuGet-Dependencies).
:::

The complete scripts are in the repository: [`main_script.fsx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/FSharpScriptDemo/main_script.fsx) and [`selection_script.fsx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/FSharpScriptDemo/selection_script.fsx).

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
2. Add the [`FSharpScriptDemo`](https://github.com/trgiangv/RevitDevTool/tree/main/samples/FSharpScriptDemo) folder as a script folder.
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
- [NuGet Dependencies](/docs/execution/NuGet-Dependencies)

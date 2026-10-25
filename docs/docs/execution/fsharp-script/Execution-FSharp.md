# F# Scripts

Use F# scripts for quick experiments when you prefer F# syntax and interactive-style scripting.

Entry suffix:

```text
*script.fsx
```

## Example

Pick an element and trace its bounding box. In Revit the traced box is drawn in the active view:

```fsharp title="bbox_script.fsx"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPIUI.dll"

open System.Diagnostics
open Autodesk.Revit.Attributes
open Autodesk.Revit.UI
open Autodesk.Revit.UI.Selection

[<Transaction(TransactionMode.Manual)>]
type BoundingBoxCommand() =
    interface IExternalCommand with
        member _.Execute(commandData, _message, _elements) =
            let uiDoc = commandData.Application.ActiveUIDocument
            let reference = uiDoc.Selection.PickObject(ObjectType.Element)
            let element = uiDoc.Document.GetElement(reference.ElementId)

            Trace.Write(element.get_BoundingBox(null))
            Result.Succeeded
```

Fuller scripts are in the repo: [`selection_script.fsx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/FSharpScriptDemo/selection_script.fsx) for the example above with more tracing, and [`main_script.fsx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/FSharpScriptDemo/main_script.fsx) for scripts that use NuGet packages and `#load`.

## NuGet References

Declare package references with F# `#r` directives:

```fsharp
#r "nuget: FSharp.Data, 6.4.0"
#r "nuget: Newtonsoft.Json"
#load "Helpers.fsx"
```

:::tip Package resolution
Scripts resolve NuGet packages for you, so an `.fsx` needs no project file. Versioning, conflicts with host assemblies and restore problems are covered in [NuGet Dependencies](/docs/execution/NuGet-Dependencies).
:::

`#r "nuget: …"` restores packages automatically into:

```text
%APPDATA%\RevitDevTool\nuget
```

Errors and breakpoints point back to your original `.fsx` files.

## Host Version Symbols

F# scripts receive the same host-year symbols as C# scripts (`REVIT`, `AUTOCAD`, `{HOST}{year}`, `{HOST}{year}_OR_GREATER`).

For example, one script can handle an API that changed between Revit versions, and tell Revit from AutoCAD:

```fsharp title="version_script.fsx"
#if REVIT2024_OR_GREATER
Trace.WriteLine(element.Id.Value)          // ElementId.Value is a long since Revit 2024
#else
Trace.WriteLine(element.Id.IntegerValue)   // older versions use an int
#endif

#if REVIT
Trace.WriteLine("Running in Revit")
#elif AUTOCAD
Trace.WriteLine("Running in AutoCAD")
#endif
```

The symbols are chosen from the host and year that run the script, so the same file works on every supported version.

## Debugging

Attach the debugger to the host process, set breakpoints in your `.fsx` files, then run from RevitDevTool.

For attach settings shared with C# scripts and assemblies, see [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

## Editing

For a better VSCode editing experience, install [Ionide for F#](https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp).

## Related

- [Execution Overview](/docs/execution/Execution-Overview)
- [Attach Debugger](/docs/execution/python/Execution-PythonDebugging)
- [C# Scripts](/docs/execution/csharp-script/Execution-CSharp)
- [.NET Assembly](/docs/execution/dotnet-assembly/Execution-Assembly)

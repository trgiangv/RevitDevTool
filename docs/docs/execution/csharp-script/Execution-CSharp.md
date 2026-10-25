# C# Scripts

Use C# scripts when you want a short typed snippet without creating a full add-in project.

Entry suffix:

```text
*script.csx
```

## Example

Pick an element and trace its bounding box. In Revit the traced box is drawn in the active view:

```csharp title="bbox_script.csx"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPIUI.dll"

using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

[Transaction(TransactionMode.Manual)]
public class BoundingBoxCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uidoc = commandData.Application.ActiveUIDocument;
        var reference = uidoc.Selection.PickObject(ObjectType.Element);
        var element = uidoc.Document.GetElement(reference);

        Trace.Write(element.get_BoundingBox(null));
        return Result.Succeeded;
    }
}
```

A fuller script with a shared helper and a task dialog is in the repo: [`main_script.csx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/CSharpScriptDemo/main_script.csx).

## NuGet References

Declare package references with Roslyn `#r` directives:

```csharp
#r "nuget: Newtonsoft.Json, 13.0.3"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#load "./Helpers/DocumentHelper.csx"
```

`#r "nuget: …"` restores packages automatically into `%APPDATA%\RevitDevTool\nuget`. Paths to host API assemblies are adjusted for the running host.

:::tip Package resolution
Scripts resolve NuGet packages for you, so a `.csx` needs no project file. Versioning, conflicts with host assemblies and restore problems are covered in [NuGet Dependencies](/docs/execution/NuGet-Dependencies).
:::

`#load` pulls additional `.csx` files into the script graph. Each file can declare its own `#r` and `#load` directives.

## Host Version Symbols

Scripts receive the same preprocessor symbols as host add-in projects (`REVIT2025`, `REVIT2025_OR_GREATER`, `AUTOCAD2026`, and so on). Use `#if REVIT2025_OR_GREATER` in `.csx` the same way you would in a compiled add-in.

For example, one script can handle an API that changed between Revit versions, and tell Revit from AutoCAD:

```csharp title="version_script.csx"
#if REVIT2024_OR_GREATER
Trace.WriteLine(element.Id.Value);          // ElementId.Value is a long since Revit 2024
#else
Trace.WriteLine(element.Id.IntegerValue);   // older versions use an int
#endif

#if REVIT
Trace.WriteLine("Running in Revit");
#elif AUTOCAD
Trace.WriteLine("Running in AutoCAD");
#endif
```

The symbols are chosen from the host and year that run the script, so the same file works on every supported version.

## Debugging

An attached debugger binds breakpoints directly to your original `.csx` files.

Workflow:

1. Open the script folder in Visual Studio or VS Code (C# Dev Kit recommended).
2. Set breakpoints in your `.csx` files.
3. Attach the debugger to the running host process (`Revit.exe`, `acad.exe`, …).
4. Run the script from RevitDevTool.

For attach settings shared with F# scripts and assemblies, see [Attach Debugger](/docs/execution/python/Execution-PythonDebugging).

## Samples

- [`CSharpScriptDemo`](https://github.com/trgiangv/RevitDevTool/tree/main/samples/CSharpScriptDemo): Revit scripts, including a helper loaded with `#load`
- [`AcadCSharpDemo`](https://github.com/trgiangv/RevitDevTool/tree/main/samples/AcadCSharpDemo): AutoCAD-family commands

## Related

- [Execution Overview](/docs/execution/Execution-Overview)
- [Attach Debugger](/docs/execution/python/Execution-PythonDebugging)
- [F# Scripts](/docs/execution/fsharp-script/Execution-FSharp)
- [.NET Assembly](/docs/execution/dotnet-assembly/Execution-Assembly)

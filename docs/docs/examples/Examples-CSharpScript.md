# Example: C# Script Demo

**Scripts:** [`main_script.csx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/CSharpScriptDemo/main_script.csx) · [`wpf_script.csx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/CSharpScriptDemo/wpf_script.csx)

Use these examples when you want to test a small C# Revit command without creating a full compiled add-in project.

<LogoRow items={['csharp', 'nuget', 'revit']} />

## The script

`main_script.csx` loads a helper script, shows a task dialog, then asks you to pick an element and traces its bounding box and points:

```csharp title="main_script.csx"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPIUI.dll"
#load "./Helpers/DocumentHelper.csx"

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Diagnostics;

[Transaction(TransactionMode.Manual)]
public class DemoCmd : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiapp = commandData.Application;
        var doc = uiapp.ActiveUIDocument.Document;

        DocumentHelper.TraceDocumentStats(doc);
        var info = DocumentHelper.GetProjectInfo(doc);
        var wallCount = DocumentHelper.CountElements<Wall>(doc);

        TaskDialog.Show("C# Script Demo",
            $"{info}\n\nWalls: {wallCount}");

        var selectedRef = uiapp.ActiveUIDocument.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element);
        var selectedElement = doc.GetElement(selectedRef);
        var bbox = selectedElement.get_BoundingBox(null);
        var topCenter = (bbox.Max + new XYZ(bbox.Min.X, bbox.Min.Y, bbox.Max.Z)) / 2;
        Trace.Write(bbox);
        Trace.Write(topCenter);
        Trace.Write(bbox.Max);

        return Result.Succeeded;
    }
}
```

:::info The full file
The file in the repository starts with one extra `#r` for `System.Runtime`, which depends on the .NET SDK installed on that machine. See [`main_script.csx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/CSharpScriptDemo/main_script.csx) and the helper [`DocumentHelper.csx`](https://github.com/trgiangv/RevitDevTool/blob/main/samples/CSharpScriptDemo/Helpers/DocumentHelper.csx).
:::

## What It Shows

- referencing Revit API assemblies from a script;
- loading a helper script with `#load`;
- defining an `IExternalCommand` class;
- showing a Revit `TaskDialog`;
- selecting an element;
- tracing bounding box and point geometry for visualization/logging (`main_script.csx`);
- a minimal WPF surface from a script (`wpf_script.csx`).

## Run

1. Open Revit 2025 or adjust the `#r` paths in the script to match your installed Revit version.
2. Add the [`CSharpScriptDemo`](https://github.com/trgiangv/RevitDevTool/tree/main/samples/CSharpScriptDemo) folder as a script folder.
3. Run `main_script.csx` or `wpf_script.csx`.
4. For `main_script.csx`, select an element when prompted.
5. Review the trace panel and any temporary geometry output.

## What To Edit

Common edits:

- Change Revit API reference paths for another Revit version.
- Add more helper functions under `Helpers/`.
- Replace the selected element logic with a collector.
- Trace different geometry objects to test visualization.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Revit API reference not found | update `#r` paths for your Revit version |
| script compiles but selection fails | run in an open document and select a valid element |
| no visualization | confirm the traced object is supported geometry |
| command not discovered | file name must end with `script.csx` |

## Related

- [C# Scripts](/docs/execution/csharp-script/Execution-CSharp)
- [NuGet Dependencies](/docs/execution/NuGet-Dependencies)
- [.NET Assembly](/docs/execution/dotnet-assembly/Execution-Assembly)

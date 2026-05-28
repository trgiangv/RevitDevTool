#r "C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/8.0.27/ref/net8.0/System.Runtime.dll"
// #r "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8/mscorlib.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2024/RevitAPIUI.dll"
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

        return Result.Succeeded;
    }
}
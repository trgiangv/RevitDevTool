#r "C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/8.0.27/ref/net8.0/System.Runtime.dll"
// #r "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8/mscorlib.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPIUI.dll"

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Diagnostics;


[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
public class DemoCmd : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiapp = commandData.Application;
        var doc = uiapp.ActiveUIDocument.Document;
        var title = doc.Title;
        Trace.WriteLine($"Document title: {title}");

        TaskDialog.Show("Hello", "Hello from C# script!");

        return Result.Succeeded;
    }
}
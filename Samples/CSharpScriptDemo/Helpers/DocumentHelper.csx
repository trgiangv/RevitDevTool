#r "C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/8.0.27/ref/net8.0/System.Runtime.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"

using Autodesk.Revit.DB;
using System.Diagnostics;
using System.Linq;

public static class DocumentHelper
{
    public static string GetProjectInfo(Document doc)
    {
        var info = doc.ProjectInformation;
        return $"Project: {info.Name}\nNumber: {info.Number}\nAuthor: {info.Author}";
    }

    public static int CountElements<T>(Document doc) where T : Element
    {
        using var collector = new FilteredElementCollector(doc);
        return collector.OfClass(typeof(T)).GetElementCount();
    }

    public static void TraceDocumentStats(Document doc)
    {
        Trace.WriteLine($"[DocumentHelper] Title: {doc.Title}");
        Trace.WriteLine($"[DocumentHelper] Path: {doc.PathName}");
        Trace.WriteLine($"[DocumentHelper] IsWorkshared: {doc.IsWorkshared}");
    }
}

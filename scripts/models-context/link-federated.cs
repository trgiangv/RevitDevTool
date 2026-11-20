using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.Manual)]
public class Command : IExternalCommand
{
    private const string WorkFolder = @"C:\Users\truon\source\repos\RevitDevTool\artifacts\models-context-work\2026";

    private static readonly string[] Files =
    {
        "Snowdon Towers Sample Structural.rvt",
        "Snowdon Towers Sample Electrical.rvt",
        "Snowdon Towers Sample Plumbing.rvt",
        "Snowdon Towers Sample Facades.rvt",
        "Snowdon Towers Sample Site.rvt"
    };

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var host = commandData.Application.ActiveUIDocument.Document;
        var linked = new HashSet<string>(new FilteredElementCollector(host).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>()
            .Select(PathOf).Where(path => path.Length > 0).Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        using (var transaction = new Transaction(host, "Model Context federate links"))
        {
            transaction.Start();
            foreach (var file in Files)
            {
                var path = Path.Combine(WorkFolder, file);
                if (linked.Contains(file))
                {
                    lines.Add(file + ": already linked");
                    continue;
                }

                if (!File.Exists(path))
                {
                    lines.Add(file + ": no file " + path);
                    continue;
                }

                try
                {
                    var options = new RevitLinkOptions(false);
                    var result = RevitLinkType.Create(host, ModelPathUtils.ConvertUserVisiblePathToModelPath(path), options);
                    if (result.ElementId == ElementId.InvalidElementId)
                    {
                        lines.Add(file + ": not linked (" + result.LoadResult + ")");
                        continue;
                    }

                    var instance = RevitLinkInstance.Create(host, result.ElementId, ImportPlacement.Origin);
                    var document = instance.GetLinkDocument();
                    lines.Add(file + ": linked (" + result.LoadResult + "), " + (document == null ? "not loaded" : ModelElements(document) + " model elements"));
                }
                catch (Exception exception)
                {
                    lines.Add(file + ": failed: " + exception.Message);
                }
            }

            transaction.Commit();
        }

        message = host.Title + Environment.NewLine + string.Join(Environment.NewLine, lines);
        return Result.Succeeded;
    }

    private static string PathOf(RevitLinkType type)
    {
        try
        {
            var reference = type.GetExternalFileReference();
            return reference == null ? string.Empty : ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetAbsolutePath());
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static int ModelElements(Document document)
    {
        return new FilteredElementCollector(document).WhereElementIsNotElementType()
            .Count(element => element.Category != null && element.Category.CategoryType == CategoryType.Model);
    }
}

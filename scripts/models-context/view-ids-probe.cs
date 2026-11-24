using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.ReadOnly)]
public class Command : IExternalCommand
{
    private const string Tag = "A1";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var ui = commandData.Application.ActiveUIDocument;
        var document = ui.Document;
        var view = ui.ActiveView;
        if (view.IsTemplate || view.ViewType is not (ViewType.FloorPlan or ViewType.CeilingPlan or ViewType.EngineeringPlan
                or ViewType.AreaPlan or ViewType.Section or ViewType.Elevation or ViewType.Detail or ViewType.ThreeD
                or ViewType.Walkthrough))
        {
            message = "Make a plan, section, elevation or 3D view active first: the link view collector crashes Revit in '"
                      + view.Name + "' (" + view.ViewType + ").";
            return Result.Failed;
        }

        var lines = new List<string>();
        var summary = new List<string>();
        Collect(new FilteredElementCollector(document, view.Id), "Current model", lines, summary);
        foreach (var link in new FilteredElementCollector(document).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
        {
            if (link.GetLinkDocument() is not { } linked)
            {
                continue;
            }

            try
            {
                Collect(new FilteredElementCollector(document, view.Id, link.Id), linked.Title, lines, summary);
            }
            catch (Exception exception)
            {
                summary.Add(linked.Title + " " + exception.Message);
            }
        }

        var folder = Path.Combine(Path.GetTempPath(), "ModelContextViewIds");
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, view.Name + "-" + Tag + ".txt");
        File.WriteAllLines(file, lines);
        message = Tag + " " + view.Name + ": " + string.Join("; ", summary) + ". Ids by document and category: " + file;
        return Result.Succeeded;
    }

    private static void Collect(FilteredElementCollector collector, string name, List<string> lines, List<string> summary)
    {
        var count = 0;
        foreach (var element in collector.WhereElementIsNotElementType())
        {
            if (element.Category is not { CategoryType: CategoryType.Model } category
                || category.BuiltInCategory == BuiltInCategory.OST_RvtLinks)
            {
                continue;
            }

            lines.Add(name + "|" + category.Name + "|" + element.Id.Value);
            count++;
        }

        summary.Add(name + " " + count);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.Manual)]
public class Command : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiDocument = commandData.Application.ActiveUIDocument;
        var document = uiDocument.Document;
        var original = uiDocument.ActiveView;
        var views = new FilteredElementCollector(document).OfClass(typeof(View)).Cast<View>().Where(view => !view.IsTemplate).ToList();
        var open = new HashSet<ElementId>(uiDocument.GetOpenUIViews().Select(item => item.ViewId));
        var wanted = new Dictionary<string, Func<View, bool>>
        {
            { "Plan", view => view.ViewType == ViewType.FloorPlan },
            { "ThreeD", view => view.ViewType == ViewType.ThreeD },
            { "Sheet", view => view.ViewType == ViewType.DrawingSheet },
            { "Schedule", view => view.ViewType == ViewType.Schedule && !((ViewSchedule)view).IsTitleblockRevisionSchedule },
            { "Drafting", view => view.ViewType == ViewType.DraftingView },
            { "Legend", view => view.ViewType == ViewType.Legend }
        };

        var lines = new List<string>();
        foreach (var pair in wanted)
        {
            if (views.Any(view => open.Contains(view.Id) && pair.Value(view)))
            {
                lines.Add(pair.Key + ": already open");
                continue;
            }

            var next = views.FirstOrDefault(view => pair.Value(view));
            if (next == null)
            {
                lines.Add(pair.Key + ": the model has none");
                continue;
            }

            uiDocument.ActiveView = next;
            lines.Add(pair.Key + ": opened '" + next.Name + "'");
        }

        uiDocument.ActiveView = original;
        message = string.Join(Environment.NewLine, lines);
        return Result.Succeeded;
    }
}

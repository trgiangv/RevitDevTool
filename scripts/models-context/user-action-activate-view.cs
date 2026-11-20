using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

[Transaction(TransactionMode.Manual)]
public class Command : IExternalCommand
{
    private const string ViewKind = "Plan";
    private const string ViewName = "";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiDocument = commandData.Application.ActiveUIDocument;
        var document = uiDocument.Document;
        var view = ViewName.Length > 0
            ? new FilteredElementCollector(document).OfClass(typeof(View)).Cast<View>()
                .FirstOrDefault(candidate => !candidate.IsTemplate && candidate.Name == ViewName)
            : uiDocument.GetOpenUIViews()
                .Select(open => document.GetElement(open.ViewId) as View)
                .FirstOrDefault(candidate => candidate != null && !candidate.IsTemplate && Matches(candidate));
        if (view == null)
        {
            message = ViewName.Length > 0
                ? "No view named '" + ViewName + "'. Run user-action-template-view.cs first."
                : "No open " + ViewKind + " view. Run user-action-open-views.cs first.";
            return Result.Failed;
        }

        uiDocument.ActiveView = view;
        message = "Active view: '" + view.Name + "' (" + view.ViewType + "). Run the explicit test for it in a run of its own.";
        return Result.Succeeded;
    }

    private static bool Matches(View view)
    {
        switch (ViewKind)
        {
            case "Plan": return view.ViewType == ViewType.FloorPlan || view.ViewType == ViewType.EngineeringPlan || view.ViewType == ViewType.CeilingPlan;
            case "ThreeD": return view.ViewType == ViewType.ThreeD;
            case "Sheet": return view.ViewType == ViewType.DrawingSheet;
            case "Schedule": return view.ViewType == ViewType.Schedule && !((ViewSchedule)view).IsTitleblockRevisionSchedule;
            case "Drafting": return view.ViewType == ViewType.DraftingView;
            case "Legend": return view.ViewType == ViewType.Legend;
            default: return false;
        }
    }
}

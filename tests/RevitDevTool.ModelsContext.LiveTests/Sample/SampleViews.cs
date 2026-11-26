namespace RevitDevTool.ModelsContext.LiveTests;

internal static class SampleViews
{
    public static View View => Named(CaseFiles.Facts.ViewName, ViewType.FloorPlan);

    public static View ThreeD => Named(CaseFiles.Facts.ThreeDViewName, ViewType.ThreeD);

    public static View Sheet => Named(CaseFiles.Facts.SheetViewName, ViewType.DrawingSheet);

    public static UIDocument Active => SampleModel.Application.ActiveUIDocument;

    public static View Of(string kind)
    {
        return kind switch
        {
            "Plan" => View,
            "Sheet" => Sheet,
            _ => throw new ArgumentException($"Unknown view '{kind}'; use Plan or Sheet.", nameof(kind))
        };
    }

    public static bool IsOpen(View view)
    {
        return Active.GetOpenUIViews().Any(open => open.ViewId == view.Id);
    }

    public static void Activate()
    {
        Activate(View);
    }

    public static void Activate(View view)
    {
        if (!IsOpen(view))
        {
            Assert.Inconclusive($"'{view.Name}' is not open yet. Run scripts/test-models-context.ps1, which opens it in a run of its own first; Revit draws a newly opened view only after that run returns.");
        }

        Switch(view);
        Active.RefreshActiveView();
    }

    public static bool Switch(View view)
    {
        if (Active.ActiveView.Id == view.Id)
        {
            return false;
        }

        Active.ActiveView = view;
        return true;
    }

    private static View Named(string name, ViewType type)
    {
        var view = new FilteredElementCollector(SampleModel.Host)
            .OfClass(typeof(View))
            .Cast<View>()
            .FirstOrDefault(candidate => !candidate.IsTemplate && candidate.ViewType == type && candidate.Name == name);
        if (view is null)
        {
            Assert.Inconclusive($"The sample has no {type} view named '{name}'.");
        }

        return view!;
    }
}

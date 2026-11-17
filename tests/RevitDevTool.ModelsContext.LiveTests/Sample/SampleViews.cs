namespace RevitDevTool.ModelsContext.LiveTests;

internal static class SampleViews
{
    public static View View => Named(CaseFiles.Facts.ViewName);

    public static View ThreeD => Named(CaseFiles.Facts.ThreeDViewName);

    public static UIDocument Active => SampleModel.Application.ActiveUIDocument;

    public static void Activate()
    {
        var view = View;
        if (!Active.GetOpenUIViews().Any(open => open.ViewId == view.Id))
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

    private static View Named(string name)
    {
        var view = new FilteredElementCollector(SampleModel.Host)
            .OfClass(typeof(View))
            .Cast<View>()
            .FirstOrDefault(candidate => !candidate.IsTemplate && candidate.Name == name);
        if (view is null)
        {
            Assert.Inconclusive($"The sample has no view named '{name}'.");
        }

        return view!;
    }
}

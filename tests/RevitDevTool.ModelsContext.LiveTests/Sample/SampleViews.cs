namespace RevitDevTool.ModelsContext.LiveTests;

internal static class SampleViews
{
    public static View View => Named(CaseFiles.Facts.ViewName);

    public static View ThreeD => Named(CaseFiles.Facts.ThreeDViewName);

    public static UIDocument Active => SampleModel.Application.ActiveUIDocument;

    public static void Activate()
    {
        var view = View;
        var wasOpen = Active.GetOpenUIViews().Any(open => open.ViewId == view.Id);
        if (Switch(view) && !wasOpen)
        {
            Assert.Inconclusive($"Opened the view '{view.Name}'. Revit draws a newly opened view only after this run returns, so its link counts are not final yet; run again.");
        }
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

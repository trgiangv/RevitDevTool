namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class ViewSetupTests
{
    [Test]
    [Explicit("Run by scripts/test-models-context.ps1 before the full run: Revit draws a newly opened view only after the run returns.")]
    public void OpensSampleView()
    {
        SampleViews.Switch(SampleViews.View);
    }
}

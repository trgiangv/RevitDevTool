using RevitDevTool.Tools.ModelsContext;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class ViewSetupTests
{
    [Test]
    [Explicit("Run by scripts/test-models-context.ps1 before the full run: Revit draws a newly opened view only after the run returns.")]
    public void PreparesSample()
    {
        SampleViews.Switch(SampleViews.View);
        if (!DockablePane.PaneIsRegistered(ModelsContextPane.PaneId))
        {
            Assert.Inconclusive("The Model Context pane is not registered. Deploy the current add-in (scripts/build-host.ps1 -HostApp Revit -Year <year>).");
        }

        if (!DockablePane.PaneExists(ModelsContextPane.PaneId))
        {
            Assert.Inconclusive("The Model Context pane was not created (docs/agents/test-matrix.md, Current gaps: dockable panes on Revit 2024). Start Revit without a file, then open the sample through MCP open_document.");
        }

        SampleModel.Application.GetDockablePane(ModelsContextPane.PaneId).Show();
    }
}

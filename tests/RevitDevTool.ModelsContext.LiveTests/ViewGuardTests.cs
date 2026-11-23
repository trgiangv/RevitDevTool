using DevTools.ModelsContext.Scope;
using RevitDevTool.Core;
using RevitDevTool.Tools.ModelsContext.Documents;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class ViewGuardTests
{
    [Test]
    [Explicit("Old crash path (Gotcha 21): it switches the active view and collects it in the same run. Without the guard Revit ended natively; run it alone, on a Revit you can lose.")]
    public void SwitchThenCollectIsRefused()
    {
        var active = SampleViews.Active;
        var start = active.ActiveView;
        var other = active.GetOpenUIViews()
            .Select(open => active.Document.GetElement(open.ViewId) as View)
            .Where(view => view is not null && view.Id != start.Id)
            .OrderByDescending(view => view is View3D)
            .FirstOrDefault();
        if (other is null)
        {
            Assert.Inconclusive("Only one view is open. Open a second view (the sample's 3D view) through MCP before this run.");
        }

        var watched = ViewActivations.Watch(RevitContext.UiControlledApplication);
        try
        {
            active.ActiveView = other;
            var source = new Adapter().Source;
            var exception = Assert.Throws<InvalidOperationException>(() =>
                source.CollectScopeAsync(SampleModel.Current.Key, ScopeKind.ActiveView, CancellationToken.None).GetAwaiter().GetResult());
            Assert.That(exception!.Message, Is.EqualTo(ViewActivations.ChangedInCall));
        }
        finally
        {
            active.ActiveView = start;
            if (watched)
            {
                ViewActivations.Unwatch();
            }
        }
    }
}

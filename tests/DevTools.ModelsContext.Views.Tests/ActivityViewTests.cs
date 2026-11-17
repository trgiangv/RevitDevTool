namespace DevTools.ModelsContext.Views.Tests;

[TestClass]
public sealed class ActivityViewTests
{
    [TestMethod]
    public void PanelStatusShowsActivity()
    {
        using var ui = PanelUi.Open();
        WpfSta.Invoke(() => ui.Clock.Advance(TimeSpan.FromSeconds(1)));
        var expected = WpfSta.Invoke(() => ui.Panel.Activity);

        Assert.StartsWith("Idle ", expected);
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindInWindow("ModelContext.Panel").Properties.ItemStatus.Value == expected),
            "ModelContext.Panel does not show " + expected);
    }

    [TestMethod]
    public void EditShowsBusyStatus()
    {
        using var ui = PanelUi.Open();
        ui.ShowView("ModelContext.View.Filter");
        ui.Press("Filter.Add");
        Assert.IsTrue(ThemeHost.WaitFor(() => ui.Panel.Filter.Library.IsEditingName), "Add did not open a new filter.");
        WpfSta.Invoke(() => ui.Clock.Advance(TimeSpan.FromSeconds(1)));

        WpfSta.Invoke(() => ui.Panel.Filter.Categories.Items[0].IsChecked = true);

        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindInWindow("ModelContext.Panel").Properties.ItemStatus.Value.StartsWith("Busy ", StringComparison.Ordinal)),
            "The panel did not report Busy after an edit.");
        WpfSta.Invoke(() => ui.Clock.Advance(TimeSpan.FromSeconds(1)));
        Assert.IsTrue(ThemeHost.WaitForUi(() => ui.Host.FindInWindow("ModelContext.Panel").Properties.ItemStatus.Value.StartsWith("Idle ", StringComparison.Ordinal)),
            "The panel did not settle Idle after the run.");
    }
}

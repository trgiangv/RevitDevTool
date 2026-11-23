using DevTools.ModelsContext.Panel;

namespace DevTools.ModelsContext.Tests.Panel;

[TestClass]
public sealed class ActivityTests
{
    [TestMethod]
    public async Task OpenPanelSettlesIdle()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Settle();

        Assert.StartsWith("Idle ", fixture.Panel.Activity);
    }

    [TestMethod]
    public async Task EditIsBusyUntilRun()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        filter.Library.AddCommand.Execute(null);
        fixture.Settle();
        var before = Generation(fixture.Panel.Activity);

        filter.Categories.Items.First(category => category.Name == "Doors").IsChecked = true;

        Assert.StartsWith("Busy ", fixture.Panel.Activity);
        fixture.Settle();
        Assert.StartsWith("Idle ", fixture.Panel.Activity);
        Assert.IsGreaterThan(before, Generation(fixture.Panel.Activity));
    }

    [TestMethod]
    public async Task ActivityRaisesPropertyChanged()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Settle();
        var raised = 0;
        fixture.Panel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ModelContextViewModel.Activity))
            {
                raised++;
            }
        };

        fixture.Filter.Library.AddCommand.Execute(null);

        Assert.IsGreaterThan(0, raised);
    }

    private static int Generation(string activity)
    {
        return int.Parse(activity.Substring(activity.IndexOf(' ') + 1), System.Globalization.CultureInfo.InvariantCulture);
    }
}

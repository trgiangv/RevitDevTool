using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Visibility;

[TestClass]
public sealed class CategoryHideTests
{
    [TestMethod]
    public async Task KeepLeavesCategoryHidden()
    {
        using var fixture = await PanelFixture.OpenAsync();
        await Item(fixture, "Ducts").ToggleHiddenCommand.ExecuteAsync(null);

        await fixture.Panel.TemporaryBar.KeepCommand.ExecuteAsync(null);

        Assert.IsTrue(Item(fixture, "Ducts").IsHidden);
    }

    [TestMethod]
    public async Task ShowAfterKeepPlansShow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        await Item(fixture, "Ducts").ToggleHiddenCommand.ExecuteAsync(null);
        await fixture.Panel.TemporaryBar.KeepCommand.ExecuteAsync(null);

        await Item(fixture, "Ducts").ToggleHiddenCommand.ExecuteAsync(null);

        Assert.IsFalse(Item(fixture, "Ducts").IsHidden);
    }

    [TestMethod]
    public async Task EyeHideShowsLinkNotice()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Actions.Notice = VisibilityText.LinksKeepOwnSettings;

        await Item(fixture, "Ducts").ToggleHiddenCommand.ExecuteAsync(null);

        Assert.AreEqual(VisibilityText.LinksKeepOwnSettings, fixture.Panel.Message);
    }

    private static CategoryItemViewModel Item(PanelFixture fixture, string name)
    {
        return fixture.Explorer.Categories.Picker.Items.First(item => item.Name == name);
    }
}

using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class PanelBarsTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    [TestMethod]
    public async Task KeepAsksWhenLinkChangesPending()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        await fixture.Panel.TemporaryBar.KeepCommand.ExecuteAsync(null);
        var asked = fixture.Panel.TemporaryBar.IsConfirmingKeep;
        var keptBefore = fixture.Model.KeepCount;
        await fixture.Panel.TemporaryBar.ConfirmKeepCommand.ExecuteAsync(null);

        Assert.IsTrue(asked);
        Assert.AreEqual(0, keptBefore);
        Assert.AreEqual(1, fixture.Model.KeepCount);
    }

    [TestMethod]
    public async Task KeepRunsAtOnceWithoutLinks()
    {
        using var fixture = await PanelFixture.OpenAsync();
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));

        await fixture.Panel.TemporaryBar.KeepCommand.ExecuteAsync(null);

        Assert.IsFalse(fixture.Panel.TemporaryBar.IsConfirmingKeep);
        Assert.AreEqual(1, fixture.Model.KeepCount);
    }

    [TestMethod]
    public async Task ResetClearsJournal()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var bar = fixture.Panel.TemporaryBar;
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));
        var shown = bar.IsVisible;
        var changes = bar.ChangeCount;

        await bar.ResetCommand.ExecuteAsync(null);

        Assert.IsTrue(shown);
        Assert.AreEqual(1, changes);
        Assert.AreEqual(0, bar.ChangeCount);
        Assert.IsFalse(fixture.Panel.IsTemporaryBarVisible);
        Assert.AreEqual(1, fixture.Model.ResetCount);
    }

    [TestMethod]
    public async Task SelectHiddenWhileSyncOn()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Explorer.IsSyncOn = true;

        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));

        Assert.IsFalse(fixture.Panel.ContextMenu[MenuAction.SelectInHost].IsVisible);
    }

    [TestMethod]
    public async Task SelectShownWhileSyncOff()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Explorer.IsSyncOn = false;

        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));

        Assert.IsTrue(fixture.Panel.ContextMenu[MenuAction.SelectInHost].IsVisible);
    }

    [TestMethod]
    public async Task HalftoneDisabledOnLinkChild()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);

        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));

        var menu = fixture.Panel.ContextMenu;
        Assert.IsFalse(menu[MenuAction.Halftone].IsEnabled);
        Assert.AreEqual(VisibilityReasons.LinkChildOnlyShowIsolate, menu[MenuAction.Halftone].DisabledReason);
        Assert.IsTrue(menu[MenuAction.Isolate].UsesReferenceCommands);
    }

    [TestMethod]
    public async Task SectionBoxNeeds3DView()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));

        Assert.AreEqual(ContextMenuViewModel.NeedsThreeDView,
            fixture.Panel.ContextMenu[MenuAction.SectionBox].DisabledReason);
    }

    [TestMethod]
    public async Task ResetViewOnlyInTemporaryMode()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var menu = fixture.Panel.ContextMenu;
        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));
        var before = menu[MenuAction.ResetView].IsVisible;

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));
        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));

        Assert.IsFalse(before);
        Assert.IsTrue(menu[MenuAction.ResetView].IsVisible);
        Assert.AreEqual("Show", menu[MenuAction.HideShow].Text);
    }

    [TestMethod]
    public async Task ShortcutZoomsTarget()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));

        var ran = await fixture.Panel.ContextMenu.RunShortcutAsync("z");

        Assert.IsTrue(ran);
        Assert.HasCount(1, fixture.Model.Zoomed);
    }

    [TestMethod]
    public async Task DefaultTicksCurrentModelOnly()
    {
        using var fixture = await PanelFixture.OpenAsync();

        var ticked = fixture.Panel.ContextBar.TickedDocuments;

        CollectionAssert.AreEqual(new[] { Current }, ticked.ToArray());
        Assert.AreEqual(ContextBarViewModel.CurrentModelLabel, fixture.Panel.ContextBar.Summary);
    }

    [TestMethod]
    public async Task AtLeastOneDocumentStaysTicked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var current = fixture.Panel.ContextBar.Documents.Single(item => item.Key == Current);

        current.IsChecked = false;

        Assert.IsTrue(current.IsChecked);
    }

    [TestMethod]
    public async Task SelectionScopeHidesDocuments()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Panel.ContextBar.Scope = ScopeKind.Selection;

        Assert.IsFalse(fixture.Panel.ContextBar.IsDocumentListVisible);
    }

    [TestMethod]
    public async Task DuplicateNamesGetSuffix()
    {
        var data = TestModel.SmallData;
        var documents = data.Documents
            .Select(document => document.Key == FakeModelGenerator.ArchSecond
                ? document with { DisplayName = "Arch.rvt" }
                : document.Key == Mep
                    ? document with { IsLoaded = false }
                    : document)
            .ToList();
        using var fixture = await PanelFixture.OpenAsync(data with { Documents = documents });
        var bar = fixture.Panel.ContextBar;

        var second = bar.Documents.Single(item => item.Key == FakeModelGenerator.ArchSecond);
        var mep = bar.Documents.Single(item => item.Key == Mep);
        mep.IsChecked = true;

        Assert.AreEqual("Arch.rvt (2)", second.Label);
        Assert.IsFalse(mep.IsEnabled);
        Assert.IsFalse(mep.IsChecked);
    }
}

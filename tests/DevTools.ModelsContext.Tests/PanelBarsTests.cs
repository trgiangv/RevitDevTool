using DevTools.ModelsContext.Engine;
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
        var keptBefore = fixture.Actions.KeepCount;
        await fixture.Panel.TemporaryBar.ConfirmKeepCommand.ExecuteAsync(null);

        Assert.IsTrue(asked);
        Assert.AreEqual(0, keptBefore);
        Assert.AreEqual(1, fixture.Actions.KeepCount);
    }

    [TestMethod]
    public async Task KeepRunsAtOnceWithoutLinks()
    {
        using var fixture = await PanelFixture.OpenAsync();
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));

        await fixture.Panel.TemporaryBar.KeepCommand.ExecuteAsync(null);

        Assert.IsFalse(fixture.Panel.TemporaryBar.IsConfirmingKeep);
        Assert.AreEqual(1, fixture.Actions.KeepCount);
    }

    [TestMethod]
    public async Task ResetEmptiesTemporaryBar()
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
        Assert.AreEqual(1, fixture.Actions.ResetCount);
    }

    [TestMethod]
    public async Task SelectHiddenWhileSyncOn()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Explorer.IsSelectionSyncOn = true;

        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));

        Assert.IsFalse(fixture.Panel.ContextMenu[MenuAction.SelectInHost].IsVisible);
    }

    [TestMethod]
    public async Task SelectShownWhileSyncOff()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Explorer.IsSelectionSyncOn = false;

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
        Assert.AreEqual(VisibilityReasons.LinkChildOnlyHideShow, menu[MenuAction.Halftone].DisabledReason);
        Assert.AreEqual(VisibilityReasons.LinkChildOnlyHideShow, menu[MenuAction.Isolate].DisabledReason);
        Assert.AreEqual(VisibilityReasons.LinkChildOnlyHideShow, menu[MenuAction.Color].DisabledReason);
        Assert.IsTrue(menu[MenuAction.HideShow].UsesReferenceCommands);
    }

    [TestMethod]
    public async Task LinkChildHideRunsAndJournals()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        var ducts = fixture.Explorer.ElementsOf([fixture.Group(Mep, "Ducts")]);

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        Assert.IsTrue(fixture.Actions.ExecutedPlans.Single().UsesReferenceCommands);
        CollectionAssert.AreEquivalent(ducts.ToList(),
            fixture.Panel.Journal.LinkEntries.Select(entry => entry.Element).ToList());
        Assert.AreEqual(1, fixture.Panel.TemporaryBar.ChangeCount);
    }

    [TestMethod]
    public async Task LinkChildShowUnhidesItsEntries()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        var ducts = fixture.Explorer.ElementsOf([fixture.Group(Mep, "Ducts")]);
        var pipes = fixture.Explorer.ElementsOf([fixture.Group(Mep, "Pipes")]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Pipes"));

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        Assert.IsNull(fixture.Explorer.Message);
        CollectionAssert.AreEquivalent(ducts.ToList(), fixture.Actions.ShownLinked.Single().ToList());
        CollectionAssert.AreEquivalent(pipes.ToList(),
            fixture.Panel.Journal.LinkEntries.Select(entry => entry.Element).ToList());
        Assert.AreEqual(0, fixture.Actions.ResetCount);
        Assert.AreEqual(RowEye.Shown, fixture.Group(Mep, "Ducts").Eye);
        Assert.AreEqual(RowEye.Hidden, fixture.Group(Mep, "Pipes").Eye);
    }

    [TestMethod]
    public async Task ShowWithoutUnhideIsDisabled()
    {
        using var fixture = await PanelFixture.OpenAsync(WithoutUnhide());
        fixture.Panel.ContextBar.Tick([Mep]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));

        var item = fixture.Panel.ContextMenu[MenuAction.HideShow];
        Assert.AreEqual("Show", item.Text);
        Assert.AreEqual(VisibilityReasons.ShowFromReveal, item.DisabledReason);
    }

    [TestMethod]
    public async Task ResetShowsJournaledLinkElements()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        var group = fixture.Group(Mep, "Ducts");
        var ducts = fixture.Explorer.ElementsOf([group]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(group);

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        CollectionAssert.AreEquivalent(ducts.ToList(), fixture.Actions.ShownLinked.Single().ToList());
        Assert.AreEqual(JournalState.Empty, fixture.Panel.Journal);
        Assert.IsFalse(fixture.Panel.IsTemporaryBarVisible);
    }

    [TestMethod]
    public async Task ResetFailureShowsMessage()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));
        fixture.Actions.ResetError = "Reset failed in the host.";

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.AreEqual("Reset failed in the host.", fixture.Panel.Message);
    }

    [TestMethod]
    public async Task ResetLeavesHidesWithoutUnhide()
    {
        using var fixture = await PanelFixture.OpenAsync(WithoutUnhide());
        fixture.Panel.ContextBar.Tick([Mep]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.IsEmpty(fixture.Actions.ShownLinked);
        Assert.IsNotEmpty(fixture.Panel.Journal.LinkEntries);
        Assert.AreEqual(RowEye.Hidden, fixture.Group(Mep, "Ducts").Eye);
    }

    [TestMethod]
    public async Task KeepClearsJournalWithoutShow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        await fixture.Panel.TemporaryBar.KeepCommand.ExecuteAsync(null);
        await fixture.Panel.TemporaryBar.ConfirmKeepCommand.ExecuteAsync(null);

        Assert.IsEmpty(fixture.Actions.ShownLinked);
        Assert.AreEqual(JournalState.Empty, fixture.Panel.Journal);
    }

    [TestMethod]
    public async Task CurrentShowKeepsLinkHides()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Doors"));

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));

        Assert.AreEqual(1, fixture.Actions.ResetCount);
        Assert.IsFalse(fixture.Actions.ExecutedPlans.Any(plan => plan.UsesReferenceCommands));
        Assert.IsEmpty(fixture.Actions.ShownLinked);
        Assert.IsNotEmpty(fixture.Panel.Journal.LinkEntries);
        Assert.AreEqual(RowEye.Hidden, fixture.Group(Mep, "Ducts").Eye);
        Assert.AreEqual(2, fixture.Panel.TemporaryBar.ChangeCount);
    }

    private static FakeModelData WithoutUnhide()
    {
        var data = TestModel.SmallData;
        return data with
        {
            Documents = data.Documents
                .Select(document => document.Key.IsCurrentModel
                    ? document
                    : document with { Capabilities = document.Capabilities & ~DocumentCapabilities.ReferenceUnhide })
                .ToList()
        };
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
        Assert.HasCount(1, fixture.Actions.Zoomed);
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
    public async Task LastUntickShowsHint()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var current = fixture.Panel.ContextBar.Documents.Single(item => item.Key == Current);

        current.IsChecked = false;

        Assert.AreEqual(ContextBarViewModel.LastDocumentHint, fixture.Panel.Message);
    }

    [TestMethod]
    public async Task HintClearsOnNextTick()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Documents.Single(item => item.Key == Current).IsChecked = false;

        fixture.Panel.ContextBar.Documents.Single(item => item.Key == Mep).IsChecked = true;

        Assert.IsNull(fixture.Panel.Message);
    }

    [TestMethod]
    public async Task CurrentModelUnticksWithLink()
    {
        using var fixture = await PanelFixture.OpenAsync();

        UntickCurrentForMep(fixture);

        CollectionAssert.AreEqual(new[] { Mep }, fixture.Panel.ContextBar.TickedDocuments.ToArray());
    }

    [TestMethod]
    public async Task LastLinkStaysTicked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        UntickCurrentForMep(fixture);
        var mep = fixture.Panel.ContextBar.Documents.Single(item => item.Key == Mep);

        mep.IsChecked = false;

        Assert.IsTrue(mep.IsChecked);
    }

    [TestMethod]
    public async Task DocumentRefreshKeepsCurrentUnticked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        UntickCurrentForMep(fixture);

        fixture.Model.RaiseDocumentsChanged();
        fixture.Settle();

        CollectionAssert.AreEqual(new[] { Mep }, fixture.Panel.ContextBar.TickedDocuments.ToArray());
    }

    [TestMethod]
    public async Task ViewChangeKeepsCurrentUnticked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        UntickCurrentForMep(fixture);

        fixture.Model.RaiseViewActivated(new ViewInfo(Current, "view-2", "Level 2", false));
        fixture.Settle();

        CollectionAssert.AreEqual(new[] { Mep }, fixture.Panel.ContextBar.TickedDocuments.ToArray());
    }

    [TestMethod]
    public async Task LinkReloadKeepsCurrentUnticked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        UntickCurrentForMep(fixture);

        fixture.Model.SetLoaded(Mep, false);
        fixture.Settle();
        fixture.Model.SetLoaded(Mep, true);
        fixture.Settle();

        CollectionAssert.AreEqual(new[] { Mep }, fixture.Panel.ContextBar.TickedDocuments.ToArray());
    }

    [TestMethod]
    public async Task ExplorerSkipsUntickedCurrent()
    {
        using var fixture = await PanelFixture.OpenAsync();

        UntickCurrentForMep(fixture);

        var rows = fixture.Explorer.Rows;
        Assert.IsTrue(rows.Any(row => row.Kind == TreeRowKind.LinkModel && row.Document == Mep));
        Assert.IsFalse(rows.Any(row => row.Kind == TreeRowKind.CurrentModelHeader || (row.Document == Current && row.Kind == TreeRowKind.Group)));
    }

    [TestMethod]
    public async Task FilterSkipsUntickedCurrent()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var filter = fixture.Filter;
        UntickCurrentForMep(fixture);
        filter.Library.AddCommand.Execute(null);

        filter.Categories.Items.First(category => category.Name == "Ducts").IsChecked = true;
        fixture.Settle();

        Assert.IsNotEmpty(filter.Runner.Result);
        Assert.IsTrue(filter.Runner.Result.All(element => element.Document == Mep));
    }

    [TestMethod]
    public async Task FilterSwitchSelectsFilterTab()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Panel.IsFilterTab = true;

        Assert.AreEqual(PanelTab.Filter, fixture.Panel.SelectedTab);
        Assert.IsFalse(fixture.Panel.IsExplorerTab);
    }

    private static void UntickCurrentForMep(PanelFixture fixture)
    {
        var bar = fixture.Panel.ContextBar;
        bar.Tick([Mep]);
        bar.Documents.Single(item => item.Key == Current).IsChecked = false;
        fixture.Settle();
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

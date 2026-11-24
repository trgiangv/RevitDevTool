using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Panel;

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
    public async Task LinkGroupMenuOffersLinkActions()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);

        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));

        var menu = fixture.Panel.ContextMenu;
        Assert.IsTrue(menu[MenuAction.Halftone].IsEnabled);
        Assert.AreEqual(VisibilityText.LinkFilterNote, menu[MenuAction.Halftone].ToolTipText);
        Assert.AreEqual(VisibilityText.LinkFilterNote, menu[MenuAction.Color].ToolTipText);
        Assert.IsTrue(menu[MenuAction.Isolate].IsEnabled);
        Assert.AreEqual(VisibilityText.SavedWithView, menu[MenuAction.Isolate].Note);
        Assert.AreEqual(VisibilityText.SavedWithView, menu[MenuAction.HideShow].Note);
    }

    [TestMethod]
    public async Task LinkOverridesNeedNewerRevit()
    {
        using var fixture = await PanelFixture.OpenAsync(TestModel.WithoutLinkFilters());
        fixture.Panel.ContextBar.Tick([Mep]);

        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));

        var menu = fixture.Panel.ContextMenu;
        Assert.AreEqual(VisibilityText.LinkFiltersUnavailable, menu[MenuAction.Halftone].DisabledReason);
        Assert.IsTrue(menu[MenuAction.Isolate].IsEnabled);
    }

    [TestMethod]
    public async Task LinkIsolateJournalsRest()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        var ducts = fixture.Group(Mep, "Ducts");
        var kept = fixture.Explorer.ElementsOf([ducts]).ToHashSet();
        fixture.Explorer.OpenMenu(ducts);

        await fixture.Panel.ContextMenu[MenuAction.Isolate].Command.ExecuteAsync(null);

        var plan = fixture.Actions.ExecutedPlans.Single();
        var step = (LinkIsolateStep)plan.Steps.Single();
        var journaled = fixture.Panel.Journal.LinkHides.Select(hide => hide.Element).ToList();
        Assert.IsNotNull(plan.ViewId);
        Assert.AreEqual(kept.Count, step.KeptIds.Count);
        Assert.IsNotEmpty(journaled);
        Assert.IsFalse(journaled.Any(kept.Contains));
        Assert.AreEqual(RowEye.Hidden, fixture.Group(Mep, "Pipes").Eye);
        Assert.AreEqual(RowEye.Shown, ducts.Eye);
    }

    [TestMethod]
    public async Task ReloadKeepsLinkIsolate()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));
        fixture.Model.UniqueIdGate = new TaskCompletionSource<bool>();

        var isolate = fixture.Panel.ContextMenu[MenuAction.Isolate].Command.ExecuteAsync(null);
        await fixture.Panel.RefreshAsync();
        fixture.Model.UniqueIdGate.SetResult(true);
        await isolate;

        Assert.IsNotEmpty(fixture.Panel.Journal.LinkHides);
        Assert.IsTrue(fixture.Panel.IsTemporaryBarVisible);
    }

    [TestMethod]
    public async Task MenuRefusalShowsInPanel()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Current, Mep]);
        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));
        await fixture.Panel.ContextMenu[MenuAction.Isolate].Command.ExecuteAsync(null);
        fixture.Actions.ShowError = VisibilityText.NotShown;
        fixture.Explorer.OpenMenu(fixture.Group(Current, "Walls"));

        await fixture.Panel.ContextMenu[MenuAction.HideShow].Command.ExecuteAsync(null);

        Assert.AreEqual(VisibilityText.LinkedStayHidden, fixture.Panel.Message);
    }

    [TestMethod]
    public async Task ResetShowsIsolateRest()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        var ducts = fixture.Group(Mep, "Ducts");
        fixture.Explorer.OpenMenu(ducts);
        await fixture.Panel.ContextMenu[MenuAction.Isolate].Command.ExecuteAsync(null);
        var rest = fixture.Panel.Journal.LinkHides.Select(hide => hide.Element).ToList();

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        CollectionAssert.AreEquivalent(rest, fixture.Actions.ShownLinked.Single().ToList());
        Assert.AreEqual(JournalPage.Empty, fixture.Panel.Journal);
        Assert.AreEqual(RowEye.Shown, fixture.Group(Mep, "Pipes").Eye);
    }

    [TestMethod]
    public async Task ResetDropsUnshownOnce()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));
        fixture.Actions.ShowError = VisibilityText.NotShown;

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);
        var first = fixture.Panel.Message;
        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.AreEqual(VisibilityText.LinkedStayHidden, first);
        Assert.AreEqual(JournalPage.Empty, fixture.Panel.Journal);
        Assert.HasCount(1, fixture.Actions.ShownLinked);
    }

    [TestMethod]
    public async Task EyeOnEndsLinkIsolate()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Current, Mep]);
        var ducts = fixture.Group(Mep, "Ducts");
        fixture.Explorer.OpenMenu(ducts);
        await fixture.Panel.ContextMenu[MenuAction.Isolate].Command.ExecuteAsync(null);
        var rest = fixture.Panel.Journal.LinkHides.Select(hide => hide.Element).ToList();
        var walls = fixture.Group(Current, "Walls");
        Assert.AreEqual(RowEye.Hidden, walls.Eye);

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(walls);

        CollectionAssert.AreEquivalent(rest, fixture.Actions.ShownLinked.Single().ToList());
        Assert.IsEmpty(fixture.Panel.Journal.LinkHides);
        Assert.AreEqual(RowEye.Shown, fixture.Group(Mep, "Pipes").Eye);
    }

    [TestMethod]
    public async Task EyeOnUnshownRestReports()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Current, Mep]);
        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));
        await fixture.Panel.ContextMenu[MenuAction.Isolate].Command.ExecuteAsync(null);
        fixture.Actions.ShowError = VisibilityText.NotShown;

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));

        Assert.AreEqual(VisibilityText.LinkedStayHidden, fixture.Panel.Message);
        Assert.IsEmpty(fixture.Panel.Journal.LinkHides);
    }

    [TestMethod]
    public async Task KeepDropsIsolateJournal()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));
        await fixture.Panel.ContextMenu[MenuAction.Isolate].Command.ExecuteAsync(null);

        await fixture.Panel.TemporaryBar.KeepCommand.ExecuteAsync(null);
        await fixture.Panel.TemporaryBar.ConfirmKeepCommand.ExecuteAsync(null);

        Assert.AreEqual(1, fixture.Actions.KeepCount);
        Assert.IsEmpty(fixture.Actions.ShownLinked);
        Assert.AreEqual(JournalPage.Empty, fixture.Panel.Journal);
    }

    [TestMethod]
    public async Task EyeOnReappliesLinkColorOnce()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Current, Mep]);
        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));
        await fixture.Panel.ContextMenu[MenuAction.Halftone].Command.ExecuteAsync(null);
        var walls = fixture.Group(Current, "Walls");
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(walls);

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(walls);

        Assert.AreEqual(1, fixture.Actions.ResetCount);
        Assert.HasCount(1, fixture.Actions.ExecutedPlans.Where(plan => plan.UsesLinkFilter));
    }

    [TestMethod]
    public async Task ClosedLinkDropsFilterPlan()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Current, Mep]);
        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));
        await fixture.Panel.ContextMenu[MenuAction.Halftone].Command.ExecuteAsync(null);
        var walls = fixture.Group(Current, "Walls");
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(walls);
        fixture.Model.RaiseDocumentClosing(Mep);
        fixture.Settle();

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));

        Assert.AreEqual(1, fixture.Actions.ResetCount);
        Assert.IsFalse(fixture.Actions.ExecutedPlans.Any(plan => plan.UsesLinkFilter));
    }

    [TestMethod]
    public async Task LinkColorMarksRows()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        var ducts = fixture.Group(Mep, "Ducts");
        fixture.Explorer.OpenMenu(ducts);

        await fixture.Panel.ContextMenu[MenuAction.Halftone].Command.ExecuteAsync(null);

        var step = (LinkFilterStep)fixture.Actions.ExecutedPlans.Single().Steps.Single();
        Assert.AreEqual(fixture.Explorer.ElementsOf([ducts]).Count, step.Ids.Count);
        Assert.IsTrue(ducts.IsHalftone);
    }

    [TestMethod]
    public async Task LinkChildHideRunsAndJournals()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        var ducts = fixture.Explorer.ElementsOf([fixture.Group(Mep, "Ducts")]);

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        Assert.IsTrue(fixture.Actions.ExecutedPlans.Single().UsesLinkHide);
        CollectionAssert.AreEquivalent(ducts.ToList(),
            fixture.Panel.Journal.LinkHides.Select(hide => hide.Element).ToList());
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

        Assert.IsNull(fixture.Panel.Message);
        CollectionAssert.AreEquivalent(ducts.ToList(), fixture.Actions.ShownLinked.Single().ToList());
        CollectionAssert.AreEquivalent(pipes.ToList(),
            fixture.Panel.Journal.LinkHides.Select(hide => hide.Element).ToList());
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
        Assert.AreEqual(VisibilityText.ShowFromReveal, item.DisabledReason);
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
        Assert.AreEqual(JournalPage.Empty, fixture.Panel.Journal);
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
    public async Task MenuOpenClearsOldError()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Mep]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));
        fixture.Actions.ResetError = "Reset failed in the host.";
        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));

        Assert.IsNull(fixture.Panel.Message);
    }

    [TestMethod]
    public async Task ResetLeavesHidesWithoutUnhide()
    {
        using var fixture = await PanelFixture.OpenAsync(WithoutUnhide());
        fixture.Panel.ContextBar.Tick([Mep]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.IsEmpty(fixture.Actions.ShownLinked);
        Assert.IsNotEmpty(fixture.Panel.Journal.LinkHides);
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
        Assert.AreEqual(JournalPage.Empty, fixture.Panel.Journal);
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
        Assert.IsFalse(fixture.Actions.ExecutedPlans.Any(plan => plan.UsesLinkHide));
        Assert.IsEmpty(fixture.Actions.ShownLinked);
        Assert.IsNotEmpty(fixture.Panel.Journal.LinkHides);
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
                    : document with { Capabilities = document.Capabilities & ~DocumentCapabilities.LinkShow })
                .ToList()
        };
    }

    [TestMethod]
    public async Task SectionBoxNeeds3DView()
    {
        using var fixture = await PanelFixture.OpenAsync();

        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));

        Assert.AreEqual(VisibilityText.Needs3DView,
            fixture.Panel.ContextMenu[MenuAction.SectionBox].DisabledReason);
    }

    [TestMethod]
    public async Task HiddenRowMenuSaysShow()
    {
        using var fixture = await PanelFixture.OpenAsync();
        var menu = fixture.Panel.ContextMenu;

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Ducts"));
        fixture.Explorer.OpenMenu(fixture.Group(Current, "Ducts"));

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
    public async Task ViewChangeResetsManualTicks()
    {
        using var fixture = await PanelFixture.OpenAsync();
        UntickCurrentForMep(fixture);

        fixture.Model.RaiseViewActivated(new ViewInfo(Current, "view-2", "Level 2", ViewKind.Plan));
        fixture.Settle();

        CollectionAssert.AreEqual(new[] { Current }, fixture.Panel.ContextBar.TickedDocuments.ToArray());
    }

    [TestMethod]
    public async Task OpenTicksViewLinks()
    {
        var model = new FakeModel(TestModel.SmallData);
        model.ViewLinks.Add(Mep);
        using var fixture = PanelFixture.Create(model);

        await fixture.Panel.InitializeAsync(CancellationToken.None);

        CollectionAssert.AreEquivalent(new[] { Current, Mep }, fixture.Panel.ContextBar.TickedDocuments.ToArray());
    }

    [TestMethod]
    public async Task ViewChangeTicksViewLinks()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.ViewLinks.Add(Mep);

        fixture.Model.RaiseViewActivated(new ViewInfo(Current, "view-2", "Level 2", ViewKind.Plan));
        fixture.Settle();

        CollectionAssert.AreEquivalent(new[] { Current, Mep }, fixture.Panel.ContextBar.TickedDocuments.ToArray());
    }

    [TestMethod]
    public async Task ManualTickHoldsUntilViewChange()
    {
        var model = new FakeModel(TestModel.SmallData);
        model.ViewLinks.Add(Mep);
        using var fixture = PanelFixture.Create(model);
        await fixture.Panel.InitializeAsync(CancellationToken.None);
        fixture.Panel.ContextBar.Documents.Single(item => item.Key == Mep).IsChecked = false;
        fixture.Settle();

        fixture.Model.RaiseDocumentsChanged();
        fixture.Settle();
        var held = fixture.Panel.ContextBar.TickedDocuments.ToArray();
        fixture.Model.RaiseViewActivated(new ViewInfo(Current, "view-2", "Level 2", ViewKind.Plan));
        fixture.Settle();

        CollectionAssert.AreEqual(new[] { Current }, held);
        CollectionAssert.AreEquivalent(new[] { Current, Mep }, fixture.Panel.ContextBar.TickedDocuments.ToArray());
    }

    [TestMethod]
    public async Task HiddenLinkNotTicked()
    {
        var model = new FakeModel(TestModel.SmallData);
        model.ViewLinks.Add(Mep);
        using var fixture = PanelFixture.Create(model);

        await fixture.Panel.InitializeAsync(CancellationToken.None);

        var ticked = fixture.Panel.ContextBar.TickedDocuments;
        CollectionAssert.DoesNotContain(ticked.ToList(), FakeModelGenerator.ArchFirst);
        CollectionAssert.DoesNotContain(ticked.ToList(), FakeModelGenerator.ArchSecond);
    }

    [TestMethod]
    public async Task UnloadedViewLinkStaysUnticked()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.SetLoaded(Mep, false);
        fixture.Settle();
        fixture.Model.ViewLinks.Add(Mep);

        fixture.Model.RaiseViewActivated(new ViewInfo(Current, "view-2", "Level 2", ViewKind.Plan));
        fixture.Settle();

        CollectionAssert.AreEqual(new[] { Current }, fixture.Panel.ContextBar.TickedDocuments.ToArray());
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

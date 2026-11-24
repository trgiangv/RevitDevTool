using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Visibility;

[TestClass]
public sealed class ViewStateTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    [TestMethod]
    public async Task StaleViewHideRunsOnActive()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.View = new ViewInfo(Current, "view-2", "Level 2", ViewKind.Plan);

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));

        Assert.IsNull(fixture.Panel.Message);
        Assert.AreEqual("view-2", fixture.Actions.ExecutedPlans.Single().ViewId);
        Assert.AreEqual(1, fixture.Panel.Journal.ChangeCount);
    }

    [TestMethod]
    public async Task StaleViewResetRunsOnActive()
    {
        using var fixture = await PanelFixture.OpenAsync();
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));
        fixture.Model.View = new ViewInfo(Current, "view-2", "Level 2", ViewKind.Plan);

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.IsNull(fixture.Panel.Message);
        Assert.AreEqual(1, fixture.Actions.ResetCount);
    }

    [TestMethod]
    public async Task StaleViewKeepRunsOnActive()
    {
        using var fixture = await PanelFixture.OpenAsync();
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));
        fixture.Model.View = new ViewInfo(Current, "view-2", "Level 2", ViewKind.Plan);

        await fixture.Panel.TemporaryBar.KeepCommand.ExecuteAsync(null);

        Assert.IsNull(fixture.Panel.Message);
        Assert.AreEqual(1, fixture.Actions.KeepCount);
    }

    [TestMethod]
    public async Task OtherModelViewRefusesHide()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Model.View = new ViewInfo(DocumentKey.Current("other-model"), "view-2", "Level 2", ViewKind.Plan);

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));

        Assert.AreEqual(VisibilityText.ViewChanged, fixture.Panel.Message);
        Assert.IsEmpty(fixture.Actions.ExecutedPlans);
        Assert.AreEqual(0, fixture.Panel.Journal.ChangeCount);
    }

    [TestMethod]
    public async Task FilterCleanupDropsLinkColor()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Current, Mep]);
        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));
        await fixture.Panel.ContextMenu[MenuAction.Halftone].Command.ExecuteAsync(null);

        fixture.Model.RaiseViewFiltersRemoved(Current);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));

        Assert.IsFalse(fixture.Actions.ExecutedPlans.Any(plan => plan.UsesLinkFilter));
        Assert.IsFalse(fixture.Group(Mep, "Ducts").IsHalftone);
    }

    [TestMethod]
    public async Task ResetDropsRemovedLinkEntries()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Current, Mep]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));
        fixture.Model.RemoveDocument(Mep);
        fixture.Settle();

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.AreEqual(JournalPage.Empty, fixture.Panel.Journal);
        Assert.IsEmpty(fixture.Actions.ShownLinked);
    }

    [TestMethod]
    public async Task KeepLeavesCategoryIsolate()
    {
        using var fixture = await PanelFixture.OpenAsync();
        await fixture.Explorer.Categories.IsolateCommand.ExecuteAsync(null);

        await fixture.Panel.TemporaryBar.KeepCommand.ExecuteAsync(null);
        var kept = fixture.Panel.TemporaryBar.ChangeCount;
        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.AreEqual(1, kept);
        Assert.AreEqual(1, fixture.Actions.KeepCount);
        Assert.AreEqual(1, fixture.Actions.ResetCount);
        Assert.AreEqual(0, fixture.Panel.TemporaryBar.ChangeCount);
    }

    [TestMethod]
    public async Task KeepClearsElementHide()
    {
        using var fixture = await PanelFixture.OpenAsync();
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));

        await fixture.Panel.TemporaryBar.KeepCommand.ExecuteAsync(null);

        Assert.AreEqual(JournalPage.Empty, fixture.Panel.Journal);
    }

    [TestMethod]
    public async Task RenameKeepsResetWorking()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Current, Mep]);
        var ducts = fixture.Explorer.ElementsOf([fixture.Group(Mep, "Ducts")]);
        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));
        fixture.Explorer.OpenMenu(fixture.Group(Current, "Walls"));
        await fixture.Panel.ContextMenu[MenuAction.Halftone].Command.ExecuteAsync(null);

        var rename = fixture.Model.RenameModel("saved-as.rvt");
        fixture.Settle();
        var renamed = fixture.Panel.Journal.ChangeCount;
        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.AreEqual(2, renamed);
        Assert.AreEqual(DocumentKey.Current("saved-as.rvt"), fixture.Panel.ActiveModel);
        CollectionAssert.AreEquivalent(ducts.Select(rename.Map).ToList(), fixture.Actions.ShownLinked.Single().ToList());
        Assert.AreEqual(JournalPage.Empty, fixture.Panel.Journal);
        Assert.AreEqual(1, fixture.Actions.ResetCount);
    }

    [TestMethod]
    public async Task PickingBlocksRowActions()
    {
        using var fixture = await PanelFixture.OpenAsync();
        fixture.Panel.ContextBar.Tick([Current, Mep]);
        fixture.Model.PickGate = new TaskCompletionSource<bool>();
        var picking = fixture.Explorer.Pick.PickCommand.ExecuteAsync(null);

        await fixture.Explorer.ToggleEyeCommand.ExecuteAsync(fixture.Group(Current, "Walls"));
        fixture.Model.PickGate.SetResult(true);
        await picking;

        Assert.IsEmpty(fixture.Actions.ExecutedPlans);
        Assert.AreEqual(VisibilityText.HostBusy(FakeModel.FakeHostName), fixture.Panel.Message);
    }
}

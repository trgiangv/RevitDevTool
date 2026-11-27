using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Visibility;

[TestClass]
public sealed class LinkHideRecordTests
{
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly DocumentKey Mep = FakeModelGenerator.Mep;

    [TestMethod]
    public async Task LinkHideAgreesEverywhere()
    {
        using var fixture = await OpenAsync();
        var ducts = fixture.Group(Mep, "Ducts");

        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(ducts);

        Assert.AreEqual(HideState.Hidden, fixture.Group(Mep, "Ducts").HideState);
        Assert.HasCount(ducts.Count, fixture.Panel.Journal.LinkHides);
        Assert.AreEqual(1, fixture.Panel.TemporaryBar.ChangeCount);
    }

    [TestMethod]
    public async Task LinkShowClearsRecord()
    {
        using var fixture = await OpenAsync();
        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        Assert.AreEqual(HideState.Shown, fixture.Group(Mep, "Ducts").HideState);
        Assert.AreEqual(JournalPage.Empty, fixture.Panel.Journal);
    }

    [TestMethod]
    public async Task ResetClearsLinkRecord()
    {
        using var fixture = await OpenAsync();
        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.AreEqual(HideState.Shown, fixture.Group(Mep, "Ducts").HideState);
        Assert.AreEqual(JournalPage.Empty, fixture.Panel.Journal);
        Assert.AreEqual(0, fixture.Panel.TemporaryBar.ChangeCount);
    }

    [TestMethod]
    public async Task RenameKeepsLinkHide()
    {
        using var fixture = await OpenAsync();
        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        var rename = fixture.Model.RenameModel("renamed-model");
        await fixture.Panel.RefreshAsync();

        var mep = rename.Map(Mep);
        Assert.AreEqual(HideState.Hidden, fixture.Group(mep, "Ducts").HideState);
        Assert.IsTrue(fixture.Panel.Journal.LinkHides.All(hide => hide.Element.Document == mep));
    }

    [TestMethod]
    public async Task ViewSwitchKeepsLinkHide()
    {
        using var fixture = await OpenAsync();
        var first = fixture.Model.View;
        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));

        fixture.Model.RaiseViewActivated(new ViewInfo(Current, "view-2", "Level 2", ViewKind.Plan));
        fixture.Settle();
        var other = fixture.Group(Mep, "Ducts").HideState;
        fixture.Model.RaiseViewActivated(first);
        fixture.Settle();

        Assert.AreEqual(HideState.Shown, other);
        Assert.AreEqual(HideState.Hidden, fixture.Group(Mep, "Ducts").HideState);
    }

    [TestMethod]
    public async Task IsolateRestShownOnShow()
    {
        using var fixture = await OpenAsync();
        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));
        await fixture.Panel.ContextMenu[MenuAction.Isolate].Command.ExecuteAsync(null);
        var rest = fixture.Panel.Journal.LinkHides.Where(hide => hide.IsIsolateRest).Select(hide => hide.Element).ToList();

        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Current, "Walls"));

        Assert.IsNotEmpty(rest);
        CollectionAssert.AreEquivalent(rest, fixture.Actions.ShownLinked.Single().ToList());
    }

    [TestMethod]
    public async Task ExplicitHideOutlivesIsolateShow()
    {
        using var fixture = await OpenAsync();
        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Mep, "Pipes"));
        var pipes = fixture.Panel.Journal.LinkHides.Select(hide => hide.Element).ToList();
        fixture.Explorer.OpenMenu(fixture.Group(Mep, "Ducts"));
        await fixture.Panel.ContextMenu[MenuAction.Isolate].Command.ExecuteAsync(null);

        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Current, "Walls"));

        var shown = fixture.Actions.ShownLinked.SelectMany(batch => batch).ToHashSet();
        Assert.IsFalse(pipes.Any(shown.Contains));
        Assert.AreEqual(HideState.Hidden, fixture.Group(Mep, "Pipes").HideState);
    }

    [TestMethod]
    public async Task UnshownTypedFailureReports()
    {
        using var fixture = await OpenAsync();
        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));
        fixture.Actions.LinkedNotShown = true;

        await fixture.Panel.TemporaryBar.ResetCommand.ExecuteAsync(null);

        Assert.AreEqual(VisibilityText.LinkedStayHidden, fixture.Panel.Message);
    }

    [TestMethod]
    public async Task ActiveViewLinkHideKeepsRow()
    {
        using var fixture = await OpenAsync(ScopeKind.ActiveView);
        var before = fixture.Group(Mep, "Ducts").Count;

        await fixture.Explorer.ToggleHiddenCommand.ExecuteAsync(fixture.Group(Mep, "Ducts"));
        await fixture.Panel.RefreshAsync();

        var ducts = fixture.Group(Mep, "Ducts");
        Assert.AreEqual(before, ducts.Count);
        Assert.AreEqual(HideState.Hidden, ducts.HideState);
        Assert.IsTrue(ducts.IsLinkHidden);
    }

    private static async Task<PanelFixture> OpenAsync(ScopeKind scope = ScopeKind.WholeModel)
    {
        var fixture = await PanelFixture.OpenAsync(scope: scope);
        fixture.Model.ViewLinks.Add(Mep);
        fixture.Panel.ContextBar.Check([Current, Mep]);
        await fixture.Panel.RefreshAsync();
        return fixture;
    }
}
